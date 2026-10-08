using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// The projection has no second opinion available offline, so it is checked two
    /// ways: against the one point the parameters state outright, and against the
    /// boundary data it has to agree with.
    /// </summary>
    public class StatePlaneTests
    {
        // 16 W Jones Street: the State Legislative Building in Raleigh, Wake County.
        private const double RaleighLatitude = 35.7822;
        private const double RaleighLongitude = -78.6398;

        [Fact]
        public void ToStatePlaneFeet_PutsTheProjectionsOwnOriginWhereItSaysItIs()
        {
            // Latitude_Of_Origin 33.75, Central_Meridian -79, False_Easting 2,000,000,
            // False_Northing 0, straight from the .prj.
            var (easting, northing) = StatePlane.ToStatePlaneFeet(33.75, -79);

            Assert.Equal(2_000_000, easting, 0.01);
            Assert.Equal(0, northing, 0.01);
        }

        [Fact]
        public void ToStatePlaneFeet_LandsRaleighInsideTheShapefilesOwnExtent()
        {
            // The bounding box the precinct shapefiles declare, in the same feet.
            var (easting, northing) = StatePlane.ToStatePlaneFeet(
                RaleighLatitude,
                RaleighLongitude);

            Assert.InRange(easting, 406_833, 3_070_216);
            Assert.InRange(northing, 2_699, 1_043_624);
        }

        [Fact]
        public void ToMapUnits_IsFeetDividedDownWithNorthUp()
        {
            var (easting, northing) = StatePlane.ToStatePlaneFeet(
                RaleighLatitude,
                RaleighLongitude);
            var (x, y) = StatePlane.ToMapUnits(RaleighLatitude, RaleighLongitude);

            Assert.Equal(easting / StatePlane.FeetPerMapUnit, x, 0.0001);

            // Negative, because SVG counts y downward and this is well north of the
            // projection's origin.
            Assert.Equal(-northing / StatePlane.FeetPerMapUnit, y, 0.0001);
            Assert.True(y < 0);
        }

        /// <summary>
        /// Every boundary reaches the page through the inverse, so it has to land each
        /// point back where it started — across the state, not just at the middle.
        /// </summary>
        [Theory]
        [InlineData(35.7822, -78.6398)] // Raleigh
        [InlineData(35.0873, -84.0346)] // Murphy, the far west
        [InlineData(35.9082, -75.6757)] // Manteo, the far east
        [InlineData(33.8500, -78.5500)] // the southern coast
        public void ToLatLon_UndoesToMapUnits(double latitude, double longitude)
        {
            var (x, y) = StatePlane.ToMapUnits(latitude, longitude);
            var (backLatitude, backLongitude) = StatePlane.ToLatLon(x, y);

            Assert.Equal(latitude, backLatitude, 1e-8);
            Assert.Equal(longitude, backLongitude, 1e-8);
        }

        [Fact]
        public void ToLatLon_PutsTheProjectionsOriginBackWhereTheParametersSay()
        {
            var (latitude, longitude) = StatePlane.ToLatLon(
                2_000_000 / StatePlane.FeetPerMapUnit,
                0);

            Assert.Equal(33.75, latitude, 1e-8);
            Assert.Equal(-79, longitude, 1e-8);
        }

        [Fact]
        public void FeetPerMapUnit_KeepsTheClientsFiftyFiveUnitsToTheMile()
        {
            // MAP_UNITS_PER_MILE in the client's map/frame.ts, which turns the design
            // spec's sizes into miles. The two name the same unit and have to agree.
            Assert.Equal(55, 5280 / StatePlane.FeetPerMapUnit);
        }
    }
}
