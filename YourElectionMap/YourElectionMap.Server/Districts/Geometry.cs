namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// Containment, distance and polygon sorting over rings stored the way the district
    /// files store them: flat, closed <c>[x, y, x, y, …]</c> arrays.
    /// </summary>
    public static class Geometry
    {
        /// <summary>
        /// Even-odd rather than winding: the file keeps a district's rings loose, with no
        /// promise about which way they were wound, so an island and a hole look the same
        /// until the crossings are counted.
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

        /// <summary>A cheap "no" for nearly every district before <see cref="Covers"/>.</summary>
        public static bool BoxCovers(double[] box, double x, double y) =>
            x >= box[0] && x <= box[2] && y >= box[1] && y <= box[3];

        /// <summary>
        /// Distance to the nearest edge, in degrees of latitude (about 364,000 feet), with
        /// longitude scaled by the cosine of the latitude so every direction measures alike.
        /// </summary>
        public static double DistanceToRings(IReadOnlyList<double[]> rings, double x, double y)
        {
            var scale = Math.Cos(y * Math.PI / 180);
            var nearest = double.MaxValue;

            foreach (var ring in rings)
            {
                var count = ring.Length / 2;

                for (int i = 0, j = count - 1; i < count; j = i++)
                {
                    // The edge from j to i, with the point at the origin.
                    var ax = (ring[j * 2] - x) * scale;
                    var ay = ring[j * 2 + 1] - y;
                    var dx = (ring[i * 2] - x) * scale - ax;
                    var dy = ring[i * 2 + 1] - y - ay;
                    var lengthSquared = dx * dx + dy * dy;
                    var along = lengthSquared == 0
                        ? 0
                        : Math.Clamp(-(ax * dx + ay * dy) / lengthSquared, 0, 1);
                    var px = ax + along * dx;
                    var py = ay + along * dy;

                    nearest = Math.Min(nearest, px * px + py * py);
                }
            }

            return Math.Sqrt(nearest);
        }

        /// <summary>
        /// Sorts loose rings into GeoJSON polygons, outer ring first, wound
        /// counter-clockwise, then its holes, clockwise. A ring inside an even number of
        /// others is an outer one (an island in a lake again), and one inside an odd
        /// number is a hole in the innermost of them.
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
        /// A district's rings never cross, so one vertex settles it: any but one the two
        /// share, which would sit on the outer ring's edge and answer either way.
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

        /// <summary>A ray cast east; the closing repeat of the first point is an edge of no length.</summary>
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
