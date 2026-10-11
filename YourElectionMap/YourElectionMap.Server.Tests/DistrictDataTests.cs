using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// The real district files, as the server loads them. These tests are the only check
    /// that the converter's output is sane, since the converter itself needs the TIGER
    /// layers, which are not in the repository.
    /// </summary>
    public class DistrictDataTests
    {
        // Loaded once: it is megabytes, and nothing here changes it.
        private static readonly DistrictData Data = new();

        /// <summary>
        /// Each state's capitol, what its lower house is called, and the box around the
        /// state that every boundary point must fall in.
        /// </summary>
        private static readonly Dictionary<string, State> States = new()
        {
            // The State Legislative Building, 16 W Jones Street, Raleigh.
            ["NC"] = new(35.7822, -78.6398, "State House", (-84.4, -75.4), (33.8, 36.6)),
            // The State Capitol, 1315 10th Street, Sacramento.
            ["CA"] = new(38.5767, -121.4934, "State Assembly", (-124.5, -114.1), (32.5, 42.1)),
        };

        [Fact]
        public void Loads_EveryDistrictOfBothStates()
        {
            // North Carolina 14 + 50 + 120, California 52 + 40 + 80.
            Assert.Equal(["CA", "NC"], Data.States);
            Assert.Equal(184 + 172, Data.Count);
        }

        [Theory]
        [InlineData("NC")]
        [InlineData("CA")]
        public void Covering_FindsOneDistrictPerLevelAtTheCapitol(string code)
        {
            var state = States[code];
            var found = Data.Covering(state.Longitude, state.Latitude);

            Assert.NotNull(found);
            Assert.Equal("U.S. Census Bureau TIGER", found.Source);
            Assert.Equal(
                new[] { "congressional", "state-upper", "state-lower" },
                found.Districts.Select(district => district.Level).ToArray());
            Assert.Equal(
                new[] { "U.S. House", "State Senate", state.LowerHouse },
                found.Districts.Select(district => district.Office).ToArray());
            Assert.Equal(
                new[] { "Federal", "State", "State" },
                found.Districts.Select(district => district.Detail).ToArray());

            foreach (var district in found.Districts)
            {
                Assert.Equal($"{district.Level}-{district.Number}", district.Id);
                Assert.Equal("District", district.Unit);
                Assert.Null(district.Area);
            }
        }

        [Theory]
        [InlineData("NC")]
        [InlineData("CA")]
        public void Covering_SendsBoundariesAsPolygonsOfLongitudeAndLatitude(string code)
        {
            var state = States[code];
            var found = Covering(state.Latitude, state.Longitude);

            Assert.NotEmpty(found);

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
                            Assert.Equal(2, point.Length);
                            Assert.InRange(point[0], state.Longitudes.Low, state.Longitudes.High);
                            Assert.InRange(point[1], state.Latitudes.Low, state.Latitudes.High);
                        }
                    }
                }

                // At the detail the file keeps rather than thinned for one view: the map
                // zooms to street level. More than a rectangle, less than a flood.
                var points = district.Boundary.Sum(polygon => polygon.Sum(ring => ring.Length));
                Assert.InRange(points, 100, 60_000);
            }
        }

        /// <summary>
        /// The capitol, as the page's map will draw it, falls inside the outer ring of one
        /// of each district's polygons: the rings did not come out as holes.
        /// </summary>
        [Theory]
        [InlineData("NC")]
        [InlineData("CA")]
        public void Covering_PutsTheCapitolInsideItsDistrictsOuterRing(string code)
        {
            var state = States[code];

            foreach (var district in Covering(state.Latitude, state.Longitude))
            {
                Assert.Contains(
                    district.Boundary,
                    polygon => Geometry.Covers(
                        [polygon[0].SelectMany(point => point).ToArray()],
                        state.Longitude,
                        state.Latitude));
            }
        }

        /// <summary>
        /// If the conversion were off, or a district were missing, one end of a state
        /// would come back empty.
        /// </summary>
        [Theory]
        [InlineData(35.0873, -84.0346)] // Murphy, the west of North Carolina
        [InlineData(35.9082, -75.6757)] // Manteo, the east
        [InlineData(41.7558, -124.2026)] // Crescent City, the north-west of California
        [InlineData(32.7920, -115.5631)] // El Centro, the south-east
        public void Covering_ReachesTheEndsOfEachState(double latitude, double longitude)
        {
            Assert.Equal(3, Covering(latitude, longitude).Count);
        }

        [Theory]
        [InlineData(38.8977, -77.0365)] // the White House
        [InlineData(32.7765, -79.9311)] // Charleston, South Carolina
        [InlineData(36.8508, -76.2859)] // Norfolk, Virginia
        [InlineData(33.5, -76.0)] // the Atlantic, east of North Carolina
        [InlineData(39.5296, -119.8138)] // Reno, Nevada
        [InlineData(36.1699, -115.1398)] // Las Vegas, Nevada
        [InlineData(32.5149, -117.0382)] // Tijuana, Mexico
        [InlineData(36.0, -125.0)] // the Pacific, west of California
        public void Covering_FindsNothingOutsideTheStates(double latitude, double longitude)
        {
            Assert.Null(Data.Covering(longitude, latitude));
        }

        /// <summary>
        /// The one check on the file that needs no outside authority. The three plans were
        /// drawn separately, but each is built from the same 2020 census blocks and
        /// covers the whole state exactly once, so anywhere one of them places you the
        /// other two have to as well — and never twice. A plan read wrongly, a district
        /// missing, or a conversion that put one layer off by a few feet along the coast
        /// would have to show up here.
        /// </summary>
        [Fact]
        public void Covering_HasTheThreeLevelsAgreeAcrossNorthCarolina()
        {
            var inside = AssertLevelsAgree(
                (-84.35, -75.40),
                (33.80, 36.60),
                0.05);

            // Enough of the grid to have covered the state rather than clipped a corner.
            Assert.InRange(inside, 4_000, 7_000);
        }

        /// <summary>The same check as North Carolina's.</summary>
        [Fact]
        public void Covering_HasTheThreeLevelsAgreeAcrossCalifornia()
        {
            var inside = AssertLevelsAgree(
                (-124.5, -114.0),
                (32.5, 42.1),
                0.1);

            Assert.InRange(inside, 3_000, 6_000);
        }

        private static List<District> Covering(double latitude, double longitude) =>
            Data.Covering(longitude, latitude)?.Districts ?? [];

        /// <summary>
        /// Walks a grid over one state, and at every point some district covers, checks
        /// there is exactly one district per level. Returns how many points were covered.
        /// The two states' boxes do not meet, so the grid never reaches the other one.
        /// </summary>
        private static int AssertLevelsAgree(
            (double West, double East) longitudes,
            (double South, double North) latitudes,
            double step)
        {
            var inside = 0;
            var disagreed = new List<string>();

            for (var longitude = longitudes.West; longitude < longitudes.East; longitude += step)
            {
                for (var latitude = latitudes.South; latitude < latitudes.North; latitude += step)
                {
                    var found = Data.Covering(longitude, latitude);

                    if (found is null)
                    {
                        continue;
                    }

                    inside++;

                    var levels = found.Districts.Select(district => district.Level).ToArray();

                    if (!levels.SequenceEqual(["congressional", "state-upper", "state-lower"]))
                    {
                        disagreed.Add($"({longitude:0.00}, {latitude:0.00}): {string.Join(", ", levels)}");
                    }
                }
            }

            Assert.Empty(disagreed);

            return inside;
        }

        private sealed record State(
            double Latitude,
            double Longitude,
            string LowerHouse,
            (double Low, double High) Longitudes,
            (double Low, double High) Latitudes);
    }
}
