using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// Hosts the real app in memory, so routing, model binding and the JSON on the wire
    /// are covered. The client reads these exact names and formats, so this is where the
    /// contract is pinned down.
    /// <para>
    /// The geocoder's handler is replaced, so no test reaches the internet. Everything
    /// else is real, including the four megabytes of North Carolina boundaries.
    /// </para>
    /// </summary>
    public class DistrictsEndpointTests : IClassFixture<DistrictsEndpointTests.OfflineApp>
    {
        /// <summary>16 W Jones Street, Raleigh: an address the stubbed geocoder places.</summary>
        private const string RaleighAddress = "16 W Jones St, Raleigh NC";

        private readonly OfflineApp _app;

        public DistrictsEndpointTests(OfflineApp app)
        {
            _app = app;
        }

        [Fact]
        public async Task Get_WithAnAddress_ReturnsTheLookupTheClientReads()
        {
            var lookup = await LookupAsync(AddressQuery(RaleighAddress));

            Assert.Equal("16 W Jones St", lookup.GetProperty("label").GetString());
            Assert.Equal("NC 2024 precincts", lookup.GetProperty("source").GetString());

            // No election data yet, which is what hides the rail's footer.
            Assert.Equal(JsonValueKind.Null, lookup.GetProperty("election").ValueKind);

            // The address, in map units: inside the state, and north of the projection's
            // origin, so y is negative.
            var location = Numbers(lookup.GetProperty("location"));

            Assert.InRange(location[0], 4_200, 32_000);
            Assert.InRange(location[1], -10_900, 0);

            var districts = lookup.GetProperty("districts");

            // Broad to local, the order the rail and map read. Three: the levels this data
            // covers.
            Assert.Equal(
                new[] { "congressional", "state-upper", "state-lower" },
                Each(districts, "level"));
            Assert.Equal(
                new[] { "U.S. House", "State Senate", "State House" },
                Each(districts, "office"));
            Assert.Equal(new[] { "Federal", "State", "State" }, Each(districts, "detail"));
        }

        [Fact]
        public async Task Get_PlacesARaleighAddressInTheDistrictsThatCoverIt()
        {
            // Pinned, so that a change to the projection, the dissolve or the data file
            // cannot quietly start answering with the wrong districts.
            var lookup = await LookupAsync(AddressQuery(RaleighAddress));

            Assert.Equal(
                new[] { "congressional-2", "state-upper-14", "state-lower-38" },
                Each(lookup.GetProperty("districts"), "id"));
        }

        [Fact]
        public async Task Get_DrawsEachDistrictAsRingsOfPoints()
        {
            var lookup = await LookupAsync(AddressQuery(RaleighAddress));

            foreach (var district in lookup.GetProperty("districts").EnumerateArray())
            {
                var boundary = district.GetProperty("boundary");

                Assert.True(boundary.GetArrayLength() > 0);

                var ring = boundary[0];

                // Enough points to be a real boundary, few enough to send.
                Assert.InRange(ring.GetArrayLength(), 20, 4000);
                Assert.Equal(2, ring[0].GetArrayLength());

                // Closed: the last point repeats the first, so the path needs no fixing up.
                Assert.Equal(Numbers(ring[0]), Numbers(ring[ring.GetArrayLength() - 1]));
            }
        }

        [Fact]
        public async Task Get_SendsNullRatherThanOmittingWhatIsNotKnown()
        {
            var lookup = await LookupAsync(AddressQuery(RaleighAddress));
            var first = lookup.GetProperty("districts")[0];

            // Neither the area nor the population is in this data, and the rail and chip
            // leave out what they are not given.
            Assert.Equal(JsonValueKind.Null, first.GetProperty("area").ValueKind);
            Assert.Equal(JsonValueKind.Null, first.GetProperty("population").ValueKind);
            Assert.Equal("District", first.GetProperty("unit").GetString());
        }

        [Theory]
        [InlineData("16 W Jones St, Raleigh NC", "16 W Jones St")]
        [InlineData("101 S Tryon St", "101 S Tryon St")]
        public async Task Get_NamesAnAddressByItsStreetLine(string address, string label)
        {
            var lookup = await LookupAsync(AddressQuery(address));

            Assert.Equal(label, lookup.GetProperty("label").GetString());
        }

        [Fact]
        public async Task Get_NamesALocatedLookupPlainly()
        {
            var lookup = await LookupAsync("lat=35.7822&lon=-78.6398");

            Assert.Equal("Your location", lookup.GetProperty("label").GetString());
            Assert.Equal(3, lookup.GetProperty("districts").GetArrayLength());
        }

        [Fact]
        public async Task Get_PrefersTheAddressWhenBothAreGiven()
        {
            var lookup = await LookupAsync(
                $"{AddressQuery(RaleighAddress)}&lat=35.2271&lon=-80.8431");

            Assert.Equal("16 W Jones St", lookup.GetProperty("label").GetString());
        }

        [Theory]
        [InlineData("address=nowhere%20at%20all")] // the geocoder cannot place it
        [InlineData("address=1600%20Pennsylvania%20Ave%2C%20Washington%20DC")] // placed, but not here
        [InlineData("lat=38.8977&lon=-77.0365")] // Washington DC
        [InlineData("lat=30&lon=-30")] // the middle of the Atlantic
        public async Task Get_Returns404WhenThereIsNothingToShow(string query)
        {
            var response = await GetAsync(query);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Get_UsesTheFirstMatchInNorthCarolina()
        {
            // The stub answers Greenville SC first and Greenville NC second, as the real
            // geocoder can for an address with no state.
            var lookup = await LookupAsync(AddressQuery("100 Main St, Greenville"));

            var (x, y) = StatePlane.ToMapUnits(35.6127, -77.3664);
            var location = Numbers(lookup.GetProperty("location"));

            Assert.Equal(x, location[0], 0.001);
            Assert.Equal(y, location[1], 0.001);
            Assert.Equal(3, lookup.GetProperty("districts").GetArrayLength());
        }

        [Fact]
        public async Task Get_Returns503WhenTheGeocoderIsUnavailable()
        {
            // Not a 404: the address may be fine, and the page offers to try again.
            var response = await GetAsync(AddressQuery($"16 W Jones St {FakeGeocoding.Outage}"));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
        }

        [Theory]
        [InlineData("")] // nothing to look up
        [InlineData("address=%20%20")] // only spaces
        [InlineData("lat=39.8")] // half a point
        [InlineData("lat=91&lon=0")] // off the globe
        [InlineData("lat=abc&lon=-89.65")] // not a number
        public async Task Get_Returns400ForAQueryItCannotUse(string query)
        {
            var response = await GetAsync(query);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Get_Returns400ForAnAddressTooLongToBeReal()
        {
            var response = await GetAsync(AddressQuery(new string('x', 201)));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Get_TellsNothingToKeepTheAddressItEchoes()
        {
            var response = await GetAsync(AddressQuery(RaleighAddress));

            Assert.True(response.Headers.CacheControl?.NoStore);
        }

        private Task<HttpResponseMessage> GetAsync(string query) =>
            _app.CreateClient().GetAsync($"/api/districts?{query}");

        private async Task<JsonElement> LookupAsync(string query)
        {
            var response = await GetAsync(query);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            return document.RootElement.Clone();
        }

        private static string AddressQuery(string address) =>
            $"address={Uri.EscapeDataString(address)}";

        private static string?[] Each(JsonElement districts, string property) =>
            districts
                .EnumerateArray()
                .Select(district => district.GetProperty(property).GetString())
                .ToArray();

        private static double[] Numbers(JsonElement array) =>
            array.EnumerateArray().Select(number => number.GetDouble()).ToArray();

        /// <summary>The app with its one outward call stubbed out.</summary>
        public sealed class OfflineApp : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureTestServices(services =>
                    services
                        .AddHttpClient<CensusGeocoder>()
                        .ConfigurePrimaryHttpMessageHandler(FakeGeocoding.Handler));
            }
        }
    }
}
