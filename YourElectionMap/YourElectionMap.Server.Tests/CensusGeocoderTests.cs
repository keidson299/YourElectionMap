using System.Net;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// The one call this app makes out to the internet, against a stubbed handler. What
    /// matters is that every way of failing comes back as "could not place it" rather
    /// than as an exception reaching the page.
    /// </summary>
    public class CensusGeocoderTests
    {
        [Fact]
        public async Task FindAsync_ReadsTheCoordinatesOfTheFirstMatch()
        {
            var geocoder = Geocoder(
                (_, _) => Task.FromResult(FakeGeocoding.Reply(FakeGeocoding.Match(35.7822, -78.6398))));

            var found = await geocoder.FindAsync("16 W Jones St, Raleigh NC", default);

            Assert.NotNull(found);
            Assert.Equal(35.7822, found.Value.Latitude);

            // x is the longitude, which is the way round it is easy to get wrong.
            Assert.Equal(-78.6398, found.Value.Longitude);
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

        [Fact]
        public async Task FindAsync_ReturnsNothingWhenTheAddressCannotBePlaced()
        {
            var geocoder = Geocoder(
                (_, _) => Task.FromResult(FakeGeocoding.Reply(FakeGeocoding.NoMatch)));

            Assert.Null(await geocoder.FindAsync("nowhere at all", default));
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError, "{}")]
        [InlineData(HttpStatusCode.TooManyRequests, "{}")]
        [InlineData(HttpStatusCode.OK, "<html>down for maintenance</html>")]
        [InlineData(HttpStatusCode.OK, "{ \"result\": {} }")]
        [InlineData(HttpStatusCode.OK, "{ \"result\": { \"addressMatches\": \"none\" } }")]
        [InlineData(HttpStatusCode.OK, "{ \"result\": { \"addressMatches\": [ {} ] } }")]
        public async Task FindAsync_ReturnsNothingWhenTheAnswerIsNotOne(
            HttpStatusCode status,
            string body)
        {
            var geocoder = Geocoder((_, _) => Task.FromResult(FakeGeocoding.Reply(body, status)));

            Assert.Null(await geocoder.FindAsync("16 W Jones St", default));
        }

        [Fact]
        public async Task FindAsync_ReturnsNothingWhenTheCallItselfFails()
        {
            // What a timeout or a DNS failure looks like from here.
            var geocoder = Geocoder(
                (_, _) => throw new HttpRequestException("no such host"));

            Assert.Null(await geocoder.FindAsync("16 W Jones St", default));
        }

        [Fact]
        public async Task FindAsync_ReturnsNothingWhenItRunsOutOfTime()
        {
            var geocoder = Geocoder(
                (_, _) => throw new TaskCanceledException("the client gave up waiting"));

            Assert.Null(await geocoder.FindAsync("16 W Jones St", default));
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
