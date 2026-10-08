using System.Text;

namespace YourElectionMap.Tools
{
    /// <summary>
    /// Reads the attribute table beside a shapefile: a dBASE III <c>.dbf</c>, which is
    /// a header, a list of fixed-width field descriptors, and then one fixed-width
    /// record per feature in the same order as the shapes.
    /// <para>
    /// The whole file is held in memory; the district plans' tables are a few
    /// kilobytes.
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

        /// <summary>
        /// One field of one record, trimmed. A field the table does not have throws
        /// rather than reading as empty, so a renamed column stops the run.
        /// </summary>
        public string Text(int record, string field)
        {
            foreach (var candidate in _fields)
            {
                if (candidate.Name == field)
                {
                    return Text(record, candidate);
                }
            }

            throw new InvalidDataException(
                $"No field {field}; the table has {string.Join(", ", _fields.Select(f => f.Name))}.");
        }

        private string Text(int record, Field field)
        {
            // Each record opens with a one byte deletion flag.
            var at = _firstRecord + record * _recordLength + 1 + field.Offset;

            // The .cpg beside each plan's files names UTF-8.
            return Encoding.UTF8.GetString(_bytes, at, field.Length).Trim();
        }

        private readonly record struct Field(string Name, int Offset, int Length);
    }
}
