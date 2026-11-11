using System.Globalization;
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// Google's voter lookup, against a stubbed handler. What matters is that the counts
    /// are read the way the footer states them, and that no way of getting no answer ever
    /// throws: the election is extra, and must not cost a visitor their districts.
    /// </summary>
    public class GoogleVoterInfoTests
    {
        private const string Raleigh = "16 W JONES ST, RALEIGH, NC, 27601";

        [Fact]
        public async Task GetAsync_CountsCandidatesAndMeasuresSeparately()
        {
            var day = FakeVoterInfo.DaysFromToday(30);
            var voterInfo = VoterInfo((_, _) => Task.FromResult(
                FakeGeocoding.Reply(FakeVoterInfo.Election(day))));

            var election = await voterInfo.GetAsync(Raleigh, default);

            Assert.NotNull(election);
            Assert.Equal(DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture), election.Date);
            Assert.Equal(5, election.Candidates);
            Assert.Equal(1, election.Measures);
        }

        [Fact]
        public async Task GetAsync_LeavesTheCountsUnknownWhenTheAnswerHoldsNoContests()
        {
            // Not zero: a ballot nobody has seen is not an empty one.
            var voterInfo = VoterInfo((_, _) => Task.FromResult(FakeGeocoding.Reply($$"""
                { "election": { "electionDay": "{{FakeVoterInfo.DaysFromToday(10)}}" } }
                """)));

            var election = await voterInfo.GetAsync(Raleigh, default);

            Assert.NotNull(election);
            Assert.Null(election.Candidates);
            Assert.Null(election.Measures);
        }

        [Fact]
        public async Task GetAsync_ShowsNothingForAnElectionThatIsOver()
        {
            var voterInfo = VoterInfo((_, _) => Task.FromResult(
                FakeGeocoding.Reply(FakeVoterInfo.Election(FakeVoterInfo.DaysFromToday(-3)))));

            Assert.Null(await voterInfo.GetAsync(Raleigh, default));
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, FakeVoterInfo.NoElection)] // no election here
        [InlineData(HttpStatusCode.Forbidden, "{}")] // a bad key, or the quota spent
        [InlineData(HttpStatusCode.OK, "<html>down for maintenance</html>")]
        [InlineData(HttpStatusCode.OK, "{ \"election\": { \"electionDay\": \"soon\" } }")]
        public async Task GetAsync_ShowsNothingWhenThereIsNoElectionToRead(
            HttpStatusCode status,
            string body)
        {
            var voterInfo = VoterInfo((_, _) => Task.FromResult(FakeGeocoding.Reply(body, status)));

            Assert.Null(await voterInfo.GetAsync(Raleigh, default));
        }

        /// <summary>A DNS failure, as it looks from here, a timeout, and anything else.</summary>
        public static TheoryData<Exception> CallFailures => new()
        {
            new HttpRequestException("no such host"),
            new TaskCanceledException("the client gave up waiting"),
            new InvalidOperationException("nobody thought of this"),
        };

        [Theory]
        [MemberData(nameof(CallFailures))]
        public async Task GetAsync_ShowsNothingWhenTheCallItselfFails(Exception failure)
        {
            var voterInfo = VoterInfo((_, _) => throw failure);

            Assert.Null(await voterInfo.GetAsync(Raleigh, default));
        }

        [Fact]
        public async Task GetAsync_StopsRatherThanAnsweringWhenTheRequestIsAbandoned()
        {
            using var abandoned = new CancellationTokenSource();
            await abandoned.CancelAsync();

            var voterInfo = VoterInfo((_, token) =>
            {
                token.ThrowIfCancellationRequested();

                return Task.FromResult(FakeGeocoding.Reply("{}"));
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => voterInfo.GetAsync(Raleigh, abandoned.Token));
        }

        [Fact]
        public async Task GetAsync_AsksNothingWithoutAKey()
        {
            // A fresh clone has no key, and the site still works: it shows no election.
            var handler = new FakeGeocoding.StubHandler(
                (_, _) => Task.FromResult(FakeGeocoding.Reply("{}")));

            var election = await Google(handler, apiKey: null).GetAsync(Raleigh, default);

            Assert.Null(election);
            Assert.Null(handler.LastRequest);
        }

        [Fact]
        public async Task GetAsync_SendsTheKeyAsAHeaderAndTheAddressAndElectionInTheQuery()
        {
            var handler = Handler((_, _) => Task.FromResult(FakeGeocoding.Reply("{}")));

            await Google(handler)
                .GetAsync(Raleigh, default);

            Assert.NotNull(handler.LastRequest);
            Assert.Equal(
                "test-key",
                Assert.Single(handler.LastRequest.Headers.GetValues("X-Goog-Api-Key")));

            // Never in the URL, which is what request logs print.
            Assert.DoesNotContain("test-key", handler.LastRequest.RequestUri!.ToString());
            Assert.Contains("address=16%20W%20JONES%20ST%2C%20RALEIGH%2C%20NC%2C%2027601", handler.LastQuery);
            Assert.Contains("electionId=9000", handler.LastQuery);
        }

        [Fact]
        public async Task GetAsync_AsksAboutTheSoonestElectionCoveringTheAddressesState()
        {
            // Past, another state's, a county's in this state, then nationwide: the
            // county's is soonest of the ones that can cover the address.
            var elections = FakeVoterInfo.Elections(
                ("1", FakeVoterInfo.DaysFromToday(-5), "ocd-division/country:us"),
                ("2", FakeVoterInfo.DaysFromToday(5), "ocd-division/country:us/state:ca"),
                ("3", FakeVoterInfo.DaysFromToday(20), "ocd-division/country:us/state:nc/county:wake"),
                ("4", FakeVoterInfo.DaysFromToday(30), "ocd-division/country:us"));
            var handler = Handler((_, _) => Task.FromResult(FakeGeocoding.Reply("{}")), elections);

            await Google(handler)
                .GetAsync(Raleigh, default);

            Assert.Contains("electionId=3", handler.LastQuery);
        }

        [Fact]
        public async Task GetAsync_AsksNoMoreWhenNoElectionIsComing()
        {
            // Google's test election is always listed and always in the future.
            var elections = FakeVoterInfo.Elections(
                ("2", FakeVoterInfo.DaysFromToday(5), "ocd-division/country:us/state:ca"));
            var voterInfoAsked = false;
            var handler = Handler(
                (_, _) =>
                {
                    voterInfoAsked = true;
                    return Task.FromResult(FakeGeocoding.Reply(FakeVoterInfo.Election(FakeVoterInfo.DaysFromToday(5))));
                },
                elections);

            var election = await Google(handler)
                .GetAsync(Raleigh, default);

            Assert.Null(election);
            Assert.False(voterInfoAsked);
        }

        [Theory]
        [InlineData(HttpStatusCode.Forbidden, "{}")]
        [InlineData(HttpStatusCode.OK, "<html>down for maintenance</html>")]
        public async Task GetAsync_ShowsNothingWhenTheElectionListCannotBeRead(
            HttpStatusCode status,
            string body)
        {
            var handler = new FakeGeocoding.StubHandler((request, _) => Task.FromResult(
                FakeVoterInfo.IsElectionList(request)
                    ? FakeGeocoding.Reply(body, status)
                    : FakeGeocoding.Reply(FakeVoterInfo.Election(FakeVoterInfo.DaysFromToday(30)))));

            var election = await Google(handler)
                .GetAsync(Raleigh, default);

            Assert.Null(election);
        }

        [Fact]
        public async Task GetAsync_ReadsTheElectionListOnceForManyLookups()
        {
            var listsRead = 0;
            var handler = new FakeGeocoding.StubHandler((request, _) =>
            {
                if (FakeVoterInfo.IsElectionList(request))
                {
                    Interlocked.Increment(ref listsRead);
                    return Task.FromResult(FakeGeocoding.Reply(FakeVoterInfo.Elections(
                        ("9000", FakeVoterInfo.DaysFromToday(30), "ocd-division/country:us"))));
                }

                return Task.FromResult(FakeGeocoding.Reply(FakeVoterInfo.Election(FakeVoterInfo.DaysFromToday(30))));
            });
            var voterInfo = Google(handler);

            for (var i = 0; i < 3; i++)
            {
                Assert.NotNull(await voterInfo.GetAsync(Raleigh, default));
            }

            Assert.Equal(1, listsRead);
        }

        [Fact]
        public async Task GetAsync_AsksForTheElectionListAgainAfterItCouldNotBeRead()
        {
            var listsRead = 0;
            var handler = new FakeGeocoding.StubHandler((request, _) =>
            {
                if (FakeVoterInfo.IsElectionList(request))
                {
                    // Down the first time, answering the second.
                    return Task.FromResult(Interlocked.Increment(ref listsRead) == 1
                        ? FakeGeocoding.Reply("{}", HttpStatusCode.ServiceUnavailable)
                        : FakeGeocoding.Reply(FakeVoterInfo.Elections(
                            ("9000", FakeVoterInfo.DaysFromToday(30), "ocd-division/country:us"))));
                }

                return Task.FromResult(FakeGeocoding.Reply(FakeVoterInfo.Election(FakeVoterInfo.DaysFromToday(30))));
            });
            var voterInfo = Google(handler);

            Assert.Null(await voterInfo.GetAsync(Raleigh, default));
            Assert.NotNull(await voterInfo.GetAsync(Raleigh, default));
            Assert.Equal(2, listsRead);
        }

        private static GoogleVoterInfo VoterInfo(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) =>
            Google(Handler(reply));

        /// <summary>A fresh cache each time, so no test sees another's election list.</summary>
        private static GoogleVoterInfo Google(HttpMessageHandler handler, string? apiKey = "test-key") =>
            new(new HttpClient(handler), Settings(apiKey), new MemoryCache(new MemoryCacheOptions()));

        /// <summary>
        /// Answers the election list with <paramref name="elections"/>, by default a
        /// nationwide election in 30 days, and the voter lookup however the test says.
        /// </summary>
        private static FakeGeocoding.StubHandler Handler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply,
            string? elections = null) =>
            new((request, token) => FakeVoterInfo.IsElectionList(request)
                ? Task.FromResult(FakeGeocoding.Reply(elections ?? FakeVoterInfo.Elections(
                    ("9000", FakeVoterInfo.DaysFromToday(30), "ocd-division/country:us"))))
                : reply(request, token));

        private static IConfiguration Settings(string? apiKey) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GoogleCivic:ApiKey"] = apiKey,
                })
                .Build();
    }
}
