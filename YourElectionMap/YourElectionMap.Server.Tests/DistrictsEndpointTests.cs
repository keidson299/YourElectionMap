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

            Assert.Equal("U.S. Census Bureau TIGER", lookup.GetProperty("source").GetString());

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

            // Broad to local, the order the rail and map read. The ids are pinned, so
            // that a change to the converter or the data file cannot quietly start
            // answering with the wrong districts.
            Assert.Equal(
                new[]
                {
                    "congressional-2", "state-upper-14", "state-lower-38",
                    "county-37183", "city-3755000", "school-3704720",
                },
                Each(districts, "id"));
            Assert.Equal(
                new[] { "congressional", "state-upper", "state-lower", "county", "city", "school" },
                Each(districts, "level"));
            Assert.Equal(
                new[]
                {
                    "U.S. House", "State Senate", "State House",
                    "Wake County", "Raleigh city", "Wake County Schools",
                },
                Each(districts, "office"));
            Assert.Equal(
                new[] { "Federal", "State", "State", "County", "City or town", "School district" },
                Each(districts, "detail"));

            // The county is named on every other district's map chip.
            Assert.Equal(
                new[] { "Wake County", "Wake County", "Wake County", null, "Wake County", "Wake County" },
                Each(districts, "area"));
        }

        [Theory]
        [InlineData(SacramentoAddress, "county-06067", "city-0664000", "school-0633840")]
        [InlineData("900 Court St NE, Salem OR", "county-41047", "city-4164900", "school-4110820")]
        [InlineData("1100 Gervais St, Columbia SC", "county-45079", "city-4516000", "school-4503360")]
        [InlineData("416 Sid Snyder Ave SW, Olympia WA", "county-53067", "city-5351300", "school-5306180")]
        [InlineData("1700 W Washington St, Phoenix AZ", "county-04013", "city-0455000", "school-0406330", "school-0406300")]
        [InlineData("700 W Jefferson St, Boise ID", "county-16001", "city-1608830", "school-1600360")]
        [InlineData("1301 E 6th Ave, Helena MT", "county-30049", "city-3035600", "school-3013830", "school-3000005")]
        [InlineData("350 N State St, Salt Lake City UT", "county-49035", "city-4967000", "school-4900870")]
        [InlineData("200 W 24th St, Cheyenne WY", "county-56021", "city-5613900", "school-5601980")]
        public async Task Get_ListsTheCapitolsCountyCityAndSchoolDistricts(
            string address,
            string county,
            string city,
            params string[] schools)
        {
            // Phoenix and Helena have a high school district over an elementary one, and
            // list both, high school first.
            var lookup = await LookupAsync(AddressQuery(address));

            Assert.Equal(
                new[] { county, city }.Concat(schools),
                Each(lookup.GetProperty("districts"), "id").Skip(3));
        }

        [Theory]
        [InlineData(SacramentoAddress, 7, 8, 6, "State Assembly")]
        [InlineData("900 Court St NE, Salem OR", 6, 11, 21, "State House")]
        [InlineData("1100 Gervais St, Columbia SC", 6, 26, 72, "State House")]
        [InlineData("416 Sid Snyder Ave SW, Olympia WA", 10, 22, 22, "State House")]
        [InlineData("1700 W Washington St, Phoenix AZ", 3, 11, 11, "State House")]
        [InlineData("700 W Jefferson St, Boise ID", 2, 19, 19, "State House")]
        [InlineData("1301 E 6th Ave, Helena MT", 2, 41, 82, "State House")]
        [InlineData("350 N State St, Salt Lake City UT", 1, 8, 22, "State House")]
        [InlineData("200 W 24th St, Cheyenne WY", 1, 8, 11, "State House")]
        public async Task Get_PlacesEachOtherCapitolInItsStatesDistricts(
            string address,
            int congressional,
            int upper,
            int lower,
            string lowerHouse)
        {
            // Pinned the same way as Raleigh; the local rows that follow are pinned above.
            var lookup = await LookupAsync(AddressQuery(address));
            var districts = lookup.GetProperty("districts");

            Assert.Equal(
                new[] { $"congressional-{congressional}", $"state-upper-{upper}", $"state-lower-{lower}" },
                Each(districts, "id").Take(3));
            Assert.Equal(
                new[] { "U.S. House", "State Senate", lowerHouse },
                Each(districts, "office").Take(3));
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
            var county = lookup.GetProperty("districts")[3];

            // A county is named, not numbered, and is no other place's part.
            Assert.Equal("county", county.GetProperty("level").GetString());
            Assert.Equal(JsonValueKind.Null, county.GetProperty("unit").ValueKind);
            Assert.Equal(JsonValueKind.Null, county.GetProperty("number").ValueKind);
            Assert.Equal(JsonValueKind.Null, county.GetProperty("area").ValueKind);
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
            Assert.Equal(6, lookup.GetProperty("districts").GetArrayLength());

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
            // The stub answers Greenville MS first and Greenville NC second, as the real
            // geocoder can for an address with no state.
            var lookup = await LookupAsync(AddressQuery("100 Main St, Greenville"));

            var location = Numbers(lookup.GetProperty("location"));

            Assert.Equal(-77.3664, location[0], 0.0001);
            Assert.Equal(35.6127, location[1], 0.0001);
            Assert.Equal(6, lookup.GetProperty("districts").GetArrayLength());
        }

        [Fact]
        public async Task Get_AsksGoogleAboutTheAddressTheGeocoderMatchedInACoveredState()
        {
            // Typed with no state, and matched in Mississippi first: Google must be
            // asked about the match the districts came from, spelled with its state.
            await LookupAsync(AddressQuery("100 Main St, Greenville"));

            Assert.Contains(
                FakeVoterInfo.Asked,
                asked => asked.Contains("100 MAIN ST, GREENVILLE, NC, 27858"));
            Assert.DoesNotContain(FakeVoterInfo.Asked, asked => asked.Contains(", MS,"));
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
            // The districts do not depend on the election, so they come back without it:
            // three legislative, then the county, the city and the school district.
            var lookup = await LookupAsync(AddressQuery(address));

            Assert.Equal(6, lookup.GetProperty("districts").GetArrayLength());
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
