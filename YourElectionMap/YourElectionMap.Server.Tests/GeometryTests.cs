using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>Rings written out by hand, so what is asserted is visible in the test.</summary>
    public class GeometryTests
    {
        private static readonly double[] Square =
            [0, 0, 10, 0, 10, 10, 0, 10, 0, 0];

        private static readonly double[] HoleInIt =
            [4, 4, 6, 4, 6, 6, 4, 6, 4, 4];

        private static readonly double[] Island =
            [20, 0, 22, 0, 22, 2, 20, 2, 20, 0];

        [Theory]
        [InlineData(5, 5, true)]
        [InlineData(0.01, 0.01, true)]
        [InlineData(15, 5, false)]
        [InlineData(-1, 5, false)]
        [InlineData(5, 15, false)]
        public void Covers_AnswersForOneRing(double x, double y, bool inside)
        {
            Assert.Equal(inside, Geometry.Covers([Square], x, y));
        }

        [Fact]
        public void Covers_LeavesOutAHole()
        {
            // Both rings are wound the same way, which is the point: the file keeps rings
            // loose, so only the crossing count can say which is the hole.
            Assert.False(Geometry.Covers([Square, HoleInIt], 5, 5));
            Assert.True(Geometry.Covers([Square, HoleInIt], 2, 2));
        }

        [Fact]
        public void Covers_ReachesAnIsland()
        {
            Assert.True(Geometry.Covers([Square, Island], 21, 1));
            Assert.True(Geometry.Covers([Square, Island], 5, 5));
            Assert.False(Geometry.Covers([Square, Island], 15, 1));
        }

        [Theory]
        [InlineData(0.002, 0.005, 0.002)] // inside, nearest the west edge
        [InlineData(0.013, 0.014, 0.005)] // outside, off the corner: a 3-4-5 triangle
        public void DistanceToRings_MeasuresToTheNearestEdge(double x, double y, double distance)
        {
            // A hundredth of a degree square at the equator, where a degree of longitude
            // and one of latitude are the same length.
            double[] small = [0, 0, 0.01, 0, 0.01, 0.01, 0, 0.01, 0, 0];

            Assert.Equal(distance, Geometry.DistanceToRings([small], x, y), 6);
        }

        [Fact]
        public void DistanceToRings_ShortensLongitudeAwayFromTheEquator()
        {
            // At 60°N a degree of longitude is half a degree of latitude.
            double[] edge = [1, 59, 1, 61, 1, 59];

            Assert.Equal(0.5, Geometry.DistanceToRings([edge], 0, 60), 3);
        }

        [Fact]
        public void ToPolygons_PutsAHoleInsideTheRingAroundIt()
        {
            // Wound the same way, as the file can hold them.
            var polygons = Geometry.ToPolygons([HoleInIt, Square]);

            var polygon = Assert.Single(polygons);
            Assert.Equal(2, polygon.Count);
            Assert.Equal(Geometry.BoxOf([Square]), Geometry.BoxOf([polygon[0]]));
            Assert.Equal(Geometry.BoxOf([HoleInIt]), Geometry.BoxOf([polygon[1]]));
        }

        [Fact]
        public void ToPolygons_KeepsSeparateRingsSeparate()
        {
            var polygons = Geometry.ToPolygons([Square, Island]);

            Assert.Equal(2, polygons.Count);
            Assert.All(polygons, polygon => Assert.Single(polygon));
        }

        [Fact]
        public void ToPolygons_MakesAnIslandInAHoleItsOwnPolygon()
        {
            double[] islet = [4.5, 4.5, 5.5, 4.5, 5.5, 5.5, 4.5, 5.5, 4.5, 4.5];

            var polygons = Geometry.ToPolygons([Square, HoleInIt, islet]);

            Assert.Equal(2, polygons.Count);
            Assert.Equal(2, polygons[0].Count); // the square, with the hole in it
            Assert.Single(polygons[1]); // the islet, filled again
            Assert.Equal(Geometry.BoxOf([islet]), Geometry.BoxOf([polygons[1][0]]));
        }

        [Fact]
        public void ToPolygons_WindsOutersCounterClockwiseAndHolesClockwise()
        {
            // This square runs clockwise (y up) and the hole counter-clockwise: both the
            // wrong way round for what they turn out to be.
            double[] clockwiseSquare = [0, 0, 0, 10, 10, 10, 10, 0, 0, 0];

            var polygon = Assert.Single(Geometry.ToPolygons([clockwiseSquare, HoleInIt]));

            Assert.True(SignedArea(polygon[0]) > 0, "the outer ring should turn counter-clockwise");
            Assert.True(SignedArea(polygon[1]) < 0, "the hole should turn clockwise");

            // Reversed, still closed.
            Assert.Equal(polygon[0][0], polygon[0][^2]);
            Assert.Equal(polygon[0][1], polygon[0][^1]);
        }

        private static double SignedArea(double[] ring)
        {
            var sum = 0.0;

            for (var i = 0; i < ring.Length - 2; i += 2)
            {
                sum += ring[i] * ring[i + 3] - ring[i + 2] * ring[i + 1];
            }

            return sum / 2;
        }
    }
}
