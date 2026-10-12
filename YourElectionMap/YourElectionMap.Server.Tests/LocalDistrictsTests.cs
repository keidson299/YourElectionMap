using System.Globalization;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// The county, city and school rows, from a small file written here: the real files
    /// gain them only once the converter is run with those layers. Squares nested the
    /// way the real ones are: a city inside its county, and a California-style
    /// elementary district inside the high school district that covers it.
    /// </summary>
    public sealed class LocalDistrictsTests : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(), $"local-{Guid.NewGuid():N}-districts.json");

        private readonly DistrictData _data;

        public LocalDistrictsTests()
        {
            File.WriteAllText(_path, $$"""
                {
                  "source": "U.S. Census Bureau TIGER",
                  "state": "ZZ",
                  "units": "degrees",
                  "districts": [
                    {{Square("school-0600002", "school", "Lakeside Elementary", "", "Elementary school district", 0.2, 0.8)}},
                    {{Square("city-0600100", "city", "Springfield city", "", "City or town", 0.1, 0.5)}},
                    {{Square("school-0600001", "school", "Valley Union High", "", "High school district", 0, 1)}},
                    {{Square("county-06001", "county", "Greenlake County", "", "County", 0, 1)}},
                    {{Square("state-lower-4", "state-lower", "State Assembly", "4", "State", 0, 1)}}
                  ]
                }
                """);

            _data = new DistrictData(_path);
        }

        public void Dispose() => File.Delete(_path);

        [Fact]
        public void Covering_ListsTheLocalRowsBroadToLocal()
        {
            var found = _data.Covering(0.3, 0.3);

            Assert.NotNull(found);
            Assert.Equal(
                ["state-lower-4", "county-06001", "city-0600100", "school-0600001", "school-0600002"],
                found.Districts.Select(district => district.Id));
        }

        [Fact]
        public void Covering_GivesANamedRowNoUnitOrNumber()
        {
            var county = _data.Covering(0.3, 0.3)!.Districts.Single(district => district.Level == "county");

            Assert.Equal("Greenlake County", county.Office);
            Assert.Equal("County", county.Detail);
            Assert.Null(county.Unit);
            Assert.Null(county.Number);
        }

        [Fact]
        public void Covering_NamesTheCountyOnEveryOtherRow()
        {
            var found = _data.Covering(0.3, 0.3)!;

            Assert.All(
                found.Districts.Where(district => district.Level != "county"),
                district => Assert.Equal("Greenlake County", district.Area));
            Assert.Null(found.Districts.Single(district => district.Level == "county").Area);
        }

        [Fact]
        public void Covering_HasNoCityRowOutsideEveryCity()
        {
            // Unincorporated county: no city row, rather than a made-up one.
            var found = _data.Covering(0.9, 0.9)!;

            Assert.Equal(
                ["state-lower", "county", "school"],
                found.Districts.Select(district => district.Level));
        }

        /// <summary>A square from (low, low) to (high, high), in the file's own shape.</summary>
        private static string Square(
            string id, string level, string office, string number, string detail, double low, double high)
        {
            string N(double value) => value.ToString(CultureInfo.InvariantCulture);

            return $$"""
                {"id": "{{id}}", "level": "{{level}}", "office": "{{office}}", "number": "{{number}}", "detail": "{{detail}}", "bbox": [{{N(low)}},{{N(low)}},{{N(high)}},{{N(high)}}], "rings": [[{{N(low)}},{{N(low)}},{{N(high)}},{{N(low)}},{{N(high)}},{{N(high)}},{{N(low)}},{{N(high)}},{{N(low)}},{{N(low)}}]]}
                """;
        }
    }
}
