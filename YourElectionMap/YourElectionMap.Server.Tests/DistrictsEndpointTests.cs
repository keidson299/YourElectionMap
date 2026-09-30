using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// Hosts the real app in memory, so routing, model binding and the JSON on the
    /// wire are covered. The client reads these exact names and formats, so this is
    /// where the contract is pinned down.
    /// </summary>
    public class DistrictsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public DistrictsEndpointTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Get_WithAnAddress_ReturnsTheLookupTheClientReads()
        {
            var lookup = await LookupAsync(AddressQuery("1420 Maple Street, Springfield"));

            Assert.Equal("1420 Maple Street", lookup.GetProperty("label").GetString());
            Assert.Equal(new double[] { 1127, 629 }, Numbers(lookup.GetProperty("location")));
            Assert.True(lookup.GetProperty("sample").GetBoolean());

            var election = lookup.GetProperty("election");

            Assert.Equal("2026-11-03", election.GetProperty("date").GetString());
            Assert.Equal(14, election.GetProperty("candidates").GetInt32());
            Assert.Equal(2, election.GetProperty("measures").GetInt32());

            var districts = lookup.GetProperty("districts");

            // Broad to local, which is the order DistrictLookup promises and the rail
            // and the map's neighbour rule both read.
            Assert.Equal(
                new[]
                {
                    "congressional-7",
                    "state-upper-22",
                    "state-lower-44",
                    "county-3",
                    "city-2",
                    "school-5",
                },
                districts
                    .EnumerateArray()
                    .Select(district => district.GetProperty("id").GetString())
                    .ToArray());

            var house = districts
                .EnumerateArray()
                .Single(district => district.GetProperty("id").GetString() == "state-lower-44");

            Assert.Equal("state-lower", house.GetProperty("level").GetString());
            Assert.Equal("State House", house.GetProperty("office").GetString());
            Assert.Equal("District", house.GetProperty("unit").GetString());
            Assert.Equal("44", house.GetProperty("number").GetString());
            Assert.Equal("State", house.GetProperty("detail").GetString());
            Assert.Equal("Greenlake County", house.GetProperty("area").GetString());
            Assert.Equal(71400, house.GetProperty("population").GetInt32());

            var ring = house.GetProperty("boundary")[0];

            Assert.Equal(4, ring.GetArrayLength());
            Assert.Equal(new double[] { 930, 495 }, Numbers(ring[0]));
        }

        [Fact]
        public async Task Get_SendsNullRatherThanOmittingWhatIsNotKnown()
        {
            var lookup = await LookupAsync(AddressQuery("1420 Maple Street"));

            var federal = lookup
                .GetProperty("districts")
                .EnumerateArray()
                .Single(district => district.GetProperty("id").GetString() == "congressional-7");

            Assert.Equal(JsonValueKind.Null, federal.GetProperty("area").ValueKind);
        }

        [Theory]
        [InlineData("1420 Maple Street, Springfield", "1420 Maple Street")]
        [InlineData("88 Harbor Road", "88 Harbor Road")]
        public async Task Get_NamesAnAddressByItsStreetLine(string address, string label)
        {
            var lookup = await LookupAsync(AddressQuery(address));

            Assert.Equal(label, lookup.GetProperty("label").GetString());
        }

        [Fact]
        public async Task Get_NamesALocatedLookupPlainly()
        {
            var lookup = await LookupAsync("lat=39.8&lon=-89.65");

            Assert.Equal("Your location", lookup.GetProperty("label").GetString());
        }

        [Fact]
        public async Task Get_PrefersTheAddressWhenBothAreGiven()
        {
            var lookup = await LookupAsync(
                $"{AddressQuery("1420 Maple Street")}&lat=39.8&lon=-89.65");

            Assert.Equal("1420 Maple Street", lookup.GetProperty("label").GetString());
        }

        [Theory]
        [InlineData("address=Main%20Street")] // no house number for the stub to place
        [InlineData("lat=30&lon=-30")] // the middle of the Atlantic
        public async Task Get_Returns404WhenThereIsNothingToShow(string query)
        {
            var response = await GetAsync(query);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
            var response = await GetAsync(AddressQuery("1420 Maple Street"));

            Assert.True(response.Headers.CacheControl?.NoStore);
        }

        private Task<HttpResponseMessage> GetAsync(string query) =>
            _factory.CreateClient().GetAsync($"/api/districts?{query}");

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

        private static double[] Numbers(JsonElement array) =>
            array.EnumerateArray().Select(number => number.GetDouble()).ToArray();
    }
}
