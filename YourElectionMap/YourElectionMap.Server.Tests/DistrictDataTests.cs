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
        /// Each state's FIPS code, its capitol, its lower house (Nebraska has none), and the
        /// box every boundary point must fall in.
        /// </summary>
        private static readonly Dictionary<string, State> States = new()
        {
            ["NC"] = new("37", 35.7822, -78.6398, "State House", (-84.4, -75.4), (33.8, 36.6)),
            ["CA"] = new("06", 38.5767, -121.4934, "State Assembly", (-124.5, -114.1), (32.5, 42.1)),
            ["OR"] = new("41", 44.9385, -123.0301, "State House", (-124.75, -116.4), (41.95, 46.35)),
            ["SC"] = new("45", 34.0004, -81.0330, "State House", (-83.4, -78.45), (31.95, 35.25)),
            // From here on the boxes start a few hundredths off any border on a round degree:
            // a grid row on 37°N would be on the Utah–Arizona line, and one on 49°N finds a
            // Washington school district on the Canadian line itself.
            ["WA"] = new("53", 47.0358, -122.9048, "State House", (-124.9, -116.9), (45.52, 49.1)),
            ["AZ"] = new("04", 33.4481, -112.0970, "State House", (-114.83, -109.0), (31.31, 37.05)),
            ["ID"] = new("16", 43.6177, -116.1996, "State House", (-117.23, -111.0), (41.97, 49.05)),
            ["MT"] = new("30", 46.5857, -112.0184, "State House", (-116.07, -104.0), (44.37, 49.05)),
            ["UT"] = new("49", 40.7774, -111.8882, "State House", (-114.07, -109.0), (36.97, 42.05)),
            ["WY"] = new("56", 41.1403, -104.8202, "State House", (-111.07, -104.0), (40.97, 45.05)),
            ["CO"] = new("08", 39.7393, -104.9848, "State House", (-109.07, -102.02), (36.97, 41.05)),
            ["NM"] = new("35", 35.6824, -105.9397, "State House", (-109.07, -102.97), (31.31, 37.02)),
            ["NV"] = new("32", 39.1640, -119.7663, "State Assembly", (-120.07, -114.0), (34.97, 42.05)),
            ["OK"] = new("40", 35.4922, -97.5034, "State House", (-103.07, -94.4), (33.57, 37.05)),
            ["TX"] = new("48", 30.2747, -97.7403, "State House", (-106.67, -93.47), (25.82, 36.52)),
            ["KS"] = new("20", 39.0481, -95.6780, "State House", (-102.07, -94.55), (36.97, 40.05)),
            ["MN"] = new("27", 44.9551, -93.1022, "State House", (-97.27, -89.45), (43.47, 49.42)),
            ["ND"] = new("38", 46.8209, -100.7827, "State House", (-104.07, -96.5), (45.92, 49.05)),
            ["NE"] = new("31", 40.8081, -96.6997, null, (-104.07, -95.27), (39.97, 43.05), UpperHouse: "Legislature"),
            ["SD"] = new("46", 44.3671, -100.3462, "State House", (-104.07, -96.4), (42.47, 45.97)),
            ["AR"] = new("05", 34.7466, -92.2890, "State House", (-94.62, -89.64), (32.97, 36.52)),
            ["IA"] = new("19", 41.5912, -93.6038, "State House", (-96.64, -90.14), (40.37, 43.51)),
            ["LA"] = new("22", 30.4571, -91.1874, "State House", (-94.05, -88.75), (28.87, 33.03)),
            ["MO"] = new("29", 38.5792, -92.1729, "State House", (-95.78, -89.09), (35.97, 40.62)),
            ["MS"] = new("28", 32.3036, -90.1823, "State House", (-91.66, -88.07), (30.12, 35.02)),
            ["AL"] = new("01", 32.3771, -86.3008, "State House", (-88.48, -84.88), (30.14, 35.01)),
            ["FL"] = new("12", 30.4381, -84.2816, "State House", (-87.64, -79.97), (24.39, 31.01)),
            ["GA"] = new("13", 33.7490, -84.3880, "State House", (-85.61, -80.78), (30.34, 35.01)),
            ["KY"] = new("21", 38.1867, -84.8753, "State House", (-89.58, -81.96), (36.49, 39.15)),
            ["TN"] = new("47", 36.1658, -86.7844, "State House", (-90.32, -81.64), (34.98, 36.68)),
            ["IL"] = new("17", 39.7983, -89.6544, "State House", (-91.52, -87.01), (36.96, 42.51)),
            ["IN"] = new("18", 39.7686, -86.1625, "State House", (-88.10, -84.78), (37.77, 41.77)),
            ["MI"] = new("26", 42.7336, -84.5555, "State House", (-90.42, -82.12), (41.69, 48.31)),
            ["OH"] = new("39", 39.9612, -82.9988, "State House", (-84.83, -80.51), (38.40, 42.33)),
            ["WI"] = new("55", 43.0747, -89.3842, "State Assembly", (-92.89, -86.24), (42.48, 47.31)),
            ["DE"] = new("10", 39.1573, -75.5197, "State House", (-75.79, -74.98), (38.45, 39.84)),
            ["MD"] = new("24", 38.9787, -76.4910, "House of Delegates", (-79.49, -74.98), (37.88, 39.73)),
            ["PA"] = new("42", 40.2645, -76.8836, "State House", (-80.52, -74.68), (39.71, 42.52)),
            ["VA"] = new("51", 37.5385, -77.4336, "House of Delegates", (-83.68, -75.16), (36.54, 39.47)),
            ["WV"] = new("54", 38.3365, -81.6123, "House of Delegates", (-82.65, -77.71), (37.20, 40.64)),
            ["CT"] = new("09", 41.7640, -72.6823, "State House", (-73.73, -71.78), (40.95, 42.06)),
            ["MA"] = new("25", 42.3587, -71.0637, "State House", (-73.51, -69.85), (41.18, 42.89), Named: ["state-upper", "state-lower"]),
            ["NJ"] = new("34", 40.2204, -74.7701, "General Assembly", (-75.57, -73.88), (38.78, 41.36)),
            ["NY"] = new("36", 42.6526, -73.7573, "State Assembly", (-79.77, -71.77), (40.47, 45.02)),
            ["RI"] = new("44", 41.8309, -71.4148, "State House", (-71.91, -71.08), (41.09, 42.02)),
            ["VT"] = new("50", 44.2624, -72.5809, "State House", (-73.44, -71.46), (42.72, 45.02), Named: ["state-upper", "state-lower"]),
            ["NH"] = new("33", 43.2068, -71.5383, "State House", (-72.56, -70.57), (42.69, 45.31), Named: ["state-lower"]),
            ["ME"] = new("23", 44.3072, -69.7817, "State House", (-71.09, -66.88), (42.91, 47.46)),
            ["HI"] = new("15", 21.3073, -157.8574, "State House", (-178.45, -154.75), (18.86, 28.52), NoCity: true),
            // The Near Islands, past the 180th meridian, are kept below -180.
            ["AK"] = new("02", 58.3020, -134.4104, "State House", (-187.66, -129.97), (51.17, 71.44)),
        };

        /// <summary>Every state above, one test case each.</summary>
        public static TheoryData<string> EveryState => new(States.Keys);

        /// <summary>The levels a state elects its legislators by, broad to local.</summary>
        private static string[] Legislative(State state) =>
            state.LowerHouse is null
                ? ["congressional", "state-upper"]
                : ["congressional", "state-upper", "state-lower"];

        [Fact]
        public void Loads_EveryState()
        {
            Assert.Equal(
                [
                    "AK", "AL", "AR", "AZ", "CA", "CO", "CT", "DE", "FL", "GA", "HI", "IA", "ID",
                    "IL", "IN", "KS", "KY", "LA", "MA", "MD", "ME", "MI", "MN", "MO", "MS", "MT",
                    "NC", "ND", "NE", "NH", "NJ", "NM", "NV", "NY", "OH", "OK", "OR", "PA", "RI",
                    "SC", "SD", "TN", "TX", "UT", "VA", "VT", "WA", "WI", "WV", "WY",
                ],
                Data.States);
        }

        [Theory]
        [MemberData(nameof(EveryState))]
        public void Covering_FindsOneDistrictPerLevelAtTheCapitol(string code)
        {
            var state = States[code];
            var found = Data.Covering(state.Longitude, state.Latitude);

            Assert.NotNull(found);

            var levels = Legislative(state);
            var legislative = found.Take(levels.Length).ToArray();
            var local = found.Skip(levels.Length).ToArray();

            Assert.Equal(levels, legislative.Select(district => district.Level));

            foreach (var (district, chamber) in legislative.Zip(new[] { "U.S. House", state.UpperHouse, state.LowerHouse }))
            {
                if (state.Names(district.Level))
                {
                    // Massachusetts and Vermont name their districts, and New Hampshire its
                    // House districts: each is its name, over its chamber, "Addison-1
                    // District" or "Merrimack District 18".
                    Assert.Matches(@" District( \d+)?$", district.Office);
                    Assert.DoesNotContain(chamber!, district.Office);
                    Assert.Equal(chamber, district.Detail);
                }
                else
                {
                    Assert.Equal(chamber, district.Office);
                    Assert.Equal(district.Level == "congressional" ? "Federal" : "State", district.Detail);
                }
            }

            // A county row, then a city row (none in Honolulu, which is a county, and a
            // township's before it in Illinois and Indiana), then one or two school rows.
            var cities = local.Skip(1).TakeWhile(district => district.Level == "city").Count();

            Assert.Equal("county", local[0].Level);
            Assert.InRange(cities, state.NoCity ? 0 : 1, state.NoCity ? 0 : 2);
            Assert.NotEmpty(local.Skip(1 + cities));
            Assert.All(local.Skip(1 + cities), district => Assert.Equal("school", district.Level));
            Assert.Null(Disagreement(found, state.Longitude, state.Latitude));

            var county = local[0].Office;

            foreach (var district in legislative)
            {
                if (state.Names(district.Level))
                {
                    Assert.StartsWith($"{district.Level}-{state.Fips}", district.Id);
                    Assert.Null(district.Number);
                }
                else
                {
                    Assert.Equal($"{district.Level}-{district.Number}", district.Id);
                }

                Assert.Equal(county, district.Area);
            }

            foreach (var district in local)
            {
                Assert.StartsWith($"{district.Level}-{state.Fips}", district.Id);
                Assert.Null(district.Number);
            }

            foreach (var district in found)
            {
                // Polygons of closed rings in [longitude, latitude], inside the state.
                Assert.NotEmpty(district.Boundary);
                Assert.All(district.Boundary.SelectMany(polygon => polygon), ring =>
                {
                    Assert.True(ring.Length >= 4);
                    Assert.Equal(ring[0], ring[^1]);
                    Assert.All(ring, point =>
                    {
                        Assert.Equal(2, point.Length);
                        Assert.InRange(point[0], state.Longitudes.Low, state.Longitudes.High);
                        Assert.InRange(point[1], state.Latitudes.Low, state.Latitudes.High);
                    });
                });

                // The capitol falls inside an outer ring, as the map will draw it: the
                // rings did not come out as holes.
                Assert.Contains(
                    district.Boundary,
                    polygon => Geometry.Covers(
                        [polygon[0].SelectMany(point => point).ToArray()],
                        state.Longitude,
                        state.Latitude));
            }
        }

        /// <summary>
        /// Points the capitol and the grid would miss: past the 180th meridian, a city
        /// outside any county, an exclave across a lake, and an island smaller than the
        /// grid's spacing.
        /// </summary>
        [Theory]
        [InlineData(52.7236, -185.8900)] // Shemya, Alaska, past the 180th meridian, at 174.11 east
        [InlineData(38.6270, -90.1994)] // St. Louis, a city outside any county
        [InlineData(49.3456, -95.0647)] // Angle Inlet, on the Northwest Angle, across the Lake of the Woods
        [InlineData(24.5551, -81.7800)] // Key West
        public void Covering_ReachesThePlacesTheGridMisses(double latitude, double longitude)
        {
            var found = Covering(latitude, longitude);

            Assert.NotEmpty(found);
            Assert.Null(Disagreement(found, longitude, latitude));
        }

        [Theory]
        [InlineData(38.8977, -77.0365)] // the White House
        [InlineData(42.3149, -83.0364)] // Windsor, Ontario, across the river from Detroit
        [InlineData(38.0, -74.5)] // the Atlantic, east of Maryland
        [InlineData(31.6904, -106.4245)] // Ciudad Juárez, Chihuahua, across the river from El Paso
        [InlineData(21.6, -158.8)] // the Kauai Channel, between Oahu and Kauai
        [InlineData(64.3833, -173.2333)] // Provideniya, Russia, across the Bering Strait
        public void Covering_FindsNothingOutsideTheStates(double latitude, double longitude)
        {
            Assert.Null(Data.Covering(longitude, latitude));
        }

        /// <summary>
        /// A town or township that governs itself is a row of its own, and a city or
        /// village inside one, whose voters elect both, comes after it.
        /// </summary>
        [Theory]
        [InlineData(40.7268, -73.6343, "Hempstead town", "Town", "Garden City village")] // Garden City, New York
        [InlineData(40.0068, -75.2852, "Lower Merion township", "Township")] // Ardmore, Pennsylvania, in no city
        [InlineData(43.7646, -69.3195, "Monhegan plantation", "Plantation")] // Monhegan, Maine, an island
        public void Covering_ListsATownOrTownshipBeforeTheCityInIt(
            double latitude,
            double longitude,
            string town,
            string kind,
            params string[] city)
        {
            var found = Covering(latitude, longitude);
            var cities = found.Where(district => district.Level == "city").ToArray();

            Assert.Equal([town, .. city], cities.Select(district => district.Office));
            Assert.Equal([kind, .. city.Select(_ => "City or town")], cities.Select(district => district.Detail));
            Assert.Null(Disagreement(found, longitude, latitude));
        }

        [Theory]
        [InlineData(30.2, -90.1, "congressional-1", "state-lower-89", "county-22103", "school-2201650")] // Lake Pontchartrain
        [InlineData(41.9, -87.5, "county-17031")] // Lake Michigan, off Chicago
        [InlineData(43.05, -87.7, "congressional-4", "county-55079")] // Lake Michigan, off Milwaukee
        [InlineData(44.0, -88.4, "congressional-6", "county-55015", "city-5501510350")] // Lake Winnebago, in the Town of Brothertown
        [InlineData(41.15, -73.0, "county-09170")] // Long Island Sound, off Milford
        [InlineData(41.85, -70.3, "congressional-9", "state-upper-25D40", "county-25001")] // Cape Cod Bay
        [InlineData(42.94, -70.76, "county-33015")] // the Atlantic, off Rye, New Hampshire
        public void Covering_FindsNoLegislatorsOnTheWaterTigerLeavesOut(
            double latitude,
            double longitude,
            params string[] ids)
        {
            // At the shore, all three levels stop in Illinois, Connecticut and New
            // Hampshire, leaving the county; both chambers in Wisconsin; the State House in
            // Massachusetts; and the Senate on Lake Pontchartrain.
            var found = Covering(latitude, longitude);

            Assert.Equal(ids, found.Select(district => district.Id));
            Assert.Null(Disagreement(found, longitude, latitude));
        }

        /// <summary>
        /// The one check on the files that needs no outside authority. Every plan, and the
        /// counties, is built from the same 2020 census blocks and covers the state exactly
        /// once, so every covered point must find one district per level, never two. A plan
        /// read wrongly, a missing district, a layer shifted along the coast or two states'
        /// files overlapping would show up here. Cities and school districts are held to
        /// what <see cref="Disagreement"/> allows.
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
        [InlineData("AL", 0.05, 4_300, 6_200)]
        [InlineData("FL", 0.05, 5_800, 8_000)]
        [InlineData("GA", 0.05, 5_000, 7_000)]
        [InlineData("KY", 0.05, 3_500, 5_000)]
        [InlineData("TN", 0.05, 3_600, 5_100)]
        [InlineData("IL", 0.05, 5_400, 7_200)]
        [InlineData("IN", 0.05, 3_400, 4_500)]
        [InlineData("MI", 0.05, 9_800, 13_000)]
        [InlineData("OH", 0.05, 4_200, 5_700)]
        [InlineData("WI", 0.05, 6_600, 8_800)]
        [InlineData("DE", 0.05, 200, 330)]
        [InlineData("MD", 0.05, 1_100, 1_550)]
        [InlineData("PA", 0.05, 4_300, 5_800)]
        [InlineData("VA", 0.05, 3_800, 5_200)]
        [InlineData("WV", 0.05, 2_200, 3_000)]
        [InlineData("CT", 0.05, 500, 750)]
        [InlineData("MA", 0.05, 1_000, 1_400)]
        [InlineData("NJ", 0.05, 800, 1_150)]
        [InlineData("NY", 0.05, 5_300, 7_200)]
        [InlineData("RI", 0.05, 140, 210)]
        [InlineData("VT", 0.05, 950, 1_300)]
        [InlineData("NH", 0.05, 900, 1_250)]
        [InlineData("ME", 0.05, 3_600, 4_800)]
        [InlineData("HI", 0.05, 850, 1_150)]
        [InlineData("AK", 0.1, 27_000, 36_000)]
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
        /// Legislature's), where addresses on a boundary street land, and where thinning
        /// each district on its own leaves slivers two districts cover, or none. Every one
        /// must still find one district per level. Every hundredth edge, to keep the suite
        /// quick.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryState))]
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

                        if (Disagreement(found, longitude, latitude) is { } wrong)
                        {
                            var ids = found.Select(district => district.Id);
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
            Data.Covering(longitude, latitude) ?? [];

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

                    if (Disagreement(found, longitude, latitude) is { } wrong)
                    {
                        var ids = found.Select(district => district.Id);
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
        private static bool OnEdge(List<District> found, double longitude, double latitude)
        {
            var rings = found[0].Boundary
                .SelectMany(polygon => polygon)
                .Select(ring => ring.SelectMany(point => point).ToArray())
                .ToArray();

            return Geometry.DistanceToRings(rings, longitude, latitude) < 30 / 364_000.0;
        }

        /// <summary>
        /// What is wrong with the districts covering one point, or null. One county; one
        /// district per legislative level of the county's state, but on the water TIGER
        /// leaves out of some levels (<see cref="Waters"/>); at most one city or town
        /// (unincorporated land has none), or a town or township and a city or village in
        /// it; and either a unified school district, a high school district and the
        /// elementary one inside it, one of those two alone, or none, where TIGER defines
        /// no district.
        /// </summary>
        private static string? Disagreement(List<District> found, double longitude, double latitude)
        {
            var levels = found.Select(district => district.Level).ToArray();

            var cities = found
                .Where(district => district.Level == "city")
                .Select(district => district.Detail)
                .ToArray();

            if (levels.Count(level => level == "county") != 1
                || cities is not ([] or [_] or ["Town" or "Township", "City or town"]))
            {
                return "not one county and at most one city, or a township and a city in it";
            }

            // The county's Census code begins with its state's, which says whether there
            // is a lower house.
            var county = found.Single(district => district.Level == "county").Id;
            var state = States.Values.Single(known => county.StartsWith($"county-{known.Fips}", StringComparison.Ordinal));

            var legislative = levels
                .Where(level => level is "congressional" or "state-upper" or "state-lower")
                .ToArray();

            if (!legislative.SequenceEqual(Legislative(state))
                && !Waters.Any(water => water.Fips == state.Fips
                    && water.Covers(longitude, latitude)
                    && legislative.SequenceEqual(Legislative(state).Except(water.Lacks))))
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
        /// The water TIGER puts in no district of some legislative levels, as the boxes of
        /// the pieces of its "not defined" areas, and the levels a point there lacks. The
        /// boxes take in shore as well, but a point there may only lack those levels all
        /// together, which one district gone missing would not do.
        /// </summary>
        private static readonly Water[] Waters =
        [
            new("22", (-90.31, -89.62), (30.01, 30.39), ["state-upper"]), // Lake Pontchartrain
            new("17", (-87.84, -87.01), (41.75, 42.50), ["congressional", "state-upper", "state-lower"]), // Lake Michigan
            new("55", (-88.05, -86.24), (42.49, 45.45), ["state-upper", "state-lower"]), // Lake Michigan and Green Bay
            new("55", (-88.54, -88.29), (43.79, 44.21), ["state-upper", "state-lower"]), // Lake Winnebago
            new("55", (-92.15, -89.95), (46.56, 47.32), ["state-upper", "state-lower"]), // Lake Superior and the Superior harbor
            new("09", (-73.46, -72.05), (40.98, 41.29), ["congressional", "state-upper", "state-lower"]), // Long Island Sound
            new("25", (-70.76, -69.85), (41.46, 42.38), ["state-lower"]), // Cape Cod Bay and Nantucket Sound
            new("25", (-70.86, -70.72), (42.29, 42.38), ["state-lower"]), // Massachusetts Bay, off Hull
            new("25", (-70.91, -70.50), (42.39, 42.88), ["state-lower"]), // Massachusetts Bay, off the North Shore
            new("25", (-70.92, -70.64), (41.50, 41.67), ["state-lower"]), // Buzzards Bay
            new("25", (-71.12, -70.97), (41.41, 41.49), ["state-lower"]), // Rhode Island Sound, off Westport
            new("33", (-70.82, -70.57), (42.87, 43.01), ["congressional", "state-upper", "state-lower"]), // the Atlantic, off New Hampshire
        ];

        private sealed record Water(
            string Fips,
            (double Low, double High) Longitudes,
            (double Low, double High) Latitudes,
            string[] Lacks)
        {
            public bool Covers(double longitude, double latitude) =>
                longitude > Longitudes.Low && longitude < Longitudes.High
                    && latitude > Latitudes.Low && latitude < Latitudes.High;
        }

        private sealed record State(
            string Fips,
            double Latitude,
            double Longitude,
            string? LowerHouse,
            (double Low, double High) Longitudes,
            (double Low, double High) Latitudes,
            string UpperHouse = "State Senate",
            string[]? Named = null,
            bool NoCity = false)
        {
            /// <summary>Whether a level's districts are named rather than numbered.</summary>
            public bool Names(string level) => Named?.Contains(level) == true;
        }
    }
}
