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
        /// Each state's file loaded on its own, for the grid checks: a state's neighbours
        /// sit inside its box, and must neither be counted as it nor overlap it.
        /// </summary>
        private static readonly Dictionary<string, DistrictData> Alone = Directory
            .GetFiles(Path.Combine(AppContext.BaseDirectory, "Districts", "Data"), "*-districts.json")
            .Select(path => new DistrictData(path))
            .ToDictionary(data => data.States[0]);

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
            // The State Capitol, 900 Court Street NE, Salem.
            ["OR"] = new(44.9385, -123.0301, "State House", (-124.75, -116.4), (41.95, 46.35)),
            // The State House, 1100 Gervais Street, Columbia.
            ["SC"] = new(34.0004, -81.0330, "State House", (-83.4, -78.45), (31.95, 35.25)),
            // The Legislative Building, 416 Sid Snyder Avenue SW, Olympia.
            ["WA"] = new(47.0358, -122.9048, "State House", (-124.9, -116.9), (45.5, 49.1)),
            // The rest have straight borders along round degrees, so their boxes start a few
            // hundredths off them: a grid row on 37°N would be on the Utah–Arizona line.
            // The State Capitol, 1700 W Washington Street, Phoenix.
            ["AZ"] = new(33.4481, -112.0970, "State House", (-114.83, -109.0), (31.31, 37.05)),
            // The State Capitol, 700 W Jefferson Street, Boise.
            ["ID"] = new(43.6177, -116.1996, "State House", (-117.23, -111.0), (41.97, 49.05)),
            // The State Capitol, 1301 E 6th Avenue, Helena.
            ["MT"] = new(46.5857, -112.0184, "State House", (-116.07, -104.0), (44.37, 49.05)),
            // The State Capitol, 350 N State Street, Salt Lake City.
            ["UT"] = new(40.7774, -111.8882, "State House", (-114.07, -109.0), (36.97, 42.05)),
            // The State Capitol, 200 W 24th Street, Cheyenne.
            ["WY"] = new(41.1403, -104.8202, "State House", (-111.07, -104.0), (40.97, 45.05)),
        };

        /// <summary>
        /// The states whose files have the county, city and school layers, with the FIPS
        /// code that begins each of those rows' Census code. The rest have the three
        /// legislative levels only, until their local layers are fetched.
        /// </summary>
        private static readonly Dictionary<string, string> WithLocalLevels = new()
        {
            ["CA"] = "06",
            ["NC"] = "37",
        };

        private static readonly string[] Legislative = ["congressional", "state-upper", "state-lower"];

        [Fact]
        public void Loads_EveryDistrictOfEveryState()
        {
            // North Carolina 14 + 50 + 120, California 52 + 40 + 80, Oregon 6 + 30 + 60,
            // South Carolina 7 + 46 + 124, Washington 10 + 49 + 49, Arizona 9 + 30 + 30,
            // Idaho 2 + 35 + 35, Montana 2 + 50 + 100, Utah 4 + 29 + 75, and Wyoming
            // 1 + 31 + 62. Washington, Arizona and Idaho elect their House two to a
            // Senate district.
            //
            // Then the local rows: North Carolina's 100 counties, 552 cities and towns and
            // 118 school districts, all unified; California's 58 counties, 483 cities and
            // towns, and 345 unified, 517 elementary and 112 high school districts.
            Assert.Equal(["AZ", "CA", "ID", "MT", "NC", "OR", "SC", "UT", "WA", "WY"], Data.States);
            Assert.Equal(
                184 + 172 + 96 + 177 + 108 + 69 + 72 + 152 + 108 + 94
                    + (100 + 552 + 118) + (58 + 483 + 345 + 517 + 112),
                Data.Count);
        }

        [Theory]
        [InlineData("NC")]
        [InlineData("CA")]
        [InlineData("OR")]
        [InlineData("SC")]
        [InlineData("WA")]
        [InlineData("AZ")]
        [InlineData("ID")]
        [InlineData("MT")]
        [InlineData("UT")]
        [InlineData("WY")]
        public void Covering_FindsOneDistrictPerLevelAtTheCapitol(string code)
        {
            var state = States[code];
            var found = Data.Covering(state.Longitude, state.Latitude);

            Assert.NotNull(found);
            Assert.Equal("U.S. Census Bureau TIGER", found.Source);

            var legislative = found.Districts.Take(3).ToArray();
            var local = found.Districts.Skip(3).ToArray();

            Assert.Equal(Legislative, legislative.Select(district => district.Level));
            Assert.Equal(
                new[] { "U.S. House", "State Senate", state.LowerHouse },
                legislative.Select(district => district.Office));
            Assert.Equal(
                new[] { "Federal", "State", "State" },
                legislative.Select(district => district.Detail));

            // The capitol is in a city and a unified school district, so it has one row
            // per local level, the county first.
            Assert.Equal(
                WithLocalLevels.ContainsKey(code) ? ["county", "city", "school"] : [],
                local.Select(district => district.Level));

            var county = local.FirstOrDefault()?.Office;

            foreach (var district in legislative)
            {
                Assert.Equal($"{district.Level}-{district.Number}", district.Id);
                Assert.Equal("District", district.Unit);
                Assert.Equal(county, district.Area);
            }

            foreach (var district in local)
            {
                Assert.StartsWith($"{district.Level}-{WithLocalLevels[code]}", district.Id);
                Assert.Null(district.Unit);
                Assert.Null(district.Number);
            }
        }

        [Theory]
        [InlineData("NC")]
        [InlineData("CA")]
        [InlineData("OR")]
        [InlineData("SC")]
        [InlineData("WA")]
        [InlineData("AZ")]
        [InlineData("ID")]
        [InlineData("MT")]
        [InlineData("UT")]
        [InlineData("WY")]
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
                // zooms to street level. More than a rectangle, less than a flood: a town
                // district drawn along a street grid, like Cheyenne's, can be under a hundred.
                var points = district.Boundary.Sum(polygon => polygon.Sum(ring => ring.Length));
                Assert.InRange(points, 20, 60_000);
            }
        }

        /// <summary>
        /// The capitol, as the page's map will draw it, falls inside the outer ring of one
        /// of each district's polygons: the rings did not come out as holes.
        /// </summary>
        [Theory]
        [InlineData("NC")]
        [InlineData("CA")]
        [InlineData("OR")]
        [InlineData("SC")]
        [InlineData("WA")]
        [InlineData("AZ")]
        [InlineData("ID")]
        [InlineData("MT")]
        [InlineData("UT")]
        [InlineData("WY")]
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
        [InlineData(42.0526, -124.2840)] // Brookings, the south-west of Oregon
        [InlineData(44.0266, -116.9629)] // Ontario, the east
        [InlineData(34.7648, -83.0640)] // Walhalla, the west of South Carolina
        [InlineData(33.8730, -78.6142)] // Little River, the north-east
        [InlineData(48.3681, -124.6250)] // Neah Bay, the north-west of Washington
        [InlineData(46.4163, -117.0457)] // Clarkston, the south-east
        [InlineData(32.6927, -114.6277)] // Yuma, the south-west of Arizona
        [InlineData(36.9147, -111.4558)] // Page, the north
        [InlineData(48.6913, -116.3163)] // Bonners Ferry, the north of Idaho
        [InlineData(42.3221, -111.2980)] // Montpelier, the south-east
        [InlineData(46.4165, -117.0177)] // Lewiston, across the river from Clarkston
        [InlineData(48.3883, -115.5560)] // Libby, the north-west of Montana
        [InlineData(47.7167, -104.1563)] // Sidney, the east
        [InlineData(37.0965, -113.5684)] // St. George, the south-west of Utah
        [InlineData(40.4555, -109.5287)] // Vernal, the east
        [InlineData(43.4799, -110.7624)] // Jackson, the west of Wyoming
        [InlineData(42.0625, -104.1844)] // Torrington, the east
        public void Covering_ReachesTheEndsOfEachState(double latitude, double longitude)
        {
            var found = Covering(latitude, longitude);

            Assert.NotEmpty(found);
            Assert.Null(Disagreement(found, local: found.Any(district => district.Level == "county")));
        }

        [Theory]
        [InlineData(38.8977, -77.0365)] // the White House
        [InlineData(33.4735, -82.0105)] // Augusta, Georgia
        [InlineData(32.0809, -81.0912)] // Savannah, Georgia
        [InlineData(36.8508, -76.2859)] // Norfolk, Virginia
        [InlineData(33.5, -76.0)] // the Atlantic, east of North Carolina
        [InlineData(39.5296, -119.8138)] // Reno, Nevada
        [InlineData(36.1699, -115.1398)] // Las Vegas, Nevada
        [InlineData(32.5149, -117.0382)] // Tijuana, Mexico
        [InlineData(36.0, -125.0)] // the Pacific, west of California
        [InlineData(44.0, -125.5)] // the Pacific, west of Oregon
        [InlineData(49.2827, -123.1207)] // Vancouver, British Columbia
        [InlineData(35.0844, -106.6504)] // Albuquerque, New Mexico
        [InlineData(31.3086, -110.9422)] // Nogales, Sonora
        [InlineData(40.7391, -114.0733)] // West Wendover, Nevada, across the line from Utah
        [InlineData(39.0639, -108.5506)] // Grand Junction, Colorado
        [InlineData(40.5853, -105.0844)] // Fort Collins, Colorado
        [InlineData(44.0805, -103.2310)] // Rapid City, South Dakota
        [InlineData(48.1470, -103.6180)] // Williston, North Dakota
        [InlineData(49.6956, -112.8451)] // Lethbridge, Alberta
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
        /// would have to show up here. So would two states' files overlapping, where a
        /// border runs through the grid. The counties are built from the same blocks, so
        /// they hold to the same rule; cities and school districts do as far as
        /// <see cref="Disagreement"/> says.
        /// </summary>
        [Theory]
        [InlineData("NC", 0.05, 4_000, 7_000)]
        [InlineData("CA", 0.1, 3_000, 6_000)]
        [InlineData("OR", 0.05, 9_000, 14_000)]
        [InlineData("SC", 0.05, 2_500, 4_500)]
        [InlineData("WA", 0.05, 7_000, 11_000)]
        [InlineData("AZ", 0.05, 9_500, 13_500)]
        [InlineData("ID", 0.05, 8_000, 11_500)]
        [InlineData("MT", 0.05, 15_000, 21_000)]
        [InlineData("UT", 0.05, 7_500, 11_000)]
        [InlineData("WY", 0.05, 9_500, 13_000)]
        public void Covering_HasTheLevelsAgreeAcrossTheState(
            string code,
            double step,
            int fewest,
            int most)
        {
            var inside = AssertLevelsAgree(code, step);

            // Enough of the grid to have covered the state rather than clipped a corner.
            Assert.InRange(inside, fewest, most);
        }

        private static List<District> Covering(double latitude, double longitude) =>
            Data.Covering(longitude, latitude)?.Districts ?? [];

        /// <summary>
        /// Walks a grid over one state's box, and at every point the state covers, checks
        /// there is exactly one district per level and that no other state covers it too.
        /// Returns how many points the state covered.
        /// </summary>
        private static int AssertLevelsAgree(string code, double step)
        {
            var state = States[code];
            var inside = 0;
            var disagreed = new List<string>();

            for (var longitude = state.Longitudes.Low; longitude < state.Longitudes.High; longitude += step)
            {
                for (var latitude = state.Latitudes.Low; latitude < state.Latitudes.High; latitude += step)
                {
                    var found = Alone[code].Covering(longitude, latitude);

                    if (found is null)
                    {
                        continue;
                    }

                    inside++;

                    if (Disagreement(found.Districts, WithLocalLevels.ContainsKey(code)) is { } wrong)
                    {
                        var ids = found.Districts.Select(district => district.Id);
                        disagreed.Add($"({longitude:0.00}, {latitude:0.00}): {wrong}: {string.Join(", ", ids)}");
                    }

                    foreach (var (other, data) in Alone)
                    {
                        if (other != code && data.Covering(longitude, latitude) is not null)
                        {
                            disagreed.Add($"({longitude:0.00}, {latitude:0.00}): also in {other}");
                        }
                    }
                }
            }

            Assert.Empty(disagreed);

            return inside;
        }

        /// <summary>
        /// What is wrong with the districts covering one point, or null. One district per
        /// legislative level, first; and where the file has the local levels, one county, at
        /// most one city (unincorporated land has none), and either a unified school
        /// district, a high school district and the elementary one inside it, one of those
        /// two alone, or none, where TIGER defines no district.
        /// </summary>
        private static string? Disagreement(List<District> found, bool local)
        {
            var levels = found.Select(district => district.Level).ToArray();

            if (!levels.Take(3).SequenceEqual(Legislative))
            {
                return "not one district per legislative level";
            }

            if (!local)
            {
                return levels.Length == 3 ? null : "local rows in a state without them";
            }

            if (levels.Count(level => level == "county") != 1 || levels.Count(level => level == "city") > 1)
            {
                return "not one county and at most one city";
            }

            var schools = found
                .Where(district => district.Level == "school")
                .Select(district => district.Detail)
                .ToArray();

            return schools is []
                or ["School district"]
                or ["High school district"]
                or ["Elementary school district"]
                or ["High school district", "Elementary school district"]
                ? null
                : "school districts that overlap";
        }

        private sealed record State(
            double Latitude,
            double Longitude,
            string LowerHouse,
            (double Low, double High) Longitudes,
            (double Low, double High) Latitudes);
    }
}
