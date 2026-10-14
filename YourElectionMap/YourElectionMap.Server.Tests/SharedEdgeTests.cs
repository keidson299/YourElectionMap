using System.Globalization;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// What a point gets beside an edge two districts share, where thinning each on its
    /// own has left them overlapping or apart by a few feet. On a small file written
    /// here, at the equator, where a hundred-thousandth of a degree is about 3.6 feet:
    /// the State Houses overlap by that much at x = 0.5, the State Senates leave a gap
    /// that wide, and the counties a gap of miles.
    /// </summary>
    public sealed class SharedEdgeTests : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(), $"edges-{Guid.NewGuid():N}-districts.json");

        private readonly DistrictData _data;

        public SharedEdgeTests()
        {
            File.WriteAllText(_path, $$"""
                {
                  "source": "U.S. Census Bureau TIGER",
                  "state": "ZZ",
                  "units": "degrees",
                  "districts": [
                    {{Box("congressional-1", "congressional", "U.S. House", "1", "Federal", 0, 1)}},
                    {{Box("state-upper-1", "state-upper", "State Senate", "1", "State", 0, 0.49999)}},
                    {{Box("state-upper-2", "state-upper", "State Senate", "2", "State", 0.5, 1)}},
                    {{Box("state-lower-1", "state-lower", "State House", "1", "State", 0, 0.50001)}},
                    {{Box("state-lower-2", "state-lower", "State House", "2", "State", 0.5, 1)}},
                    {{Box("county-99001", "county", "West County", "", "County", 0, 0.4)}},
                    {{Box("county-99002", "county", "East County", "", "County", 0.6, 1)}},
                    {{Box("city-9900100", "city", "Westville city", "", "City or town", 0.1, 0.50001)}},
                    {{Box("city-9900200", "city", "Eastville city", "", "City or town", 0.5, 0.9)}}
                  ]
                }
                """);

            _data = new DistrictData(_path);
        }

        public void Dispose() => File.Delete(_path);

        [Fact]
        public void Covering_KeepsTheDistrictAPointIsDeeperInWhereTwoOverlap()
        {
            // 2.9 feet into State House 2, and 0.7 from the edge of State House 1's overlap.
            var found = Ids(0.500008);

            Assert.Contains("state-lower-2", found);
            Assert.DoesNotContain("state-lower-1", found);

            // Cities overlap the same way, and settle the same way.
            Assert.Contains("city-9900200", found);
            Assert.DoesNotContain("city-9900100", found);
        }

        [Fact]
        public void Covering_FillsAGapOfAFewFeetWithTheNearerDistrict()
        {
            // 1.1 feet east of State Senate 1, 2.5 feet west of State Senate 2.
            var found = Ids(0.499993);

            Assert.Contains("state-upper-1", found);
            Assert.DoesNotContain("state-upper-2", found);
        }

        [Fact]
        public void Covering_LeavesAGapOfMilesEmpty()
        {
            // Between the counties: nothing within fifty feet, so no county is made up.
            Assert.DoesNotContain(Ids(0.45), id => id.StartsWith("county-", StringComparison.Ordinal));
        }

        [Fact]
        public void Covering_NeverFillsInACity()
        {
            // 1.8 feet outside Eastville: unincorporated, however close.
            Assert.DoesNotContain(Ids(0.900005), id => id.StartsWith("city-", StringComparison.Ordinal));
        }

        [Fact]
        public void Covering_StillListsBroadToLocal()
        {
            Assert.Equal(
                ["congressional-1", "state-upper-1", "state-lower-1", "city-9900100"],
                Ids(0.499993).Where(id => !id.StartsWith("county-", StringComparison.Ordinal)));
        }

        private string[] Ids(double longitude) =>
            _data.Covering(longitude, 0.5)!.Districts.Select(district => district.Id).ToArray();

        /// <summary>A box from <paramref name="west"/> to <paramref name="east"/>, and 0 to 1 north.</summary>
        private static string Box(
            string id, string level, string office, string number, string detail, double west, double east)
        {
            string N(double value) => value.ToString(CultureInfo.InvariantCulture);

            return $$"""
                {"id": "{{id}}", "level": "{{level}}", "office": "{{office}}", "number": "{{number}}", "detail": "{{detail}}", "bbox": [{{N(west)}},0,{{N(east)}},1], "rings": [[{{N(west)}},0,{{N(east)}},0,{{N(east)}},1,{{N(west)}},1,{{N(west)}},0]]}
                """;
        }
    }
}
