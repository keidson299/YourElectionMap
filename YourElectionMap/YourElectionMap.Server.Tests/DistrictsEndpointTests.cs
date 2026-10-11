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
    /// The geocoder's and Google's handlers are replaced, so no test reaches the internet.
    /// Everything else is real, including every state's boundaries.
    /// </para>
    /// </summary>
    public class DistrictsEndpointTests : IClassFixture<DistrictsEndpointTests.OfflineApp>
    {
        /// <summary>16 W Jones Street, Raleigh: an address the stubbed geocoder places.</summary>
        private const string RaleighAddress = "16 W Jones St, Raleigh NC";

        /// <summary>The California State Capitol: an address the stubbed geocoder places.</summary>
        private const string SacramentoAddress = "1315 10th St, Sacramento CA";

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
            Assert.Equal("NCGA enacted maps", lookup.GetProperty("source").GetString());

            // The stub's election: a date the client reads as "2026-11-03", and the counts
            // the rail's footer states.
            var election = lookup.GetProperty("election");

            Assert.Equal(FakeVoterInfo.DaysFromToday(30), election.GetProperty("date").GetString());
            Assert.Equal(5, election.GetProperty("candidates").GetInt32());
            Assert.Equal(1, election.GetProperty("measures").GetInt32());

            // The address as [longitude, latitude], the order GeoJSON and the map use.
            var location = Numbers(lookup.GetProperty("location"));

            Assert.InRange(location[0], -84.4, -75.4);
            Assert.InRange(location[1], 33.8, 36.6);

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
            // Pinned, so that a change to the converter or the data file
            // cannot quietly start answering with the wrong districts.
            var lookup = await LookupAsync(AddressQuery(RaleighAddress));

            Assert.Equal(
                new[] { "congressional-2", "state-upper-14", "state-lower-38" },
                Each(lookup.GetProperty("districts"), "id"));
        }

        [Fact]
        public async Task Get_PlacesASacramentoAddressInCaliforniasDistricts()
        {
            // Pinned the same way, against the TIGER layers.
            var lookup = await LookupAsync(AddressQuery(SacramentoAddress));
            var districts = lookup.GetProperty("districts");

            Assert.Equal(
                new[] { "congressional-7", "state-upper-8", "state-lower-6" },
                Each(districts, "id"));
            Assert.Equal(
                new[] { "U.S. House", "State Senate", "State Assembly" },
                Each(districts, "office"));

            // TIGER has no population: sent as null, not left out, and not 0.
            foreach (var district in districts.EnumerateArray())
            {
                Assert.Equal(JsonValueKind.Null, district.GetProperty("population").ValueKind);
            }
        }

        [Theory]
        [InlineData(RaleighAddress, "NCGA enacted maps")]
        [InlineData(SacramentoAddress, "U.S. Census Bureau TIGER")]
        public async Task Get_NamesEachStatesOwnSource(string address, string source)
        {
            var lookup = await LookupAsync(AddressQuery(address));

            Assert.Equal(source, lookup.GetProperty("source").GetString());
        }

        [Fact]
        public async Task Get_DrawsEachDistrictAsMultiPolygonCoordinates()
        {
            var lookup = await LookupAsync(AddressQuery(RaleighAddress));

            foreach (var district in lookup.GetProperty("districts").EnumerateArray())
            {
                // Polygons, each of rings, each of [longitude, latitude] points: what a
                // GeoJSON MultiPolygon holds, so the client hands it to the map as it is.
                var boundary = district.GetProperty("boundary");

                Assert.True(boundary.GetArrayLength() > 0);

                foreach (var polygon in boundary.EnumerateArray())
                {
                    Assert.True(polygon.GetArrayLength() > 0);

                    foreach (var ring in polygon.EnumerateArray())
                    {
                        var first = Numbers(ring[0]);

                        Assert.True(ring.GetArrayLength() >= 4);
                        Assert.Equal(2, first.Length);
                        Assert.InRange(first[0], -84.4, -75.4);
                        Assert.InRange(first[1], 33.8, 36.6);

                        // Closed: the last point repeats the first, as GeoJSON requires.
                        Assert.Equal(first, Numbers(ring[ring.GetArrayLength() - 1]));
                    }
                }
            }
        }

        [Fact]
        public async Task Get_CompressesTheLookupForABrowserThatAsks()
        {
            // Full-detail boundaries are tens of kilobytes of coordinates, or more.
            var client = _app.CreateClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/districts?{AddressQuery(RaleighAddress)}");
            request.Headers.AcceptEncoding.ParseAdd("gzip");

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("gzip", response.Content.Headers.ContentEncoding);
        }

        [Fact]
        public async Task Get_SendsNullRatherThanOmittingWhatIsNotKnown()
        {
            var lookup = await LookupAsync(AddressQuery(RaleighAddress));
            var first = lookup.GetProperty("districts")[0];

            // The plans name no place, and the chip leaves out what it is not given.
            Assert.Equal(JsonValueKind.Null, first.GetProperty("area").ValueKind);
            Assert.Equal("District", first.GetProperty("unit").GetString());
        }

        [Fact]
        public async Task Get_SendsEachDistrictsPopulationAsANumber()
        {
            var lookup = await LookupAsync(AddressQuery(RaleighAddress));

            foreach (var district in lookup.GetProperty("districts").EnumerateArray())
            {
                Assert.True(district.GetProperty("population").GetInt32() > 0);
            }
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

            // Google's voter lookup takes an address, and a point has none.
            Assert.Equal(JsonValueKind.Null, lookup.GetProperty("election").ValueKind);
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
        public async Task Get_UsesTheFirstMatchInACoveredState()
        {
            // The stub answers Greenville SC first and Greenville NC second, as the real
            // geocoder can for an address with no state.
            var lookup = await LookupAsync(AddressQuery("100 Main St, Greenville"));

            var location = Numbers(lookup.GetProperty("location"));

            Assert.Equal(-77.3664, location[0], 0.0001);
            Assert.Equal(35.6127, location[1], 0.0001);
            Assert.Equal(3, lookup.GetProperty("districts").GetArrayLength());
        }

        [Fact]
        public async Task Get_AsksGoogleAboutTheAddressTheGeocoderMatchedInACoveredState()
        {
            // Typed with no state, and matched in South Carolina first: Google must be
            // asked about the match the districts came from, spelled with its state.
            await LookupAsync(AddressQuery("100 Main St, Greenville"));

            Assert.Contains(
                FakeVoterInfo.Asked,
                asked => asked.Contains("100 MAIN ST, GREENVILLE, NC, 27858"));
            Assert.DoesNotContain(FakeVoterInfo.Asked, asked => asked.Contains(", SC,"));
        }

        [Fact]
        public async Task Get_DoesNotAskGoogleAboutAnAddressOutsideTheCoveredStates()
        {
            await GetAsync(AddressQuery("1600 Pennsylvania Ave, Washington DC"));

            Assert.DoesNotContain(FakeVoterInfo.Asked, asked => asked.Contains("PENNSYLVANIA"));
        }

        [Theory]
        [InlineData("101 S Tryon St")] // Google has no election for it
        [InlineData("101 N Mangum St")] // Google is down
        [InlineData(SacramentoAddress)] // Google has no election for it either
        public async Task Get_StillAnswersWhenGoogleHasNoElectionToGive(string address)
        {
            // The districts do not depend on the election, so they come back without it.
            var lookup = await LookupAsync(AddressQuery(address));

            Assert.Equal(3, lookup.GetProperty("districts").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, lookup.GetProperty("election").ValueKind);
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

        /// <summary>The app with both its outward calls stubbed out.</summary>
        public sealed class OfflineApp : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                // Any key will do for the stub; without one, Google is never asked.
                builder.UseSetting("GoogleCivic:ApiKey", "test-key");

                builder.ConfigureTestServices(services =>
                {
                    services
                        .AddHttpClient<CensusGeocoder>()
                        .ConfigurePrimaryHttpMessageHandler(FakeGeocoding.Handler);
                    services
                        .AddHttpClient<GoogleVoterInfo>()
                        .ConfigurePrimaryHttpMessageHandler(FakeVoterInfo.Handler);
                });
            }
        }
    }
}
