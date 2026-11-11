using System.Globalization;
using System.Net;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// The geocoder, which every address lookup starts with, against a stubbed handler. What
    /// matters is that an address it does not know comes back as no matches, and that
    /// every way of getting no answer at all comes back as unavailable — never as "no
    /// such address", which would tell someone their address is wrong.
    /// </summary>
    public class CensusGeocoderTests
    {
        [Fact]
        public async Task FindAsync_ReadsEveryMatchInOrder()
        {
            var geocoder = Geocoder((_, _) => Task.FromResult(FakeGeocoding.Reply(
                FakeGeocoding.Match(
                    new(34.8526, -82.3940, "100 MAIN ST, GREENVILLE, SC, 29601"),
                    new(35.7822, -78.6398, "16 W JONES ST, RALEIGH, NC, 27601")))));

            var found = await geocoder.FindAsync("16 W Jones St", default);

            Assert.Equal(2, found.Count);
            Assert.Equal(34.8526, found[0].Latitude);
            Assert.Equal(35.7822, found[1].Latitude);

            // x is the longitude, which is the way round it is easy to get wrong.
            Assert.Equal(-78.6398, found[1].Longitude);

            // Each match carries the geocoder's spelling of it, not what was typed.
            Assert.Equal("16 W JONES ST, RALEIGH, NC, 27601", found[1].Address);
        }

        [Fact]
        public async Task FindAsync_ReadsTheSameAnswerOnAMachineThatWritesDecimalCommas()
        {
            var saved = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var geocoder = Geocoder((_, _) => Task.FromResult(FakeGeocoding.Reply(
                    FakeGeocoding.Match(new FakeGeocoding.Place(35.7822, -78.6398, "16 W JONES ST, RALEIGH, NC, 27601")))));

                var found = Assert.Single(await geocoder.FindAsync("16 W Jones St", default));

                Assert.Equal(35.7822, found.Latitude);
                Assert.Equal(-78.6398, found.Longitude);
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [Fact]
        public async Task FindAsync_FallsBackToTheTypedAddressWhenAMatchIsNotSpelledOut()
        {
            var geocoder = Geocoder((_, _) => Task.FromResult(FakeGeocoding.Reply("""
                { "result": { "addressMatches": [ { "coordinates": { "x": -78.6398, "y": 35.7822 } } ] } }
                """)));

            var found = await geocoder.FindAsync("16 W Jones St, Raleigh NC", default);

            Assert.Equal("16 W Jones St, Raleigh NC", Assert.Single(found).Address);
        }

        [Fact]
        public async Task FindAsync_AsksAboutTheAddressItWasGiven()
        {
            var handler = new FakeGeocoding.StubHandler(
                (_, _) => Task.FromResult(FakeGeocoding.Reply(FakeGeocoding.NoMatch)));

            await new CensusGeocoder(new HttpClient(handler))
                .FindAsync("16 W Jones St, Raleigh NC", default);

            Assert.NotNull(handler.LastQuery);
            Assert.Contains("16%20W%20Jones%20St%2C%20Raleigh%20NC", handler.LastQuery);
            Assert.Contains("benchmark=Public_AR_Current", handler.LastQuery);
        }

        [Theory]
        [InlineData(FakeGeocoding.NoMatch)] // an address it cannot place
        [InlineData("{ \"result\": {} }")]
        [InlineData("{ \"result\": { \"addressMatches\": \"none\" } }")]
        [InlineData("{ \"result\": { \"addressMatches\": [ {} ] } }")]
        public async Task FindAsync_ReturnsNothingWhenTheAnswerHoldsNoMatch(string body)
        {
            var geocoder = Geocoder((_, _) => Task.FromResult(FakeGeocoding.Reply(body)));

            Assert.Empty(await geocoder.FindAsync("16 W Jones St", default));
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError, "{}")]
        [InlineData(HttpStatusCode.TooManyRequests, "{}")]
        [InlineData(HttpStatusCode.OK, "<html>down for maintenance</html>")]
        public async Task FindAsync_IsUnavailableWhenTheAnswerIsNotOne(
            HttpStatusCode status,
            string body)
        {
            var geocoder = Geocoder((_, _) => Task.FromResult(FakeGeocoding.Reply(body, status)));

            await Assert.ThrowsAsync<GeocoderUnavailableException>(
                () => geocoder.FindAsync("16 W Jones St", default));
        }

        /// <summary>A DNS failure, as it looks from here, and a timeout.</summary>
        public static TheoryData<Exception> CallFailures => new()
        {
            new HttpRequestException("no such host"),
            new TaskCanceledException("the client gave up waiting"),
        };

        [Theory]
        [MemberData(nameof(CallFailures))]
        public async Task FindAsync_IsUnavailableWhenTheCallItselfFails(Exception failure)
        {
            var geocoder = Geocoder((_, _) => throw failure);

            await Assert.ThrowsAsync<GeocoderUnavailableException>(
                () => geocoder.FindAsync("16 W Jones St", default));
        }

        [Fact]
        public async Task FindAsync_StopsRatherThanAnsweringWhenTheRequestIsAbandoned()
        {
            // The browser went away. That is not a failure to place the address, and
            // swallowing it would have the server finish work nobody is waiting for.
            using var abandoned = new CancellationTokenSource();
            await abandoned.CancelAsync();

            var geocoder = Geocoder((_, token) =>
            {
                token.ThrowIfCancellationRequested();

                return Task.FromResult(FakeGeocoding.Reply(FakeGeocoding.NoMatch));
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => geocoder.FindAsync("16 W Jones St", abandoned.Token));
        }

        private static CensusGeocoder Geocoder(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) =>
            new(new HttpClient(new FakeGeocoding.StubHandler(reply)));
    }
}
