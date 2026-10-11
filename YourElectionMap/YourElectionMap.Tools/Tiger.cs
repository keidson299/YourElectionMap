using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Tools
{
    /// <summary>
    /// A state's districts from the Census Bureau's TIGERweb Legislative layers, the one
    /// source of every boundary on the site, saved as GeoJSON in
    /// longitude and latitude: <c>cd.geojson</c>, <c>sldu.geojson</c> and
    /// <c>sldl.geojson</c> in the folder given. DATA-SOURCE.md has the query that fetches
    /// them. TIGER carries no population, so the file has none.
    /// </summary>
    public static partial class Tiger
    {
        private const string Source = "U.S. Census Bureau TIGER";

        /// <summary>
        /// Fifteen feet as degrees of latitude. A degree of longitude is shorter, so
        /// east–west this keeps a little more than fifteen feet would, never less.
        /// </summary>
        private const double ToleranceDegrees = Program.StoredToleranceFeet / 364_000;

        /// <summary>The states this knows: FIPS code, then each layer's office and count.</summary>
        private static readonly Dictionary<string, State> States = new(StringComparer.OrdinalIgnoreCase)
        {
            ["CA"] = new(
                "06",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 52),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 40),
                    new("sldl.geojson", "state-lower", "State Assembly", "State", 80),
                ]),
            ["NC"] = new(
                "37",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 14),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 50),
                    new("sldl.geojson", "state-lower", "State House", "State", 120),
                ]),
        };

        public static Built Build(string state, string source)
        {
            if (!States.TryGetValue(state, out var known))
            {
                throw new InvalidDataException(
                    $"No TIGER layers set up for \"{state}\"; known: {string.Join(", ", States.Keys)}.");
            }

            var districts = new List<BuiltDistrict>();

            foreach (var layer in known.Layers)
            {
                districts.AddRange(Build(layer, known.Fips, Path.Combine(source, layer.File)));
            }

            return new Built(state.ToUpperInvariant(), Source, districts);
        }

        private static List<BuiltDistrict> Build(Layer layer, string fips, string path)
        {
            if (!File.Exists(path))
            {
                throw new InvalidDataException($"No such file: {path}.");
            }

            using var file = File.OpenRead(path);
            using var json = JsonDocument.Parse(file);
            var root = json.RootElement;

            // TIGERweb cuts an answer short at its record limit and says so here.
            if (root.TryGetProperty("exceededTransferLimit", out var exceeded)
                && exceeded.ValueKind == JsonValueKind.True)
            {
                throw new InvalidDataException($"{layer.File} was cut short by the server.");
            }

            var built = new List<BuiltDistrict>();

            foreach (var feature in root.GetProperty("features").EnumerateArray())
            {
                built.Add(Build(layer, fips, feature));
            }

            CheckAndSort(built, layer);

            return built;
        }

        /// <summary>
        /// Numbered 1 to N, each once, in order: anything else is a plan this was not
        /// written for. Prints a line per district as it goes.
        /// </summary>
        private static void CheckAndSort(List<BuiltDistrict> built, Layer layer)
        {
            var numbers = built.Select(district => int.Parse(district.Number)).Order().ToArray();

            if (!numbers.SequenceEqual(Enumerable.Range(1, layer.Count)))
            {
                throw new InvalidDataException(
                    $"{layer.File}: expected districts 1 to {layer.Count}, "
                    + $"found {numbers.Length}: {string.Join(", ", numbers)}.");
            }

            built.Sort((left, right) => int.Parse(left.Number) - int.Parse(right.Number));

            foreach (var district in built)
            {
                Console.WriteLine(
                    $"  {district.Id,-18} {district.RawPoints,8:N0} points -> "
                    + $"{district.Rings.Sum(ring => ring.Length / 2),6:N0}");
            }

            Console.WriteLine($"{layer.Level}: {built.Count} districts");
        }

        private static BuiltDistrict Build(Layer layer, string fips, JsonElement feature)
        {
            var properties = feature.GetProperty("properties");
            var name = properties.GetProperty("NAME").GetString() ?? "";
            var geoid = properties.GetProperty("GEOID").GetString() ?? "";

            if (!geoid.StartsWith(fips, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"{layer.File}: \"{name}\" has GEOID {geoid}, not in state {fips}.");
            }

            // "Congressional District 7", "Assembly District 7".
            var match = TrailingNumber().Match(name);

            if (!match.Success)
            {
                throw new InvalidDataException($"{layer.File}: no district number in \"{name}\".");
            }

            var number = int.Parse(match.Value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture);
            var parts = Rings(feature.GetProperty("geometry"), layer.File, name);

            if (parts.Count == 0)
            {
                throw new InvalidDataException($"{layer.File}: \"{name}\" has no shape.");
            }

            return new BuiltDistrict(
                $"{layer.Level}-{number}",
                layer.Level,
                layer.Office,
                number,
                layer.Detail,
                parts.Sum(part => part.Length / 2),
                parts.Select(part => Geometry.Thin(part, ToleranceDegrees)).ToList());
        }

        /// <summary>
        /// Every ring of a Polygon or MultiPolygon, loose: the server works out which is
        /// a hole by counting.
        /// </summary>
        private static List<double[]> Rings(JsonElement geometry, string file, string name)
        {
            var polygons = geometry.GetProperty("type").GetString() switch
            {
                "Polygon" => new List<JsonElement> { geometry.GetProperty("coordinates") },
                "MultiPolygon" => geometry.GetProperty("coordinates").EnumerateArray().ToList(),
                var other => throw new InvalidDataException(
                    $"{file}: \"{name}\" is a {other}, not a polygon."),
            };

            var rings = new List<double[]>();

            foreach (var polygon in polygons)
            {
                foreach (var ring in polygon.EnumerateArray())
                {
                    var flat = new double[ring.GetArrayLength() * 2];
                    var i = 0;

                    foreach (var point in ring.EnumerateArray())
                    {
                        var longitude = point[0].GetDouble();
                        var latitude = point[1].GetDouble();

                        // A layer fetched without outSR=4326 comes back in Web Mercator
                        // metres, which would otherwise pass for a very odd state.
                        if (Math.Abs(longitude) > 180 || Math.Abs(latitude) > 90)
                        {
                            throw new InvalidDataException(
                                $"{file}: \"{name}\" is not in degrees ({longitude}, {latitude}).");
                        }

                        flat[i++] = longitude;
                        flat[i++] = latitude;
                    }

                    rings.Add(flat);
                }
            }

            return rings;
        }

        [GeneratedRegex(@"\d+(?=\s*$)")]
        private static partial Regex TrailingNumber();

        private sealed record State(string Fips, Layer[] Layers);

        private sealed record Layer(
            string File,
            string Level,
            string Office,
            string Detail,
            int Count);
    }
}
