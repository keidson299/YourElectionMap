using System.Text.Json;
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
        /// Each state's FIPS code, which begins its local rows' Census codes, its capitol,
        /// what its lower house is called (Nebraska has none), and the box around the state
        /// that every boundary point must fall in.
        /// </summary>
        private static readonly Dictionary<string, State> States = new()
        {
            // The State Legislative Building, 16 W Jones Street, Raleigh.
            ["NC"] = new("37", 35.7822, -78.6398, "State House", (-84.4, -75.4), (33.8, 36.6)),
            // The State Capitol, 1315 10th Street, Sacramento.
            ["CA"] = new("06", 38.5767, -121.4934, "State Assembly", (-124.5, -114.1), (32.5, 42.1)),
            // The State Capitol, 900 Court Street NE, Salem.
            ["OR"] = new("41", 44.9385, -123.0301, "State House", (-124.75, -116.4), (41.95, 46.35)),
            // The State House, 1100 Gervais Street, Columbia.
            ["SC"] = new("45", 34.0004, -81.0330, "State House", (-83.4, -78.45), (31.95, 35.25)),
            // The rest have straight borders along round degrees, so their boxes start a few
            // hundredths off them: a grid row on 37°N would be on the Utah–Arizona line, and
            // one on 49°N finds a Washington school district on the Canadian line itself.
            // The Legislative Building, 416 Sid Snyder Avenue SW, Olympia.
            ["WA"] = new("53", 47.0358, -122.9048, "State House", (-124.9, -116.9), (45.52, 49.1)),
            // The State Capitol, 1700 W Washington Street, Phoenix.
            ["AZ"] = new("04", 33.4481, -112.0970, "State House", (-114.83, -109.0), (31.31, 37.05)),
            // The State Capitol, 700 W Jefferson Street, Boise.
            ["ID"] = new("16", 43.6177, -116.1996, "State House", (-117.23, -111.0), (41.97, 49.05)),
            // The State Capitol, 1301 E 6th Avenue, Helena.
            ["MT"] = new("30", 46.5857, -112.0184, "State House", (-116.07, -104.0), (44.37, 49.05)),
            // The State Capitol, 350 N State Street, Salt Lake City.
            ["UT"] = new("49", 40.7774, -111.8882, "State House", (-114.07, -109.0), (36.97, 42.05)),
            // The State Capitol, 200 W 24th Street, Cheyenne.
            ["WY"] = new("56", 41.1403, -104.8202, "State House", (-111.07, -104.0), (40.97, 45.05)),
            // The State Capitol, 200 E Colfax Avenue, Denver.
            ["CO"] = new("08", 39.7393, -104.9848, "State House", (-109.07, -102.02), (36.97, 41.05)),
            // The State Capitol, 490 Old Santa Fe Trail, Santa Fe.
            ["NM"] = new("35", 35.6824, -105.9397, "State House", (-109.07, -102.97), (31.31, 37.02)),
            // The State Capitol, 101 N Carson Street, Carson City.
            ["NV"] = new("32", 39.1640, -119.7663, "State Assembly", (-120.07, -114.0), (34.97, 42.05)),
            // The State Capitol, 2300 N Lincoln Boulevard, Oklahoma City.
            ["OK"] = new("40", 35.4922, -97.5034, "State House", (-103.07, -94.4), (33.57, 37.05)),
            // The State Capitol, 1100 Congress Avenue, Austin.
            ["TX"] = new("48", 30.2747, -97.7403, "State House", (-106.67, -93.47), (25.82, 36.52)),
            // The State Capitol, 300 SW 10th Avenue, Topeka.
            ["KS"] = new("20", 39.0481, -95.6780, "State House", (-102.07, -94.55), (36.97, 40.05)),
            // The State Capitol, 75 Rev Dr Martin Luther King Jr Boulevard, St. Paul.
            ["MN"] = new("27", 44.9551, -93.1022, "State House", (-97.27, -89.45), (43.47, 49.42)),
            // The State Capitol, 600 E Boulevard Avenue, Bismarck.
            ["ND"] = new("38", 46.8209, -100.7827, "State House", (-104.07, -96.5), (45.92, 49.05)),
            // The State Capitol, 1445 K Street, Lincoln. One chamber, the Legislature.
            ["NE"] = new("31", 40.8081, -96.6997, null, (-104.07, -95.27), (39.97, 43.05), UpperHouse: "Legislature"),
            // The State Capitol, 500 E Capitol Avenue, Pierre.
            ["SD"] = new("46", 44.3671, -100.3462, "State House", (-104.07, -96.4), (42.47, 45.97)),
            // The State Capitol, 500 Woodlane Street, Little Rock.
            ["AR"] = new("05", 34.7466, -92.2890, "State House", (-94.62, -89.64), (32.97, 36.52)),
            // The State Capitol, 1007 E Grand Avenue, Des Moines.
            ["IA"] = new("19", 41.5912, -93.6038, "State House", (-96.64, -90.14), (40.37, 43.51)),
            // The State Capitol, 900 N 3rd Street, Baton Rouge.
            ["LA"] = new("22", 30.4571, -91.1874, "State House", (-94.05, -88.75), (28.87, 33.03)),
            // The State Capitol, 201 W Capitol Avenue, Jefferson City.
            ["MO"] = new("29", 38.5792, -92.1729, "State House", (-95.78, -89.09), (35.97, 40.62)),
            // The State Capitol, 400 High Street, Jackson.
            ["MS"] = new("28", 32.3036, -90.1823, "State House", (-91.66, -88.07), (30.12, 35.02)),
        };

        /// <summary>The levels a state elects its legislators by, broad to local.</summary>
        private static string[] Legislative(State state) =>
            state.LowerHouse is null
                ? ["congressional", "state-upper"]
                : ["congressional", "state-upper", "state-lower"];

        [Fact]
        public void Loads_EveryDistrictOfEveryState()
        {
            // Per state, in the order of States above: its US House, State Senate and
            // State House districts together (the counts are in Tiger.cs, a halved
            // district counting twice), then its
            // counties, cities and towns, and unified school districts, and where it has
            // them its elementary and then high school districts.
            Assert.Equal(
                [
                    "AR", "AZ", "CA", "CO", "IA", "ID", "KS", "LA", "MN", "MO", "MS", "MT", "NC",
                    "ND", "NE", "NM", "NV", "OK", "OR", "SC", "SD", "TX", "UT", "WA", "WY",
                ],
                Data.States);
            Assert.Equal(
                184 + 172 + 96 + 177 + 108 + 69 + 72 + 152 + 108 + 94
                    + 108 + 115 + 67 + 154 + 219
                    + 169 + 209 + 96 + 52 + 73
                    + 139 + 154 + 150 + 205 + 178
                    + (100 + 552 + 118) + (58 + 483 + 345 + 517 + 112)
                    + (36 + 241 + 187 + 9 + 1) + (46 + 271 + 72) + (39 + 281 + 295)
                    + (15 + 92 + 97 + 102 + 15) + (44 + 199 + 116)
                    + (56 + 127 + 65 + 233 + 97) + (29 + 255 + 41) + (23 + 99 + 47 + 1)
                    + (64 + 273 + 178) + (33 + 105 + 89) + (17 + 19 + 17)
                    + (77 + 600 + 415 + 91) + (254 + 1230 + 1016 + 1 + 1)
                    + (105 + 626 + 285) + (87 + 856 + 321 + 8 + 1) + (53 + 355 + 147 + 20)
                    + (93 + 528 + 244) + (66 + 310 + 147)
                    + (75 + 501 + 234) + (99 + 939 + 325) + (64 + 305 + 69)
                    + (115 + 947 + 445 + 71) + (82 + 300 + 137),
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
        [InlineData("CO")]
        [InlineData("NM")]
        [InlineData("NV")]
        [InlineData("OK")]
        [InlineData("TX")]
        [InlineData("KS")]
        [InlineData("MN")]
        [InlineData("ND")]
        [InlineData("NE")]
        [InlineData("SD")]
        [InlineData("AR")]
        [InlineData("IA")]
        [InlineData("LA")]
        [InlineData("MO")]
        [InlineData("MS")]
        public void Covering_FindsOneDistrictPerLevelAtTheCapitol(string code)
        {
            var state = States[code];
            var found = Data.Covering(state.Longitude, state.Latitude);

            Assert.NotNull(found);
            Assert.Equal("U.S. Census Bureau TIGER", found.Source);

            var levels = Legislative(state);
            var legislative = found.Districts.Take(levels.Length).ToArray();
            var local = found.Districts.Skip(levels.Length).ToArray();

            Assert.Equal(levels, legislative.Select(district => district.Level));
            Assert.Equal(
                new[] { "U.S. House", state.UpperHouse, state.LowerHouse }.Take(levels.Length),
                legislative.Select(district => district.Office));
            Assert.Equal(
                new[] { "Federal", "State", "State" }.Take(levels.Length),
                legislative.Select(district => district.Detail));

            // The capitol is in a city, so it has a county and a city row, then its school
            // district, or a high school and an elementary district.
            Assert.Equal(["county", "city"], local.Take(2).Select(district => district.Level));
            Assert.NotEmpty(local.Skip(2));
            Assert.All(local.Skip(2), district => Assert.Equal("school", district.Level));
            Assert.Null(Disagreement(found.Districts, state.Longitude, state.Latitude));

            var county = local[0].Office;

            foreach (var district in legislative)
            {
                Assert.Equal($"{district.Level}-{district.Number}", district.Id);
                Assert.Equal("District", district.Unit);
                Assert.Equal(county, district.Area);
            }

            foreach (var district in local)
            {
                Assert.StartsWith($"{district.Level}-{state.Fips}", district.Id);
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
        [InlineData("CO")]
        [InlineData("NM")]
        [InlineData("NV")]
        [InlineData("OK")]
        [InlineData("TX")]
        [InlineData("KS")]
        [InlineData("MN")]
        [InlineData("ND")]
        [InlineData("NE")]
        [InlineData("SD")]
        [InlineData("AR")]
        [InlineData("IA")]
        [InlineData("LA")]
        [InlineData("MO")]
        [InlineData("MS")]
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
        [InlineData("CO")]
        [InlineData("NM")]
        [InlineData("NV")]
        [InlineData("OK")]
        [InlineData("TX")]
        [InlineData("KS")]
        [InlineData("MN")]
        [InlineData("ND")]
        [InlineData("NE")]
        [InlineData("SD")]
        [InlineData("AR")]
        [InlineData("IA")]
        [InlineData("LA")]
        [InlineData("MO")]
        [InlineData("MS")]
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
        [InlineData(39.0639, -108.5506)] // Grand Junction, the west of Colorado
        [InlineData(38.0872, -102.6208)] // Lamar, the south-east
        [InlineData(36.7281, -108.2187)] // Farmington, the north-west of New Mexico
        [InlineData(32.7026, -103.1360)] // Hobbs, the south-east
        [InlineData(35.1677, -114.5730)] // Laughlin, the south of Nevada
        [InlineData(41.9832, -114.6745)] // Jackpot, the north-east
        [InlineData(36.7292, -102.5132)] // Boise City, the Oklahoma panhandle
        [InlineData(33.8957, -94.8263)] // Idabel, the south-east
        [InlineData(31.7619, -106.4850)] // El Paso, the west of Texas
        [InlineData(25.9017, -97.4975)] // Brownsville, the south
        [InlineData(35.2220, -101.8313)] // Amarillo, the panhandle
        [InlineData(37.9717, -100.8727)] // Garden City, the west of Kansas
        [InlineData(39.1141, -94.6275)] // Kansas City, the east
        [InlineData(46.8738, -96.7678)] // Moorhead, the west of Minnesota
        [InlineData(46.7867, -92.1005)] // Duluth, the east
        [InlineData(49.3456, -95.0647)] // Angle Inlet, on the Northwest Angle, across the Lake of the Woods
        [InlineData(48.1470, -103.6180)] // Williston, the west of North Dakota
        [InlineData(46.8772, -96.7898)] // Fargo, the east
        [InlineData(41.8666, -103.6672)] // Scottsbluff, the west of Nebraska
        [InlineData(41.2565, -95.9345)] // Omaha, the east
        [InlineData(44.0805, -103.2310)] // Rapid City, the west of South Dakota
        [InlineData(43.5446, -96.7311)] // Sioux Falls, the east
        [InlineData(35.3859, -94.3985)] // Fort Smith, the west of Arkansas
        [InlineData(35.1465, -90.1845)] // West Memphis, the east
        [InlineData(41.2619, -95.8608)] // Council Bluffs, the west of Iowa, across the river from Omaha
        [InlineData(41.5236, -90.5776)] // Davenport, the east
        [InlineData(32.5252, -93.7502)] // Shreveport, the north-west of Louisiana
        [InlineData(29.2766, -89.3542)] // Venice, near the mouth of the Mississippi
        [InlineData(39.0997, -94.5786)] // Kansas City, the west of Missouri
        [InlineData(38.6270, -90.1994)] // St. Louis, the east, a city outside any county
        [InlineData(36.1931, -89.6556)] // Caruthersville, the Bootheel
        [InlineData(34.9890, -90.0126)] // Southaven, the north of Mississippi
        [InlineData(30.3960, -88.8853)] // Biloxi, the coast
        public void Covering_ReachesTheEndsOfEachState(double latitude, double longitude)
        {
            var found = Covering(latitude, longitude);

            Assert.NotEmpty(found);
            Assert.Null(Disagreement(found, longitude, latitude));
        }

        [Theory]
        [InlineData(38.8977, -77.0365)] // the White House
        [InlineData(33.4735, -82.0105)] // Augusta, Georgia
        [InlineData(32.0809, -81.0912)] // Savannah, Georgia
        [InlineData(36.8508, -76.2859)] // Norfolk, Virginia
        [InlineData(33.5, -76.0)] // the Atlantic, east of North Carolina
        [InlineData(41.5067, -90.5151)] // Moline, Illinois, across the river from Davenport
        [InlineData(38.6245, -90.1510)] // East St. Louis, Illinois
        [InlineData(35.1495, -90.0490)] // Memphis, Tennessee
        [InlineData(30.6954, -88.0399)] // Mobile, Alabama
        [InlineData(28.5, -90.5)] // the Gulf of Mexico, south of Louisiana
        [InlineData(44.8113, -91.4985)] // Eau Claire, Wisconsin
        [InlineData(49.8951, -97.1384)] // Winnipeg, Manitoba
        [InlineData(50.4452, -104.6189)] // Regina, Saskatchewan
        [InlineData(32.5149, -117.0382)] // Tijuana, Mexico
        [InlineData(36.0, -125.0)] // the Pacific, west of California
        [InlineData(44.0, -125.5)] // the Pacific, west of Oregon
        [InlineData(49.2827, -123.1207)] // Vancouver, British Columbia
        [InlineData(31.6904, -106.4245)] // Ciudad Juárez, Chihuahua, across the river from El Paso
        [InlineData(31.3086, -110.9422)] // Nogales, Sonora
        [InlineData(27.0, -94.0)] // the Gulf of Mexico, south-east of Texas
        [InlineData(49.6956, -112.8451)] // Lethbridge, Alberta
        public void Covering_FindsNothingOutsideTheStates(double latitude, double longitude)
        {
            Assert.Null(Data.Covering(longitude, latitude));
        }

        [Fact]
        public void Covering_FindsNoSenateDistrictOnLakePontchartrain()
        {
            // TIGER leaves the lake out of every Louisiana Senate district, though its
            // House districts, parishes and school districts reach across it.
            var found = Covering(30.2, -90.1);

            Assert.Equal(
                ["congressional-1", "state-lower-89", "county-22103", "school-2201650"],
                found.Select(district => district.Id));
            Assert.Null(Disagreement(found, -90.1, 30.2));
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
        [InlineData("CO", 0.05, 9_500, 13_000)]
        [InlineData("NM", 0.05, 10_500, 14_500)]
        [InlineData("NV", 0.05, 10_000, 14_000)]
        [InlineData("OK", 0.05, 6_000, 8_500)]
        [InlineData("TX", 0.1, 5_500, 7_800)]
        [InlineData("KS", 0.05, 7_500, 10_000)]
        [InlineData("MN", 0.05, 9_000, 12_500)]
        [InlineData("ND", 0.05, 7_500, 10_000)]
        [InlineData("NE", 0.05, 7_500, 10_000)]
        [InlineData("SD", 0.05, 8_000, 10_500)]
        [InlineData("AR", 0.05, 4_500, 6_500)]
        [InlineData("IA", 0.05, 5_300, 7_300)]
        [InlineData("LA", 0.05, 4_200, 6_000)]
        [InlineData("MO", 0.05, 6_300, 8_700)]
        [InlineData("MS", 0.05, 4_000, 5_700)]
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

        /// <summary>
        /// Points a few feet either side of State House edges (in Nebraska, the
        /// Legislature's), where addresses on a boundary street land, and where thinning each district on its own leaves
        /// slivers two districts cover, or none. Every one must still find one district
        /// per level. Every hundredth edge, to keep the suite quick.
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
        [InlineData("CO")]
        [InlineData("NM")]
        [InlineData("NV")]
        [InlineData("OK")]
        [InlineData("TX")]
        [InlineData("KS")]
        [InlineData("MN")]
        [InlineData("ND")]
        [InlineData("NE")]
        [InlineData("SD")]
        [InlineData("AR")]
        [InlineData("IA")]
        [InlineData("LA")]
        [InlineData("MO")]
        [InlineData("MS")]
        public void Covering_SettlesThePointsBesideASharedEdge(string code)
        {
            var disagreed = new List<string>();
            var tested = 0;

            foreach (var ring in LowerHouseRings(code))
            {
                for (var i = 0; i + 3 < ring.Length; i += 200)
                {
                    var (ax, ay, bx, by) = (ring[i], ring[i + 1], ring[i + 2], ring[i + 3]);
                    var length = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));

                    if (length == 0)
                    {
                        continue;
                    }

                    foreach (var feet in new[] { -10.0, -3.0, 3.0, 10.0 })
                    {
                        // Square to the edge, from its middle, on either side.
                        var offset = feet / 364_000;
                        var longitude = (ax + bx) / 2 - (by - ay) / length * offset;
                        var latitude = (ay + by) / 2 + (bx - ax) / length * offset;
                        var found = Alone[code].Covering(longitude, latitude);

                        if (found is null)
                        {
                            continue; // off the state's own edge
                        }

                        tested++;

                        if (Disagreement(found.Districts, longitude, latitude) is { } wrong)
                        {
                            var ids = found.Districts.Select(district => district.Id);
                            disagreed.Add($"({longitude:0.000000}, {latitude:0.000000}): {wrong}: {string.Join(", ", ids)}");
                        }
                    }
                }
            }

            Assert.Empty(disagreed);
            // Nevada's few, long Assembly districts give the fewest, about 800.
            Assert.True(tested > 500, $"Only {tested} points tested.");
        }

        /// <summary>
        /// Every ring of every State House district, or in Nebraska every Legislature
        /// district, straight from the file.
        /// </summary>
        private static IEnumerable<double[]> LowerHouseRings(string code)
        {
            var level = Legislative(States[code])[^1];
            var path = Path.Combine(
                AppContext.BaseDirectory, "Districts", "Data", $"{code.ToLowerInvariant()}-districts.json");

            using var file = File.OpenRead(path);
            using var json = JsonDocument.Parse(file);

            return json.RootElement.GetProperty("districts").EnumerateArray()
                .Where(district => district.GetProperty("level").GetString() == level)
                .SelectMany(district => district.GetProperty("rings").EnumerateArray())
                .Select(ring => ring.EnumerateArray().Select(value => value.GetDouble()).ToArray())
                .ToList();
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

                    if (Disagreement(found.Districts, longitude, latitude) is { } wrong)
                    {
                        var ids = found.Districts.Select(district => district.Id);
                        disagreed.Add($"({longitude:0.00}, {latitude:0.00}): {wrong}: {string.Join(", ", ids)}");
                    }

                    foreach (var (other, data) in Alone)
                    {
                        // Each file is thinned on its own, so two states can both claim a
                        // strip a few feet wide along their border, as neighbouring
                        // districts do. One on the Red River, between Oklahoma and Texas,
                        // falls on the grid. Anything deeper is an overlap.
                        if (other != code
                            && data.Covering(longitude, latitude) is { } also
                            && !(OnEdge(found, longitude, latitude) && OnEdge(also, longitude, latitude)))
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
        /// Whether a point is within thirty feet of the edge of the US House district it
        /// was found in: as far apart as two thinned edges can be.
        /// </summary>
        private static bool OnEdge(Found found, double longitude, double latitude)
        {
            var rings = found.Districts[0].Boundary
                .SelectMany(polygon => polygon)
                .Select(ring => ring.SelectMany(point => point).ToArray())
                .ToArray();

            return Geometry.DistanceToRings(rings, longitude, latitude) < 30 / 364_000.0;
        }

        /// <summary>
        /// What is wrong with the districts covering one point, or null. One county; one
        /// district per legislative level of the county's state, but for Louisiana's Senate
        /// on Lake Pontchartrain; at most one city
        /// (unincorporated land has none); and either a unified school district, a high
        /// school district and the elementary one inside it, one of those two alone, or
        /// none, where TIGER defines no district.
        /// </summary>
        private static string? Disagreement(List<District> found, double longitude, double latitude)
        {
            var levels = found.Select(district => district.Level).ToArray();

            if (levels.Count(level => level == "county") != 1 || levels.Count(level => level == "city") > 1)
            {
                return "not one county and at most one city";
            }

            // The county's Census code begins with its state's, which says whether there
            // is a lower house.
            var county = found.Single(district => district.Level == "county").Id;
            var state = States.Values.Single(known => county.StartsWith($"county-{known.Fips}", StringComparison.Ordinal));

            var legislative = levels
                .Where(level => level is "congressional" or "state-upper" or "state-lower")
                .ToArray();

            if (!legislative.SequenceEqual(Legislative(state))
                && !(OnLakePontchartrain(state, longitude, latitude)
                    && legislative.SequenceEqual(["congressional", "state-lower"])))
            {
                return "not one district per legislative level";
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

        /// <summary>
        /// Whether a point is in the box around Lake Pontchartrain, which TIGER puts in no
        /// Louisiana Senate district: the box of its "not defined" area in the Senate layer.
        /// </summary>
        private static bool OnLakePontchartrain(State state, double longitude, double latitude) =>
            state.Fips == "22"
                && longitude is > -90.31 and < -89.62
                && latitude is > 30.01 and < 30.39;

        private sealed record State(
            string Fips,
            double Latitude,
            double Longitude,
            string? LowerHouse,
            (double Low, double High) Longitudes,
            (double Low, double High) Latitudes,
            string UpperHouse = "State Senate");
    }
}
