using System.Text;

namespace YourElectionMap.Tools
{
    /// <summary>
    /// Reads the attribute table beside a shapefile: a dBASE III <c>.dbf</c>, which is
    /// a header, a list of fixed-width field descriptors, and then one fixed-width
    /// record per feature in the same order as the shapes.
    /// <para>
    /// The whole file is held in memory. The largest of these is 15MB, and the
    /// alternative is seeking twice per field across two and a half thousand records.
    /// </para>
    /// </summary>
    public sealed class DbaseReader
    {
        private const int FieldDescriptorLength = 32;

        private readonly byte[] _bytes;
        private readonly int _firstRecord;
        private readonly int _recordLength;
        private readonly Field[] _fields;

        public DbaseReader(string shapefilePath)
        {
            _bytes = File.ReadAllBytes(Path.ChangeExtension(shapefilePath, ".dbf"));

            Count = BitConverter.ToInt32(_bytes, 4);
            _firstRecord = BitConverter.ToInt16(_bytes, 8);
            _recordLength = BitConverter.ToInt16(_bytes, 10);

            // The descriptors fill the header after its own 32 bytes, less the 0x0D that
            // terminates them.
            var fieldCount = (_firstRecord - FieldDescriptorLength - 1) / FieldDescriptorLength;
            _fields = new Field[fieldCount];
            var offset = 0;

            for (var i = 0; i < fieldCount; i++)
            {
                var at = FieldDescriptorLength + i * FieldDescriptorLength;
                var name = Encoding.Latin1.GetString(_bytes, at, 11).TrimEnd('\0').Trim();
                var length = _bytes[at + 16];

                _fields[i] = new Field(name, offset, length);
                offset += length;
            }
        }

        /// <summary>How many records the header declares.</summary>
        public int Count { get; }

        /// <summary>Every field name, in the order the records store them.</summary>
        public IReadOnlyList<string> FieldNames =>
            Array.ConvertAll(_fields, field => field.Name);

        /// <summary>One field of one record, trimmed. Unknown names throw.</summary>
        public string Text(int record, string fieldName)
        {
            foreach (var field in _fields)
            {
                if (field.Name == fieldName)
                {
                    return Text(record, field);
                }
            }

            throw new ArgumentException($"No field named '{fieldName}'.", nameof(fieldName));
        }

        /// <summary>
        /// The names of the fields holding a number above zero in this record. The
        /// precinct files carry no district column, so which contest a precinct voted
        /// in is the only thing that says which district it belongs to.
        /// </summary>
        public List<string> FieldsWithVotes(int record, string prefix)
        {
            var found = new List<string>();

            foreach (var field in _fields)
            {
                if (!field.Name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (double.TryParse(Text(record, field), out var votes) && votes > 0)
                {
                    found.Add(field.Name);
                }
            }

            return found;
        }

        private string Text(int record, Field field)
        {
            // Each record opens with a one byte deletion flag.
            var at = _firstRecord + record * _recordLength + 1 + field.Offset;

            // The .cpg beside these files names ISO-8859-1.
            return Encoding.Latin1.GetString(_bytes, at, field.Length).Trim();
        }

        private readonly record struct Field(string Name, int Offset, int Length);
    }
}
