using YourElectionMap.Server.Districts;
using YourElectionMap.Tools;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// The converter's dissolve, on polygons small enough to reason about. None of these
    /// need the precinct shapefiles, which are 130MB and live outside the repository.
    /// </summary>
    public class DissolveTests
    {
        [Fact]
        public void Rings_AddsTwoSquaresIntoOneOutline()
        {
            // Side by side, sharing the edge from (1,0) to (1,1).
            var left = Closed(0, 0, 1, 1);
            var right = Closed(1, 0, 2, 1);

            var result = Dissolve.Rings([left, right]);

            var ring = Assert.Single(result.Rings);

            // Six corners and the closing repeat; the shared edge is gone from both sides.
            Assert.Equal(7, ring.Length / 2);
            Assert.Equal(1, result.SharedEdges);
            Assert.Equal(6, result.OutlineEdges);
            Assert.Equal(0, result.Overlaps);
            Assert.Equal(0, result.DroppedRings);
            Assert.Equal(ring[0], ring[^2]);
            Assert.Equal(ring[1], ring[^1]);
        }

        [Fact]
        public void Rings_AddsAGridIntoItsPerimeter()
        {
            var squares = new List<double[]>();

            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    squares.Add(Closed(x, y, x + 1, y + 1));
                }
            }

            var result = Dissolve.Rings(squares);
            var ring = Assert.Single(result.Rings);

            // Three squares to a side, so twelve edges around the outside.
            Assert.Equal(13, ring.Length / 2);
            Assert.Equal(new double[] { 0, 0, 3, 3 }, Geometry.BoxOf(result.Rings));
        }

        [Fact]
        public void Rings_KeepsAHoleAsItsOwnRing()
        {
            // The same grid with the middle square left out.
            var squares = new List<double[]>();

            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    if (x == 1 && y == 1)
                    {
                        continue;
                    }

                    squares.Add(Closed(x, y, x + 1, y + 1));
                }
            }

            var result = Dissolve.Rings(squares);

            Assert.Equal(2, result.Rings.Count);

            // Biggest first, so the hole is second.
            Assert.Equal(13, result.Rings[0].Length / 2);
            Assert.Equal(5, result.Rings[1].Length / 2);

            // The hole is not covered, which is the whole reason Covers counts crossings.
            Assert.False(Geometry.Covers(result.Rings, 1.5, 1.5));
            Assert.True(Geometry.Covers(result.Rings, 0.5, 0.5));
        }

        [Fact]
        public void Rings_KeepsAnIslandAsItsOwnRing()
        {
            var result = Dissolve.Rings([Closed(0, 0, 1, 1), Closed(5, 5, 6, 6)]);

            Assert.Equal(2, result.Rings.Count);
            Assert.True(Geometry.Covers(result.Rings, 0.5, 0.5));
            Assert.True(Geometry.Covers(result.Rings, 5.5, 5.5));
            Assert.False(Geometry.Covers(result.Rings, 3, 3));
        }

        [Fact]
        public void Rings_ClosesEveryRingWhenABorderIsDescribedThreeTimes()
        {
            // A precinct described twice beside its neighbour, so their border is
            // described three times, as in places in the real files.
            var result = Dissolve.Rings(
                [Closed(0, 0, 1, 1), Closed(1, 0, 2, 1), Closed(1, 0, 2, 1)]);

            Assert.Equal(1, result.Overlaps);
            Assert.Equal(0, result.DroppedRings);

            var ring = Assert.Single(result.Rings);

            Assert.Equal(5, ring.Length / 2);
            Assert.Equal(ring[0], ring[^2]);
            Assert.Equal(ring[1], ring[^1]);

            // The cost: parity is a symmetric difference, so the doubled square drops out.
            // Harmless where precincts sit side by side, which DistrictDataTests checks.
            Assert.Equal(new double[] { 0, 0, 1, 1 }, Geometry.BoxOf(result.Rings));
        }

        [Fact]
        public void Rings_CoversTheGroundWhenANeighbourDrewTheBorderDifferently()
        {
            // The neighbour put a vertex halfway along a border this square drew in one
            // go, so neither description of it has a twin and the border stays. The known
            // limit of dissolving this way: two rings, but the right ground.
            var square = Closed(0, 0, 1000, 1000);
            var neighbour = new double[]
            {
                1000, 0,
                2000, 0,
                2000, 1000,
                1000, 1000,
                1000.2, 500,
                1000, 0,
            };

            var result = Dissolve.Rings([square, neighbour]);

            Assert.Equal(2, result.Rings.Count);
            Assert.Equal(0, result.DroppedRings);
            Assert.True(Geometry.Covers(result.Rings, 500, 500));
            Assert.True(Geometry.Covers(result.Rings, 1500, 500));
            Assert.False(Geometry.Covers(result.Rings, 2500, 500));
        }

        [Fact]
        public void Rings_StartsEveryRingAtTheSamePlaceWhateverOrderItIsGiven()
        {
            var squares = new[] { Closed(0, 0, 1, 1), Closed(1, 0, 2, 1), Closed(0, 1, 1, 2) };
            var reversed = new[] { squares[2], squares[1], squares[0] };

            var once = Dissolve.Rings(squares);
            var again = Dissolve.Rings(reversed);

            Assert.Equal(once.Rings.Count, again.Rings.Count);

            for (var i = 0; i < once.Rings.Count; i++)
            {
                Assert.Equal(once.Rings[i], again.Rings[i]);
            }
        }

        /// <summary>A closed rectangle, counter-clockwise, as a flat ring.</summary>
        private static double[] Closed(double left, double bottom, double right, double top) =>
            [left, bottom, right, bottom, right, top, left, top, left, bottom];
    }
}

