using System.Globalization;
using System.Text;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Tools
{
    /// <summary>
    /// Builds the district file the server serves, out of the North Carolina General
    /// Assembly's enacted district plans. Run by hand, and again only when a plan
    /// changes. The output is git-ignored. See DATA-SOURCE.md.
    /// <para>
    /// <c>dotnet run --project YourElectionMap.Tools -- &lt;MapShapeFiles folder&gt; &lt;output file&gt;</c>
    /// </para>
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// What the file keeps. The server answers "which district is this address in"
        /// from these rings and sends them to the page as they are, so this is also
        /// the finest the map ever draws a boundary.
        /// </summary>
        private const double StoredToleranceFeet = 15;

        /// <summary>US survey feet in a metre, exactly, by its definition.</summary>
        private const double FeetPerMetre = 3937.0 / 1200.0;

        private const string Source = "NCGA enacted maps";

        /// <summary>
        /// The three plans, where each sits under the folder given, and how many
        /// districts it must have.
        /// </summary>
        private static readonly Layer[] Layers =
        [
            new(
                Path.Combine("nc_cong_adopted_2025", "NCGA_CCM-2 - Shapefile", "CCM-2.shp"),
                "congressional", "U.S. House", "Federal", 14),
            new(
                Path.Combine("nc_sldu_adopted_2023", "SL 2023-146 Senate - Shapefile", "SL 2023-146.shp"),
                "state-upper", "State Senate", "State", 50),
            new(
                Path.Combine("nc_sldl_adopted_2023", "SL 2023-149 House - Shapefile", "SL 2023-149.shp"),
                "state-lower", "State House", "State", 120),
        ];

        public static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine(
                    "Usage: YourElectionMap.Tools <MapShapeFiles folder> <output .json>");

                return 1;
            }

            var source = args[0];
            var output = args[1];

            if (!Directory.Exists(source))
            {
                Console.Error.WriteLine($"No such folder: {source}");

                return 1;
            }

            var districts = new List<Built>();

            try
            {
                foreach (var layer in Layers)
                {
                    districts.AddRange(Build(layer, source));
                }
            }
            catch (InvalidDataException problem)
            {
                Console.Error.WriteLine($"{problem.Message} Not writing the file.");

                return 1;
            }

            Write(output, districts);

            var points = districts.Sum(district => district.Rings.Sum(ring => ring.Length / 2));

            Console.WriteLine();
            Console.WriteLine(
                $"{districts.Count} districts, {points:N0} points, "
                + $"{new FileInfo(output).Length / 1024 / 1024.0:N1} MB");
            Console.WriteLine(Path.GetFullPath(output));

            return 0;
        }

        private static List<Built> Build(Layer layer, string source)
        {
            var path = Path.Combine(source, layer.Path);

            if (!File.Exists(path))
            {
                throw new InvalidDataException($"No such shapefile: {path}.");
            }

            CheckProjection(path);

            using var shapes = new ShapefileReader(path);
            var table = new DbaseReader(path);

            if (shapes.Count != table.Count)
            {
                throw new InvalidDataException(
                    $"{layer.Path}: {shapes.Count} shapes against {table.Count} records.");
            }

            var built = new List<Built>(shapes.Count);

            for (var feature = 0; feature < shapes.Count; feature++)
            {
                built.Add(Build(layer, table, feature, shapes.ReadRings(feature)));
            }

            // Numbered 1 to N, each once: anything else is a plan this was not written for.
            var numbers = built.Select(district => int.Parse(district.Number)).Order().ToArray();

            if (!numbers.SequenceEqual(Enumerable.Range(1, layer.Count)))
            {
                throw new InvalidDataException(
                    $"{layer.Path}: expected districts 1 to {layer.Count}, "
                    + $"found {numbers.Length}: {string.Join(", ", numbers)}.");
            }

            built.Sort((left, right) => int.Parse(left.Number) - int.Parse(right.Number));

            foreach (var district in built)
            {
                Console.WriteLine(
                    $"  {district.Id,-18} {district.RawPoints,8:N0} points -> "
                    + $"{district.Rings.Sum(ring => ring.Length / 2),6:N0}  "
                    + $"{district.Population,9:N0} residents");
            }

            Console.WriteLine($"{layer.Level}: {built.Count} districts");

            return built;
        }

        private static Built Build(Layer layer, DbaseReader table, int feature, List<double[]> parts)
        {
            var number = table.Text(feature, "DISTRICT");

            if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new InvalidDataException(
                    $"{layer.Path}: feature {feature} has district \"{number}\".");
            }

            var population = table.Text(feature, "PL20AA_TOT");

            if (!int.TryParse(population, NumberStyles.None, CultureInfo.InvariantCulture, out var residents))
            {
                throw new InvalidDataException(
                    $"{layer.Path}: district {parsed} has population \"{population}\".");
            }

            var rings = new List<double[]>(parts.Count);

            foreach (var part in parts)
            {
                rings.Add(Geometry.Thin(ToMapUnits(part), StoredToleranceFeet / StatePlane.FeetPerMapUnit));
            }

            if (rings.Count == 0)
            {
                throw new InvalidDataException($"{layer.Path}: district {parsed} has no shape.");
            }

            var plain = parsed.ToString(CultureInfo.InvariantCulture);

            return new Built(
                $"{layer.Level}-{plain}",
                layer.Level,
                layer.Office,
                plain,
                layer.Detail,
                residents,
                parts.Sum(part => part.Length / 2),
                rings);
        }

        /// <summary>
        /// The plans are in NC State Plane in metres. The precinct files the site used
        /// to be built from were the same projection in feet, so a plan in any other
        /// unit would land a few times too small and still look plausible: stop instead.
        /// </summary>
        private static void CheckProjection(string path)
        {
            var prj = Path.ChangeExtension(path, ".prj");

            if (!File.Exists(prj))
            {
                throw new InvalidDataException($"{path} has no .prj beside it.");
            }

            var text = File.ReadAllText(prj);

            // The projection's own unit is the last UNIT in the text; the earlier one
            // belongs to the geographic system underneath.
            var unit = text[text.LastIndexOf("UNIT[", StringComparison.Ordinal)..];

            if (!text.Contains("North_Carolina_FIPS_3200", StringComparison.Ordinal)
                || !unit.StartsWith("UNIT[\"Meter\"", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"{prj} is not NC State Plane (FIPS 3200) in metres.");
            }
        }

        /// <summary>
        /// Metres to the map's own units, by way of the US survey feet those units are
        /// counted in, flipping north so that y counts downward like the rest of the file.
        /// </summary>
        private static double[] ToMapUnits(double[] ring)
        {
            var converted = new double[ring.Length];

            for (var i = 0; i < ring.Length; i += 2)
            {
                converted[i] = ring[i] * FeetPerMetre / StatePlane.FeetPerMapUnit;
                converted[i + 1] = -ring[i + 1] * FeetPerMetre / StatePlane.FeetPerMapUnit;
            }

            return converted;
        }

        /// <summary>
        /// Written by hand rather than serialized: one district per line, and numbers to a
        /// hundredth of a unit (about a foot) rather than seventeen digits. Two runs of the
        /// same plans write the same bytes, apart from the date.
        /// </summary>
        private static void Write(string output, List<Built> districts)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(output));

            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            using var file = new StreamWriter(output, false, new UTF8Encoding(false));

            file.WriteLine("{");
            file.WriteLine($"  \"source\": \"{Source}\",");
            file.WriteLine(
                $"  \"generated\": \"{DateTime.UtcNow:yyyy-MM-dd}\",");
            file.WriteLine(
                "  \"unitFeet\": "
                + StatePlane.FeetPerMapUnit.ToString(CultureInfo.InvariantCulture)
                + ",");
            file.WriteLine("  \"districts\": [");

            for (var i = 0; i < districts.Count; i++)
            {
                var district = districts[i];
                var line = new StringBuilder("    {");

                line.Append($"\"id\": \"{district.Id}\", ");
                line.Append($"\"level\": \"{district.Level}\", ");
                line.Append($"\"office\": \"{district.Office}\", ");
                line.Append($"\"number\": \"{district.Number}\", ");
                line.Append($"\"detail\": \"{district.Detail}\", ");
                line.Append($"\"population\": {district.Population.ToString(CultureInfo.InvariantCulture)}, ");
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

                if (i < districts.Count - 1)
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

                line.Append(numbers[i].ToString("0.##", CultureInfo.InvariantCulture));
            }
        }

        private sealed record Layer(
            string Path,
            string Level,
            string Office,
            string Detail,
            int Count);

        private sealed record Built(
            string Id,
            string Level,
            string Office,
            string Number,
            string Detail,
            int Population,
            int RawPoints,
            List<double[]> Rings);
    }
}
