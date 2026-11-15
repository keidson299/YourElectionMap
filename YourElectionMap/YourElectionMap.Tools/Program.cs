using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace YourElectionMap.Tools
{
    /// <summary>
    /// Builds a state's district file for the server from the Census Bureau's TIGERweb
    /// layers: <c>tiger &lt;state&gt; &lt;layers folder&gt; &lt;output file&gt;</c>. Run by
    /// hand when a plan changes; see DATA-SOURCE.md.
    /// </summary>
    public static class Program
    {
        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // Names as written ("Doña Ana County"): the file is never embedded in HTML.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static int Main(string[] args)
        {
            if (args is not ["tiger", var state, var folder, var output])
            {
                Console.Error.WriteLine("Usage: YourElectionMap.Tools tiger <state> <layers folder> <output .json>");
                return 1;
            }

            Built built;

            try
            {
                built = Directory.Exists(folder)
                    ? Tiger.Build(state, folder)
                    : throw new InvalidDataException($"No such folder: {folder}.");
            }
            catch (InvalidDataException problem)
            {
                Console.Error.WriteLine($"{problem.Message} Not writing the file.");
                return 1;
            }

            Write(output, built);

            var points = built.Districts.Sum(district => district.Rings.Sum(ring => ring.Length / 2));

            Console.WriteLine(
                $"{built.Districts.Count} districts, {points:N0} points, "
                + $"{new FileInfo(output).Length / 1024 / 1024.0:N1} MB: {Path.GetFullPath(output)}");

            return 0;
        }

        /// <summary>
        /// Degrees to five places, about a metre: finer than the fifteen feet the rings
        /// are thinned to, and what the server sends. Two runs of the same layers write
        /// the same bytes, apart from the date.
        /// </summary>
        private static void Write(string output, Built built)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);

            using var file = File.Create(output);

            JsonSerializer.Serialize(
                file,
                new
                {
                    Generated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    built.State,
                    Units = "degrees",
                    Districts = built.Districts.Select(district =>
                    {
                        var rings = district.Rings.ConvertAll(ring => Array.ConvertAll(ring, value => Math.Round(value, 5)));

                        return new
                        {
                            district.Id,
                            district.Level,
                            district.Office,
                            district.Number,
                            district.Detail,
                            Bbox = Box(rings),
                            Rings = rings,
                        };
                    }),
                },
                WriteOptions);
        }

        /// <summary>The <c>[minX, minY, maxX, maxY]</c> box around every ring, which the server checks first.</summary>
        private static double[] Box(List<double[]> rings)
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

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
    }

    public sealed record Built(string State, List<BuiltDistrict> Districts);

    /// <summary>One district, its rings flat <c>[longitude, latitude, …]</c> and thinned.</summary>
    public sealed record BuiltDistrict(
        string Id,
        string Level,
        string Office,
        string Number,
        string Detail,
        List<double[]> Rings);
}
