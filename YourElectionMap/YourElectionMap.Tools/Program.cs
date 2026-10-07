using System.Globalization;
using System.Text;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Tools
{
    /// <summary>
    /// Builds the district file the server serves, out of the North Carolina precinct
    /// shapefiles. Run by hand, and again only when the boundaries change. The output is
    /// git-ignored. See DATA-SOURCE.md.
    /// <para>
    /// <c>dotnet run --project YourElectionMap.Tools -- &lt;shapefile folder&gt; &lt;output file&gt;</c>
    /// </para>
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// What the file keeps. Finer than the map draws, because the server also answers
        /// "which district is this address in" from these same rings.
        /// </summary>
        private const double StoredToleranceFeet = 15;

        /// <summary>
        /// Rings narrower than this are dropped: the slivers the dissolve leaves where
        /// neighbouring precincts disagree about a border by a few feet. No real island
        /// is anywhere near this small.
        /// </summary>
        private const double SmallestRingFeet = 200;

        /// <summary>
        /// The three layers, and what each one's districts are called. The precinct
        /// files carry no district column, so the district comes from which contest the
        /// precinct recorded votes in: <c>GCON07RROU</c> is US House 7. The README that
        /// came with the data documents CONG_DIST, SLDU_DIST and SLDL_DIST columns that
        /// are not actually there.
        /// </summary>
        private static readonly Layer[] Layers =
        [
            new("nc_2024_gen_cong_prec", "GCON", 2, "congressional", "U.S. House", "Federal"),
            new("nc_2024_gen_sldu_prec", "GSU", 2, "state-upper", "State Senate", "State"),
            new("nc_2024_gen_sldl_prec", "GSL", 3, "state-lower", "State House", "State"),
        ];

        public static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine(
                    "Usage: YourElectionMap.Tools <shapefile folder> <output .json>");

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

            foreach (var layer in Layers)
            {
                districts.AddRange(Build(layer, source));
            }

            var missing = districts.FindAll(district => district.Rings.Count == 0);

            if (missing.Count > 0)
            {
                Console.Error.WriteLine(
                    $"{missing.Count} districts dissolved to nothing, starting with "
                    + $"{missing[0].Id}. Not writing the file.");

                return 1;
            }

            Write(output, districts);

            var points = 0;

            foreach (var district in districts)
            {
                foreach (var ring in district.Rings)
                {
                    points += ring.Length / 2;
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                $"{districts.Count} districts, {points:N0} points, "
                + $"{new FileInfo(output).Length / 1024 / 1024.0:N1} MB");
            Console.WriteLine(Path.GetFullPath(output));

            return 0;
        }

        private static List<Built> Build(Layer layer, string source)
        {
            var path = Path.Combine(source, layer.Folder, layer.Folder + ".shp");

            using var shapes = new ShapefileReader(path);
            var table = new DbaseReader(path);

            if (shapes.Count != table.Count)
            {
                throw new InvalidDataException(
                    $"{layer.Folder}: {shapes.Count} shapes against {table.Count} records.");
            }

            // Every precinct of a district, gathered before any of it is dissolved.
            var byDistrict = new SortedDictionary<string, List<double[]>>();
            var unplaced = 0;

            for (var feature = 0; feature < shapes.Count; feature++)
            {
                var number = DistrictOf(table, feature, layer);

                if (number is null)
                {
                    unplaced++;
                    continue;
                }

                if (!byDistrict.TryGetValue(number, out var polygons))
                {
                    polygons = [];
                    byDistrict[number] = polygons;
                }

                polygons.AddRange(shapes.ReadRings(feature));
            }

            Console.WriteLine(
                $"{layer.Folder}: {shapes.Count} precincts, {byDistrict.Count} districts"
                + (unplaced == 0 ? "" : $", {unplaced} precincts with no single district"));

            var built = new List<Built>(byDistrict.Count);

            foreach (var (number, polygons) in byDistrict)
            {
                built.Add(Build(layer, number, polygons));
            }

            return built;
        }

        private static Built Build(Layer layer, string number, List<double[]> polygons)
        {
            var dissolved = Dissolve.Rings(polygons);
            var rings = new List<double[]>(dissolved.Rings.Count);
            var kept = 0;
            var slivers = dissolved.DroppedRings;

            foreach (var ring in dissolved.Rings)
            {
                // Thinned before it is measured: a sliver four hundred feet long and one
                // foot wide passes a width test and then collapses to a mark.
                var thinned = Geometry.Thin(ToMapUnits(ring), ToMapUnits(StoredToleranceFeet));

                if (Width(thinned) < ToMapUnits(SmallestRingFeet))
                {
                    slivers++;
                    continue;
                }

                rings.Add(thinned);
                kept += thinned.Length / 2;
            }

            var plain = number.TrimStart('0');

            Console.WriteLine(
                $"  {layer.Level}-{plain,-3} {polygons.Count,4} parts "
                + $"{dissolved.Vertices,8:N0} vertices -> {dissolved.OutlineEdges,7:N0} outline "
                + $"{rings.Count,3} rings -> {kept,6:N0} points"
                + (slivers == 0 ? "" : $"  {slivers} slivers")
                + (dissolved.Overlaps == 0 ? "" : $"  {dissolved.Overlaps} overlaps"));

            return new Built(
                $"{layer.Level}-{plain}",
                layer.Level,
                layer.Office,
                plain,
                layer.Detail,
                rings);
        }

        /// <summary>
        /// Which district this precinct voted in, or null when its votes do not point at
        /// exactly one. The current files have none of those; any that appear are counted
        /// and reported.
        /// </summary>
        private static string? DistrictOf(DbaseReader table, int feature, Layer layer)
        {
            string? found = null;

            foreach (var field in table.FieldsWithVotes(feature, layer.Prefix))
            {
                if (field.Length < layer.Prefix.Length + layer.Digits)
                {
                    continue;
                }

                var number = field.Substring(layer.Prefix.Length, layer.Digits);

                if (!int.TryParse(number, out _))
                {
                    continue;
                }

                if (found is not null && found != number)
                {
                    return null;
                }

                found = number;
            }

            return found;
        }

        /// <summary>
        /// Feet to the map's own units, flipping north so that the client can draw the
        /// result without transforming it: in SVG, y counts downward.
        /// </summary>
        private static double[] ToMapUnits(double[] ring)
        {
            var converted = new double[ring.Length];

            for (var i = 0; i < ring.Length; i += 2)
            {
                converted[i] = ring[i] / StatePlane.FeetPerMapUnit;
                converted[i + 1] = -ring[i + 1] / StatePlane.FeetPerMapUnit;
            }

            return converted;
        }

        private static double ToMapUnits(double feet) => feet / StatePlane.FeetPerMapUnit;

        /// <summary>The longer side of the ring's box: how much ground it covers at all.</summary>
        private static double Width(double[] ring)
        {
            var box = Geometry.BoxOf([ring]);

            return Math.Max(box[2] - box[0], box[3] - box[1]);
        }

        /// <summary>
        /// Written by hand rather than serialized: one district per line, and numbers to a
        /// hundredth of a unit (about a foot) rather than seventeen digits.
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
            file.WriteLine("  \"source\": \"NC 2024 precincts\",");
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
            string Folder,
            string Prefix,
            int Digits,
            string Level,
            string Office,
            string Detail);

        private sealed record Built(
            string Id,
            string Level,
            string Office,
            string Number,
            string Detail,
            List<double[]> Rings);
    }
}
