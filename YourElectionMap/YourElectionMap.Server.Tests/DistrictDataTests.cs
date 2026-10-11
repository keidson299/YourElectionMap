using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// The real district files, as the server loads them. These tests are the only check
    /// that the converter's output is sane, since the converter itself needs the source
    /// shapefiles and layers, which are not in the repository.
    /// </summary>
    public class DistrictDataTests
    {
        // Loaded once: it is megabytes, and nothing here changes it.
        private static readonly DistrictData Data = new();

        // The State Legislative Building, 16 W Jones Street, Raleigh.
        private const double RaleighLatitude = 35.7822;
        private const double RaleighLongitude = -78.6398;

        // The State Capitol, 1315 10th Street, Sacramento.
        private const double SacramentoLatitude = 38.5767;
        private const double SacramentoLongitude = -121.4934;

        private const string NorthCarolinaSource = "NCGA enacted maps";
        private const string CaliforniaSource = "U.S. Census Bureau TIGER";

        [Fact]
        public void Loads_EveryDistrictOfBothStates()
        {
            // North Carolina 14 + 50 + 120, California 52 + 40 + 80.
            Assert.Equal(["CA", "NC"], Data.States);
            Assert.Equal(184 + 172, Data.Count);
        }

        // ---- North Carolina: the General Assembly's enacted plans ----

        [Fact]
        public void Covering_FindsOneDistrictPerLevelOverARaleighAddress()
        {
            var found = Covering(RaleighLatitude, RaleighLongitude);

            Assert.Equal(
                new[] { "congressional", "state-upper", "state-lower" },
                found.Select(district => district.Level).ToArray());
        }

        [Fact]
        public void Covering_CreditsNorthCarolinasDistrictsToTheEnactedPlans()
        {
            Assert.Equal(NorthCarolinaSource, Data.Covering(RaleighLongitude, RaleighLatitude)?.Source);
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
            // Somewhere in North Carolina's box.
            AssertBoundaries(
                Covering(RaleighLatitude, RaleighLongitude),
                (-84.4, -75.4),
                (33.8, 36.6));
        }

        [Fact]
        public void Covering_PutsTheAddressInsideItsDistrictsOuterRing()
        {
            AssertInsideOuterRing(RaleighLatitude, RaleighLongitude);
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
            Assert.Null(Data.Covering(longitude, latitude));
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
        public void Covering_HasTheThreeLevelsAgreeAcrossNorthCarolina()
        {
            var inside = AssertLevelsAgree(
                NorthCarolinaSource,
                (-84.35, -75.40),
                (33.80, 36.60),
                0.05);

            // Enough of the grid to have covered the state rather than clipped a corner.
            Assert.InRange(inside, 4_000, 7_000);
        }

        [Fact]
        public void Covering_ReachesTheCornersOfNorthCarolina()
        {
            // Murphy in the west and Manteo in the east: if the conversion were off, or a
            // district were missing, one end of the state would come back empty.
            Assert.NotEmpty(Covering(35.0873, -84.0346));
            Assert.NotEmpty(Covering(35.9082, -75.6757));
        }

        // ---- California: the Census Bureau's TIGER layers ----

        [Fact]
        public void Covering_FindsCaliforniasThreeLevelsAtTheCapitol()
        {
            var found = Data.Covering(SacramentoLongitude, SacramentoLatitude);

            Assert.NotNull(found);
            Assert.Equal(CaliforniaSource, found.Source);
            Assert.Equal(
                new[] { "congressional", "state-upper", "state-lower" },
                found.Districts.Select(district => district.Level).ToArray());

            // California's lower house is the Assembly, not a State House.
            Assert.Equal(
                new[] { "U.S. House", "State Senate", "State Assembly" },
                found.Districts.Select(district => district.Office).ToArray());
            Assert.Equal(
                new[] { "Federal", "State", "State" },
                found.Districts.Select(district => district.Detail).ToArray());
        }

        [Fact]
        public void Covering_GivesCaliforniaNoPopulation()
        {
            // TIGER has none, and the chip leaves out what it is not given.
            foreach (var district in Covering(SacramentoLatitude, SacramentoLongitude))
            {
                Assert.Null(district.Population);
                Assert.Null(district.Area);
                Assert.Equal("District", district.Unit);
                Assert.Equal($"{district.Level}-{district.Number}", district.Id);
            }
        }

        [Fact]
        public void Covering_SendsCaliforniasBoundariesInLongitudeAndLatitude()
        {
            // Somewhere in California's box.
            AssertBoundaries(
                Covering(SacramentoLatitude, SacramentoLongitude),
                (-124.5, -114.1),
                (32.5, 42.1));
        }

        [Fact]
        public void Covering_PutsTheCapitolInsideItsDistrictsOuterRing()
        {
            AssertInsideOuterRing(SacramentoLatitude, SacramentoLongitude);
        }

        [Theory]
        [InlineData(41.7558, -124.2026)] // Crescent City, the north-west
        [InlineData(32.7920, -115.5631)] // El Centro, the south-east
        public void Covering_ReachesTheCornersOfCalifornia(double latitude, double longitude)
        {
            Assert.Equal(3, Covering(latitude, longitude).Count);
        }

        [Theory]
        [InlineData(39.5296, -119.8138)] // Reno, Nevada
        [InlineData(36.1699, -115.1398)] // Las Vegas, Nevada
        [InlineData(32.5149, -117.0382)] // Tijuana, Mexico
        [InlineData(36.0, -125.0)] // the Pacific, west of the state
        public void Covering_FindsNothingOutsideCalifornia(double latitude, double longitude)
        {
            Assert.Null(Data.Covering(longitude, latitude));
        }

        /// <summary>
        /// The same check as North Carolina's. TIGER's three layers are separate files,
        /// each built from the census blocks, so they too must cover the state once.
        /// </summary>
        [Fact]
        public void Covering_HasTheThreeLevelsAgreeAcrossCalifornia()
        {
            var inside = AssertLevelsAgree(
                CaliforniaSource,
                (-124.5, -114.0),
                (32.5, 42.1),
                0.1);

            Assert.InRange(inside, 3_000, 6_000);
        }

        private static List<District> Covering(double latitude, double longitude) =>
            Data.Covering(longitude, latitude)?.Districts ?? [];

        /// <summary>
        /// Walks a grid, and at every point some district covers, checks there is exactly
        /// one district per level and that they come from the expected state. Returns how
        /// many points were covered.
        /// </summary>
        private static int AssertLevelsAgree(
            string source,
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

                    if (found is null || found.Source != source)
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

        private static void AssertBoundaries(
            List<District> found,
            (double Low, double High) longitudes,
            (double Low, double High) latitudes)
        {
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
                            Assert.InRange(point[0], longitudes.Low, longitudes.High);
                            Assert.InRange(point[1], latitudes.Low, latitudes.High);
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
        /// The address, as the page's map will draw it, falls inside the outer ring of one
        /// of each district's polygons: the rings did not come out as holes.
        /// </summary>
        private static void AssertInsideOuterRing(double latitude, double longitude)
        {
            foreach (var district in Covering(latitude, longitude))
            {
                Assert.Contains(
                    district.Boundary,
                    polygon => Geometry.Covers(
                        [polygon[0].SelectMany(point => point).ToArray()],
                        longitude,
                        latitude));
            }
        }

        private static double[] Box(District district) =>
            Geometry.BoxOf(
                district.Boundary
                    .SelectMany(polygon => polygon)
                    .Select(ring => ring.SelectMany(point => point).ToArray())
                    .ToArray());
    }
}
