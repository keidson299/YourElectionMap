namespace YourElectionMap.Tools
{
    /// <summary>
    /// Reads polygons out of an ESRI shapefile pair (<c>.shp</c> with its <c>.shx</c>
    /// index). Only what this project needs: shape type 5, polygons, no Z or M values.
    /// <para>
    /// The format is small enough to read directly. A <c>.shp</c> is a 100 byte header
    /// then one record per feature; the header and each record's own header count in
    /// big-endian 16-bit words, while everything inside a record is little-endian. The
    /// <c>.shx</c> is the same header followed by an eight byte offset-and-length pair
    /// per feature, which is what makes the records reachable without a full scan.
    /// </para>
    /// </summary>
    public sealed class ShapefileReader : IDisposable
    {
        private const int PolygonShapeType = 5;
        private const int HeaderLength = 100;

        private readonly FileStream _shapes;
        private readonly byte[] _index;

        public ShapefileReader(string shapefilePath)
        {
            var withoutExtension = Path.ChangeExtension(shapefilePath, null);

            _index = File.ReadAllBytes(withoutExtension + ".shx");
            _shapes = File.OpenRead(withoutExtension + ".shp");

            Count = (_index.Length - HeaderLength) / 8;
        }

        /// <summary>How many features the index lists.</summary>
        public int Count { get; }

        /// <summary>
        /// One feature's rings, as flat <c>[x, y, x, y, …]</c> arrays in the file's own
        /// coordinates. A shapefile closes each ring by repeating its first point, and
        /// that is left as it is.
        /// </summary>
        public List<double[]> ReadRings(int feature)
        {
            var at = HeaderLength + feature * 8;
            var offset = (long)BigEndianInt32(_index, at) * 2;
            var length = BigEndianInt32(_index, at + 4) * 2;

            var record = new byte[length];
            _shapes.Position = offset + 8;
            _shapes.ReadExactly(record);

            var shapeType = BitConverter.ToInt32(record, 0);

            if (shapeType != PolygonShapeType)
            {
                throw new InvalidDataException(
                    $"Feature {feature} is shape type {shapeType}, not a polygon.");
            }

            // 4 bytes of shape type, then the feature's own 32 byte box.
            var partCount = BitConverter.ToInt32(record, 36);
            var pointCount = BitConverter.ToInt32(record, 40);
            var firstPoint = 44 + partCount * 4;

            var rings = new List<double[]>(partCount);

            for (var part = 0; part < partCount; part++)
            {
                var start = BitConverter.ToInt32(record, 44 + part * 4);
                var end = part < partCount - 1
                    ? BitConverter.ToInt32(record, 44 + (part + 1) * 4)
                    : pointCount;

                var ring = new double[(end - start) * 2];

                for (var point = start; point < end; point++)
                {
                    var from = firstPoint + point * 16;
                    ring[(point - start) * 2] = BitConverter.ToDouble(record, from);
                    ring[(point - start) * 2 + 1] = BitConverter.ToDouble(record, from + 8);
                }

                rings.Add(ring);
            }

            return rings;
        }

        public void Dispose() => _shapes.Dispose();

        private static int BigEndianInt32(byte[] bytes, int at) =>
            (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
    }
}
