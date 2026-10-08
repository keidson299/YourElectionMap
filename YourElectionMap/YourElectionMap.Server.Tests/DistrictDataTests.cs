using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// The real district file, as the server loads it. These tests are the only check
    /// that the converter's output is sane, since the converter itself needs the
    /// enacted plans' shapefiles, which are not in the repository.
    /// </summary>
    public class DistrictDataTests
    {
        // Loaded once: it is megabytes, and nothing here changes it.
        private static readonly DistrictData Data = new();

        // The State Legislative Building, 16 W Jones Street, Raleigh.
        private const double RaleighLatitude = 35.7822;
        private const double RaleighLongitude = -78.6398;

        [Fact]
        public void Loads_EveryDistrictOfTheThreeLevelsNorthCarolinaHas()
        {
            Assert.Equal(184, Data.Count);
            Assert.Equal("NCGA enacted maps", Data.Source);
        }

        [Fact]
        public void Covering_FindsOneDistrictPerLevelOverARaleighAddress()
        {
            var found = Covering(RaleighLatitude, RaleighLongitude);

            Assert.Equal(
                new[] { "congressional", "state-upper", "state-lower" },
                found.Select(district => district.Level).ToArray());
        }

        [Fact]
        public void Covering_NamesTheOfficesTheRailLists()
        {
            var found = Covering(RaleighLatitude, RaleighLongitude);

            Assert.Equal(
                new[] { "U.S. House", "State Senate", "State House" },
                found.Select(district => district.Office).ToArray());

            // The one thing the rail says about a district it has not selected.
            Assert.Equal(
                new[] { "Federal", "State", "State" },
                found.Select(district => district.Detail).ToArray());
        }

        [Fact]
        public void Covering_GivesEachDistrictAnIdMadeOfItsLevelAndNumber()
        {
            foreach (var district in Covering(RaleighLatitude, RaleighLongitude))
            {
                Assert.Equal($"{district.Level}-{district.Number}", district.Id);
                Assert.Equal("District", district.Unit);

                // The plans name no place.
                Assert.Null(district.Area);
            }
        }

        /// <summary>
        /// Each plan was drawn to equal 2020 populations: US House to the person, the
        /// legislature within the five per cent either way North Carolina allows.
        /// </summary>
        [Theory]
        [InlineData(RaleighLatitude, RaleighLongitude)]
        [InlineData(35.0873, -84.0346)] // Murphy
        [InlineData(35.9082, -75.6757)] // Manteo
        [InlineData(35.6127, -77.3664)] // Greenville
        public void Covering_GivesEachDistrictThePopulationItsPlanWasDrawnTo(
            double latitude,
            double longitude)
        {
            var found = Covering(latitude, longitude);

            Assert.Equal(3, found.Count);
            Assert.InRange(found[0].Population ?? 0, 745_670, 745_671);
            Assert.InRange(found[1].Population ?? 0, 190_000, 230_000);
            Assert.InRange(found[2].Population ?? 0, 80_000, 95_000);
        }

        [Fact]
        public void Covering_SendsBoundariesAsPolygonsOfLongitudeAndLatitude()
        {
            var found = Covering(RaleighLatitude, RaleighLongitude);

            foreach (var district in found)
            {
                Assert.NotEmpty(district.Boundary);

                foreach (var polygon in district.Boundary)
                {
                    Assert.NotEmpty(polygon);

                    foreach (var ring in polygon)
                    {
                        // Closed, and enough points to be a real boundary.
                        Assert.True(ring.Length >= 4);
                        Assert.Equal(ring[0], ring[^1]);

                        foreach (var point in ring)
                        {
                            // [longitude, latitude], somewhere in North Carolina's box.
                            Assert.Equal(2, point.Length);
                            Assert.InRange(point[0], -84.4, -75.4);
                            Assert.InRange(point[1], 33.8, 36.6);
                        }
                    }
                }

                // At the detail the file keeps rather than thinned for one view: the map
                // zooms to street level. More than a rectangle, less than a flood.
                var points = district.Boundary.Sum(polygon => polygon.Sum(ring => ring.Length));
                Assert.InRange(points, 100, 60_000);
            }
        }

        [Fact]
        public void Covering_PutsTheAddressInsideItsDistrictsOuterRing()
        {
            // The address, as the page's map will draw it, falls inside the outer ring of
            // one of the district's polygons: the rings did not come out as holes.
            foreach (var district in Covering(RaleighLatitude, RaleighLongitude))
            {
                Assert.Contains(
                    district.Boundary,
                    polygon => Geometry.Covers(
                        [polygon[0].SelectMany(point => point).ToArray()],
                        RaleighLongitude,
                        RaleighLatitude));
            }
        }

        [Fact]
        public void Covering_PutsTheBroaderDistrictAroundTheNarrowerOne()
        {
            var found = Covering(RaleighLatitude, RaleighLongitude);
            var federal = Box(found[0]);
            var house = Box(found[2]);

            Assert.True(federal[2] - federal[0] > house[2] - house[0]);
            Assert.True(federal[3] - federal[1] > house[3] - house[1]);
        }

        [Theory]
        [InlineData(38.8977, -77.0365)] // the White House
        [InlineData(32.7765, -79.9311)] // Charleston, South Carolina
        [InlineData(36.8508, -76.2859)] // Norfolk, Virginia
        [InlineData(33.5, -76.0)] // the Atlantic, east of the state
        public void Covering_FindsNothingOutsideNorthCarolina(double latitude, double longitude)
        {
            Assert.Empty(Covering(latitude, longitude));
        }

        /// <summary>
        /// The one check on the file that needs no outside authority. The three plans were
        /// enacted separately, but each is built from the same 2020 census blocks and
        /// covers the whole state exactly once, so anywhere one of them places you the
        /// other two have to as well — and never twice. A plan read wrongly, a district
        /// missing, or a conversion that put one layer off by a few feet along the coast
        /// would have to show up here.
        /// </summary>
        [Fact]
        public void Covering_HasTheThreeLevelsAgreeAcrossTheWholeState()
        {
            var inside = 0;
            var disagreed = new List<string>();

            // A grid over the state's own extent, in map units.
            for (var x = 4_300.0; x < 32_000; x += 900)
            {
                for (var y = -10_800.0; y < 0; y += 450)
                {
                    var found = Data.Covering(x, y);

                    if (found.Count == 0)
                    {
                        continue;
                    }

                    inside++;

                    var levels = found.Select(district => district.Level).ToArray();

                    if (!levels.SequenceEqual(["congressional", "state-upper", "state-lower"]))
                    {
                        disagreed.Add($"({x}, {y}): {string.Join(", ", levels)}");
                    }
                }
            }

            // Enough of the grid to have covered the state rather than clipped a corner.
            Assert.InRange(inside, 200, 1000);
            Assert.Empty(disagreed);
        }

        [Fact]
        public void Covering_ReachesTheCornersOfTheState()
        {
            // Murphy in the west and Manteo in the east: if the projection were off, or a
            // district were missing, one end of the state would come back empty.
            Assert.NotEmpty(Covering(35.0873, -84.0346));
            Assert.NotEmpty(Covering(35.9082, -75.6757));
        }

        private static List<District> Covering(double latitude, double longitude)
        {
            var (x, y) = StatePlane.ToMapUnits(latitude, longitude);

            return Data.Covering(x, y);
        }

        private static double[] Box(District district) =>
            Geometry.BoxOf(
                district.Boundary
                    .SelectMany(polygon => polygon)
                    .Select(ring => ring.SelectMany(point => point).ToArray())
                    .ToArray());
    }
}
