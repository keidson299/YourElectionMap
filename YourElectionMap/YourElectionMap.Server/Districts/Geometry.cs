namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// The little geometry this project needs: whether a point falls inside a set of
    /// rings, the box around them, sorting rings into polygons for the page's map, and
    /// thinning a ring, which the converter does before it stores one.
    /// <para>
    /// A ring is a flat <c>[x, y, x, y, …]</c> array, closed, which is how the
    /// district file stores one. Keeping that one shape everywhere saves reshaping it
    /// at every step.
    /// </para>
    /// </summary>
    public static class Geometry
    {
        /// <summary>
        /// True when the point falls inside an odd number of the rings.
        /// <para>
        /// Even-odd rather than winding: the file keeps a district's rings loose, with
        /// no promise about which way the source layers wound them, so an island and a
        /// hole look the same until the crossings are counted.
        /// </para>
        /// </summary>
        public static bool Covers(IReadOnlyList<double[]> rings, double x, double y)
        {
            var inside = false;

            foreach (var ring in rings)
            {
                if (CrossesOddTimes(ring, x, y))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>The <c>[minX, minY, maxX, maxY]</c> box around every ring.</summary>
        public static double[] BoxOf(IReadOnlyList<double[]> rings)
        {
            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;

            foreach (var ring in rings)
            {
                for (var i = 0; i < ring.Length; i += 2)
                {
                    minX = Math.Min(minX, ring[i]);
                    maxX = Math.Max(maxX, ring[i]);
                    minY = Math.Min(minY, ring[i + 1]);
                    maxY = Math.Max(maxY, ring[i + 1]);
                }
            }

            return [minX, minY, maxX, maxY];
        }

        /// <summary>
        /// Whether the point is in the box. A district covers well under a hundredth
        /// of the state, so this answers "no" for nearly every district at a fraction
        /// of the cost of <see cref="Covers"/>.
        /// </summary>
        public static bool BoxCovers(double[] box, double x, double y) =>
            x >= box[0] && x <= box[2] && y >= box[1] && y <= box[3];

        /// <summary>
        /// Sorts loose rings into polygons: each an outer ring followed by its holes,
        /// outers wound counter-clockwise and holes clockwise (with y pointing up, as
        /// latitude does). That is the shape GeoJSON asks for, and the page's map fills
        /// by it.
        /// <para>
        /// The rings say nothing about which is which — the file keeps them loose —
        /// so it is worked out the way <see cref="Covers"/> would: a ring inside an even
        /// number of others is an outer one, an island in a lake counting as outer again,
        /// and a ring inside an odd number is a hole in the innermost of them.
        /// </para>
        /// </summary>
        public static List<List<double[]>> ToPolygons(IReadOnlyList<double[]> rings)
        {
            var boxes = rings.Select(ring => BoxOf([ring])).ToArray();
            var containers = new List<int>[rings.Count];

            for (var i = 0; i < rings.Count; i++)
            {
                containers[i] = [];

                for (var j = 0; j < rings.Count; j++)
                {
                    if (i != j && Contains(rings[j], boxes[j], rings[i], boxes[i]))
                    {
                        containers[i].Add(j);
                    }
                }
            }

            var polygons = new List<List<double[]>>();
            var polygonOf = new Dictionary<int, List<double[]>>();

            for (var i = 0; i < rings.Count; i++)
            {
                if (containers[i].Count % 2 == 0)
                {
                    var polygon = new List<double[]> { Wound(rings[i], counterClockwise: true) };
                    polygons.Add(polygon);
                    polygonOf[i] = polygon;
                }
            }

            for (var i = 0; i < rings.Count; i++)
            {
                if (containers[i].Count % 2 == 1)
                {
                    // The innermost container is the one inside the most others.
                    var parent = containers[i].MaxBy(j => containers[j].Count);
                    polygonOf[parent].Add(Wound(rings[i], counterClockwise: false));
                }
            }

            return polygons;
        }

        /// <summary>
        /// Douglas-Peucker: drop every vertex that sits within <paramref name="tolerance"/>
        /// of the line its neighbours already draw. Returns the ring unchanged when it
        /// is too short to thin, and never returns fewer than four points.
        /// </summary>
        public static double[] Thin(double[] ring, double tolerance)
        {
            var count = ring.Length / 2;

            if (count < 5 || tolerance <= 0)
            {
                return ring;
            }

            var keep = new bool[count];
            keep[0] = true;
            keep[count - 1] = true;

            // A ring ends where it began, so the chord between those two points has no
            // length and everything would measure as being on it. Anchoring the vertex
            // furthest from the start gives the halves a chord each.
            var far = FurthestFromStart(ring, count);
            keep[far] = true;

            Thin(ring, keep, 0, far, tolerance);
            Thin(ring, keep, far, count - 1, tolerance);

            var kept = 0;

            foreach (var flag in keep)
            {
                if (flag)
                {
                    kept++;
                }
            }

            if (kept < 4)
            {
                return ring;
            }

            var thinned = new double[kept * 2];
            var next = 0;

            for (var i = 0; i < count; i++)
            {
                if (keep[i])
                {
                    thinned[next++] = ring[i * 2];
                    thinned[next++] = ring[i * 2 + 1];
                }
            }

            return thinned;
        }

        /// <summary>
        /// Marks every vertex between <paramref name="first"/> and <paramref name="last"/>
        /// that has to stay. Walked with an explicit stack rather than by recursion:
        /// a congressional boundary arrives with tens of thousands of vertices.
        /// </summary>
        private static void Thin(
            double[] ring,
            bool[] keep,
            int first,
            int last,
            double tolerance)
        {
            var pending = new Stack<(int First, int Last)>();
            pending.Push((first, last));

            while (pending.Count > 0)
            {
                var (from, to) = pending.Pop();

                if (to - from < 2)
                {
                    continue;
                }

                var ax = ring[from * 2];
                var ay = ring[from * 2 + 1];
                var bx = ring[to * 2];
                var by = ring[to * 2 + 1];
                var dx = bx - ax;
                var dy = by - ay;
                var length = Math.Sqrt(dx * dx + dy * dy);

                var worst = -1.0;
                var worstAt = -1;

                for (var i = from + 1; i < to; i++)
                {
                    var px = ring[i * 2];
                    var py = ring[i * 2 + 1];

                    var distance = length == 0
                        ? Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay))
                        : Math.Abs(dy * px - dx * py + bx * ay - by * ax) / length;

                    if (distance > worst)
                    {
                        worst = distance;
                        worstAt = i;
                    }
                }

                if (worst <= tolerance || worstAt < 0)
                {
                    continue;
                }

                keep[worstAt] = true;
                pending.Push((from, worstAt));
                pending.Push((worstAt, to));
            }
        }

        private static int FurthestFromStart(double[] ring, int count)
        {
            var x = ring[0];
            var y = ring[1];
            var worst = -1.0;
            var worstAt = count / 2;

            for (var i = 1; i < count - 1; i++)
            {
                var dx = ring[i * 2] - x;
                var dy = ring[i * 2 + 1] - y;
                var distance = dx * dx + dy * dy;

                if (distance > worst)
                {
                    worst = distance;
                    worstAt = i;
                }
            }

            return worstAt;
        }

        /// <summary>
        /// Whether one ring lies inside another. A district's rings never cross, so one
        /// vertex settles it — any vertex but one the two rings share, which would sit on
        /// the outer ring's edge and answer either way.
        /// </summary>
        private static bool Contains(double[] outer, double[] outerBox, double[] inner, double[] innerBox)
        {
            if (innerBox[0] < outerBox[0] || innerBox[1] < outerBox[1]
                || innerBox[2] > outerBox[2] || innerBox[3] > outerBox[3])
            {
                return false;
            }

            var shared = new HashSet<(double, double)>();

            for (var i = 0; i < outer.Length; i += 2)
            {
                shared.Add((outer[i], outer[i + 1]));
            }

            for (var i = 0; i < inner.Length; i += 2)
            {
                if (!shared.Contains((inner[i], inner[i + 1])))
                {
                    return CrossesOddTimes(outer, inner[i], inner[i + 1]);
                }
            }

            // Every vertex shared: the same ring twice, which contains nothing.
            return false;
        }

        /// <summary>The ring as given, or reversed, so that it turns the asked way.</summary>
        private static double[] Wound(double[] ring, bool counterClockwise)
        {
            if (SignedArea(ring) > 0 == counterClockwise)
            {
                return ring;
            }

            var reversed = new double[ring.Length];

            for (var i = 0; i < ring.Length; i += 2)
            {
                reversed[ring.Length - 2 - i] = ring[i];
                reversed[ring.Length - 1 - i] = ring[i + 1];
            }

            return reversed;
        }

        /// <summary>The shoelace sum: positive for a counter-clockwise ring, y up.</summary>
        private static double SignedArea(double[] ring)
        {
            var sum = 0.0;
            var count = ring.Length / 2;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                sum += ring[j * 2] * ring[i * 2 + 1] - ring[i * 2] * ring[j * 2 + 1];
            }

            return sum / 2;
        }

        /// <summary>
        /// A ray cast east from the point: count the edges it crosses. Edges are taken
        /// as the pairs around the ring, so a ring that repeats its first point at the
        /// end simply contributes one edge of no length, which crosses nothing.
        /// </summary>
        private static bool CrossesOddTimes(double[] ring, double x, double y)
        {
            var count = ring.Length / 2;
            var crossings = false;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var ix = ring[i * 2];
                var iy = ring[i * 2 + 1];
                var jx = ring[j * 2];
                var jy = ring[j * 2 + 1];

                if (iy > y != jy > y
                    && x < (jx - ix) * (y - iy) / (jy - iy) + ix)
                {
                    crossings = !crossings;
                }
            }

            return crossings;
        }
    }
}
