using System.Net;
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
            Assert.Equal(DateOnly.ParseExact(day, "yyyy-MM-dd"), election.Date);
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
        [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
        [InlineData(HttpStatusCode.OK, "<html>down for maintenance</html>")]
        [InlineData(HttpStatusCode.OK, "{}")]
        [InlineData(HttpStatusCode.OK, "[]")]
        [InlineData(HttpStatusCode.OK, "{ \"election\": { \"electionDay\": \"soon\" } }")]
        public async Task GetAsync_ShowsNothingWhenThereIsNoElectionToRead(
            HttpStatusCode status,
            string body)
        {
            var voterInfo = VoterInfo((_, _) => Task.FromResult(FakeGeocoding.Reply(body, status)));

            Assert.Null(await voterInfo.GetAsync(Raleigh, default));
        }

        /// <summary>A DNS failure, as it looks from here, and a timeout.</summary>
        public static TheoryData<Exception> CallFailures => new()
        {
            new HttpRequestException("no such host"),
            new TaskCanceledException("the client gave up waiting"),
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

            var election = await new GoogleVoterInfo(new HttpClient(handler), Settings(apiKey: null))
                .GetAsync(Raleigh, default);

            Assert.Null(election);
            Assert.Null(handler.LastRequest);
        }

        [Fact]
        public async Task GetAsync_SendsTheKeyAsAHeaderAndTheAddressInTheQuery()
        {
            var handler = new FakeGeocoding.StubHandler(
                (_, _) => Task.FromResult(FakeGeocoding.Reply("{}")));

            await new GoogleVoterInfo(new HttpClient(handler), Settings("test-key"))
                .GetAsync(Raleigh, default);

            Assert.NotNull(handler.LastRequest);
            Assert.Equal(
                "test-key",
                Assert.Single(handler.LastRequest.Headers.GetValues("X-Goog-Api-Key")));

            // Never in the URL, which is what request logs print.
            Assert.DoesNotContain("test-key", handler.LastRequest.RequestUri!.ToString());
            Assert.Contains("address=16%20W%20JONES%20ST%2C%20RALEIGH%2C%20NC%2C%2027601", handler.LastQuery);
        }

        private static GoogleVoterInfo VoterInfo(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) =>
            new(new HttpClient(new FakeGeocoding.StubHandler(reply)), Settings("test-key"));

        private static IConfiguration Settings(string? apiKey) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GoogleCivic:ApiKey"] = apiKey,
                })
                .Build();
    }
}
