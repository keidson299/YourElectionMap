using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// The geometry behind "which district am I in", "which ring is a hole" and "how
    /// much of this boundary is worth keeping". Rings here are written out by hand, so
    /// what is being asserted is visible in the test.
    /// </summary>
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

        [Fact]
        public void BoxOf_SpansEveryRing()
        {
            Assert.Equal(new double[] { 0, 0, 22, 10 }, Geometry.BoxOf([Square, Island]));
        }

        [Theory]
        [InlineData(5, 5, true)]
        [InlineData(15, 1, true)] // in the box, between the two rings
        [InlineData(23, 1, false)]
        public void BoxCovers_IsTheCheapFirstAnswer(double x, double y, bool inside)
        {
            Assert.Equal(inside, Geometry.BoxCovers(Geometry.BoxOf([Square, Island]), x, y));
        }

        [Fact]
        public void ToPolygons_MakesOneRingOnePolygon()
        {
            var polygons = Geometry.ToPolygons([Square]);

            Assert.Single(polygons);
            Assert.Single(polygons[0]);
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

        [Fact]
        public void Thin_DropsWhatIsAlreadyOnTheLine()
        {
            // A square whose sides carry nine needless vertices each.
            var ring = Detailed();

            Assert.Equal(41, ring.Length / 2);

            var thinned = Geometry.Thin(ring, 0.01);

            // The four corners, and the closing repeat of the first.
            Assert.Equal(5, thinned.Length / 2);
            Assert.Equal(ring[0], thinned[0]);
            Assert.Equal(ring[1], thinned[1]);
            Assert.Equal(ring[^2], thinned[^2]);
            Assert.Equal(ring[^1], thinned[^1]);
        }

        [Fact]
        public void Thin_KeepsWhatIsFurtherOutThanTheTolerance()
        {
            var ring = Detailed();
            // The sixth vertex pulled five units off the side it was sitting on.
            ring[11] = -5;

            var thinned = Geometry.Thin(ring, 0.01);

            Assert.Contains(-5.0, thinned);
            Assert.True(thinned.Length < ring.Length, "it should still have thinned");
        }

        [Fact]
        public void Thin_LeavesARingItCannotUsefullyShorten()
        {
            Assert.Equal(Square, Geometry.Thin(Square, 100));
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

        /// <summary>A 10x10 square with ten vertices along each side, closed.</summary>
        private static double[] Detailed()
        {
            var points = new List<double>();

            for (var i = 0; i < 10; i++)
            {
                points.AddRange([i, 0]);
            }

            for (var i = 0; i < 10; i++)
            {
                points.AddRange([10, i]);
            }

            for (var i = 10; i > 0; i--)
            {
                points.AddRange([i, 10]);
            }

            for (var i = 10; i > 0; i--)
            {
                points.AddRange([0, i]);
            }

            points.AddRange([0, 0]);

            return [.. points];
        }
    }
}
