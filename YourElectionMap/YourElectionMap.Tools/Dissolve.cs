namespace YourElectionMap.Tools
{
    /// <summary>
    /// Turns a pile of precinct polygons into the outline of the district they make up.
    /// <para>
    /// The trick is that two precincts sharing a border describe that border twice,
    /// once from each side. So: count every edge, keep the ones counted an odd number
    /// of times, and what is left is the outline. Then walk those edges end to end to
    /// get rings back out.
    /// </para>
    /// <para>
    /// Parity, not "counted exactly once". A few hundred segments in this data are
    /// described three times (precincts laid over one another, not beside), and the
    /// stricter rule drops them, tearing holes the walk dead-ends in. Every one of the
    /// 184 districts failed that way. Parity cannot: each closed ring gives every vertex
    /// an even number of edges, removing edges in pairs keeps it even, and a graph whose
    /// vertices all have even degree is made only of closed loops.
    /// </para>
    /// <para>
    /// What parity does not fix: a neighbour with an extra vertex midway along a border
    /// this precinct drew as one segment. Neither description has a twin, so the border
    /// stays, and the ground comes back as two rings meeting along it rather than one.
    /// It is rare here, and the leftovers are slivers the caller drops by size.
    /// </para>
    /// </summary>
    public static class Dissolve
    {
        /// <summary>
        /// Vertices are snapped to a hundredth of a foot before edges are matched. Some
        /// of these polygons carry vertices a thousandth of a foot apart, left over from
        /// the merge that built the file, and no border is drawn to that precision.
        /// </summary>
        private const double Snap = 100.0;

        /// <summary>
        /// The outline of everything in <paramref name="polygons"/>, as closed flat
        /// rings, biggest first. Every ring repeats its first point at the end, the way a
        /// shapefile does.
        /// </summary>
        /// <param name="polygons">Closed rings, in the coordinates they came in.</param>
        public static Result Rings(IEnumerable<double[]> polygons)
        {
            var counted = new Dictionary<Edge, int>();
            var vertices = 0;

            foreach (var polygon in polygons)
            {
                var count = polygon.Length / 2;
                vertices += count;

                for (var i = 0; i < count - 1; i++)
                {
                    var from = SnapPoint(polygon, i);
                    var to = SnapPoint(polygon, i + 1);

                    if (from == to)
                    {
                        continue;
                    }

                    var edge = Edge.Between(from, to);
                    counted[edge] = counted.TryGetValue(edge, out var seen) ? seen + 1 : 1;
                }
            }

            // An edge described an odd number of times has a side with nothing on it.
            var outline = new List<Edge>();
            var shared = 0;
            var overlaps = 0;

            foreach (var (edge, times) in counted)
            {
                if (times % 2 == 1)
                {
                    outline.Add(edge);
                }
                else
                {
                    shared++;
                }

                if (times > 2)
                {
                    overlaps++;
                }
            }

            var (rings, dropped) = Stitch(outline);

            return new Result(rings, vertices, outline.Count, shared, overlaps, dropped);
        }

        /// <summary>
        /// Walks the outline edges into closed rings: start anywhere, keep taking an
        /// unused edge from the vertex just reached, and stop on arriving back at the
        /// start. Where four outline edges meet at one vertex (a district pinched to a
        /// point), which one is taken is arbitrary; the rings still cover the same ground.
        /// A dead end would mean the parity argument above had failed, so it is counted.
        /// </summary>
        private static (List<double[]> Rings, int Dropped) Stitch(List<Edge> outline)
        {
            var leaving = new Dictionary<Point, List<int>>();

            for (var i = 0; i < outline.Count; i++)
            {
                Add(leaving, outline[i].From, i);
                Add(leaving, outline[i].To, i);
            }

            var walked = new bool[outline.Count];
            var rings = new List<double[]>();
            var dropped = 0;

            for (var start = 0; start < outline.Count; start++)
            {
                if (walked[start])
                {
                    continue;
                }

                var first = outline[start].From;
                var at = first;
                var edge = start;
                var path = new List<Point> { first };

                while (true)
                {
                    walked[edge] = true;
                    at = outline[edge].Other(at);
                    path.Add(at);

                    if (at == first)
                    {
                        break;
                    }

                    var next = Unwalked(leaving, walked, at);

                    if (next < 0)
                    {
                        // A dead end, which parity should rule out. Dropped and counted.
                        break;
                    }

                    edge = next;
                }

                if (path.Count < 4 || path[^1] != first)
                {
                    dropped++;
                    continue;
                }

                rings.Add(Flatten(path));
            }

            // Biggest first, so the ring a reader thinks of as "the district" leads, and
            // so that two runs of the converter order them the same way.
            rings.Sort(CompareRings);

            return (rings, dropped);
        }

        private static int CompareRings(double[] left, double[] right)
        {
            if (left.Length != right.Length)
            {
                return right.Length - left.Length;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return left[i] < right[i] ? -1 : 1;
                }
            }

            return 0;
        }

        private static double[] Flatten(List<Point> path)
        {
            // Rotated to start at its lowest vertex, so the file does not change just
            // because a dictionary handed its edges back in a different order.
            var lowest = 0;

            for (var i = 1; i < path.Count - 1; i++)
            {
                if (path[i].CompareTo(path[lowest]) < 0)
                {
                    lowest = i;
                }
            }

            var count = path.Count - 1;
            var ring = new double[(count + 1) * 2];

            for (var i = 0; i <= count; i++)
            {
                var point = path[(lowest + i) % count];
                ring[i * 2] = point.X / Snap;
                ring[i * 2 + 1] = point.Y / Snap;
            }

            return ring;
        }

        private static int Unwalked(
            Dictionary<Point, List<int>> leaving,
            bool[] walked,
            Point at)
        {
            if (!leaving.TryGetValue(at, out var edges))
            {
                return -1;
            }

            foreach (var edge in edges)
            {
                if (!walked[edge])
                {
                    return edge;
                }
            }

            return -1;
        }

        private static void Add(Dictionary<Point, List<int>> leaving, Point at, int edge)
        {
            if (!leaving.TryGetValue(at, out var edges))
            {
                edges = [];
                leaving[at] = edges;
            }

            edges.Add(edge);
        }

        private static Point SnapPoint(double[] polygon, int index) =>
            new(
                (long)Math.Round(polygon[index * 2] * Snap),
                (long)Math.Round(polygon[index * 2 + 1] * Snap));

        /// <summary>What a dissolve produced, and enough of how it went to check it.</summary>
        /// <param name="Overlaps">
        /// Edges described more than twice: precincts laid over one another rather than
        /// beside one another. Parity copes, but it is worth knowing how much of it there is.
        /// </param>
        /// <param name="DroppedRings">
        /// Walks that dead-ended or closed on fewer than three points. Expected to be
        /// zero; the slivers dropped by size are the caller's, not counted here.
        /// </param>
        public sealed record Result(
            List<double[]> Rings,
            int Vertices,
            int OutlineEdges,
            int SharedEdges,
            int Overlaps,
            int DroppedRings);

        /// <summary>A vertex, snapped to a hundredth of a foot so it can be a dictionary key.</summary>
        private readonly record struct Point(long X, long Y) : IComparable<Point>
        {
            public int CompareTo(Point other) =>
                X != other.X ? X.CompareTo(other.X) : Y.CompareTo(other.Y);
        }

        /// <summary>
        /// An edge with no direction: the same border reached from either side has to be
        /// the same key, so the ends are stored in a fixed order.
        /// </summary>
        private readonly record struct Edge(Point From, Point To)
        {
            public static Edge Between(Point a, Point b) =>
                a.CompareTo(b) < 0 ? new Edge(a, b) : new Edge(b, a);

            public Point Other(Point end) => end == From ? To : From;
        }
    }
}
