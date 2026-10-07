using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// The geometry behind "which district am I in" and "how much of this boundary is
    /// worth sending". Rings here are written out by hand, so what is being asserted is
    /// visible in the test.
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
            // Both rings are wound the same way, which is the point: a dissolve does not
            // say which is the hole, so only the crossing count can.
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
