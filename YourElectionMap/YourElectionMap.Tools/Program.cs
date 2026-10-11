using System.Globalization;
using System.Text;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Tools
{
    /// <summary>
    /// Builds a state's district file for the server from the Census Bureau's TIGERweb
    /// layers (<see cref="Tiger"/>). Run by hand, and again only when a plan changes. The
    /// output is git-ignored. See DATA-SOURCE.md.
    /// <para>
    /// <c>tiger &lt;state&gt; &lt;layers folder&gt; &lt;output file&gt;</c>
    /// </para>
    /// The file holds rings in longitude and latitude, which is what the server tests an
    /// address against and what it sends the page.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// What the file keeps. The server answers "which district is this address in"
        /// from these rings and sends them to the page as they are, so this is also
        /// the finest the map ever draws a boundary.
        /// </summary>
        public const double StoredToleranceFeet = 15;

        private const string Usage =
            "Usage: YourElectionMap.Tools tiger <state> <layers folder> <output .json>";

        public static int Main(string[] args)
        {
            Built built;
            string output;

            try
            {
                switch (args)
                {
                    case ["tiger", var state, var source, var file]:
                        built = Tiger.Build(state, Folder(source));
                        output = file;
                        break;
                    default:
                        Console.Error.WriteLine(Usage);
                        return 1;
                }
            }
            catch (InvalidDataException problem)
            {
                Console.Error.WriteLine($"{problem.Message} Not writing the file.");

                return 1;
            }

            Write(output, built);

            var points = built.Districts.Sum(district => district.Rings.Sum(ring => ring.Length / 2));

            Console.WriteLine();
            Console.WriteLine(
                $"{built.Districts.Count} districts, {points:N0} points, "
                + $"{new FileInfo(output).Length / 1024 / 1024.0:N1} MB");
            Console.WriteLine(Path.GetFullPath(output));

            return 0;
        }

        private static string Folder(string path) =>
            Directory.Exists(path)
                ? path
                : throw new InvalidDataException($"No such folder: {path}.");

        /// <summary>
        /// Written by hand rather than serialized: one district per line, and degrees to
        /// six places (about four inches) rather than seventeen digits. Two runs of the
        /// same plans write the same bytes, apart from the date.
        /// </summary>
        private static void Write(string output, Built built)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(output));

            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            using var file = new StreamWriter(output, false, new UTF8Encoding(false));

            file.WriteLine("{");
            file.WriteLine($"  \"source\": \"{built.Source}\",");
            file.WriteLine($"  \"generated\": \"{DateTime.UtcNow:yyyy-MM-dd}\",");
            file.WriteLine($"  \"state\": \"{built.State}\",");
            file.WriteLine("  \"units\": \"degrees\",");
            file.WriteLine("  \"districts\": [");

            for (var i = 0; i < built.Districts.Count; i++)
            {
                var district = built.Districts[i];
                var line = new StringBuilder("    {");

                line.Append($"\"id\": \"{district.Id}\", ");
                line.Append($"\"level\": \"{district.Level}\", ");
                line.Append($"\"office\": \"{district.Office}\", ");
                line.Append($"\"number\": \"{district.Number}\", ");
                line.Append($"\"detail\": \"{district.Detail}\", ");
                line.Append("\"bbox\": [");
                Append(line, Geometry.BoxOf(district.Rings));
                line.Append("], \"rings\": [");

                for (var r = 0; r < district.Rings.Count; r++)
                {
                    if (r > 0)
                    {
                        line.Append(", ");
                    }

                    line.Append('[');
                    Append(line, district.Rings[r]);
                    line.Append(']');
                }

                line.Append("]}");

                if (i < built.Districts.Count - 1)
                {
                    line.Append(',');
                }

                file.WriteLine(line);
            }

            file.WriteLine("  ]");
            file.WriteLine("}");
        }

        private static void Append(StringBuilder line, double[] numbers)
        {
            for (var i = 0; i < numbers.Length; i++)
            {
                if (i > 0)
                {
                    line.Append(',');
                }

                line.Append(numbers[i].ToString("0.######", CultureInfo.InvariantCulture));
            }
        }
    }

    /// <summary>A whole file: whose districts, credited to what.</summary>
    public sealed record Built(string State, string Source, List<BuiltDistrict> Districts);

    /// <summary>One district, its rings flat <c>[longitude, latitude, …]</c> and already thinned.</summary>
    public sealed record BuiltDistrict(
        string Id,
        string Level,
        string Office,
        string Number,
        string Detail,
        int RawPoints,
        List<double[]> Rings);
}
