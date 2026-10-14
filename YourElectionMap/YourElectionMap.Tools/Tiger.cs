using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Tools
{
    /// <summary>
    /// A state's districts from the Census Bureau's TIGERweb layers, saved as GeoJSON in
    /// longitude and latitude: <c>cd.geojson</c>, <c>sldu.geojson</c> and
    /// <c>sldl.geojson</c> in the folder given, and, where the folder has them, the
    /// county, city and school district layers (<see cref="LocalLayers"/>). DATA-SOURCE.md
    /// has the query that fetches them. TIGER carries no population, so the file has none.
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
            // Thirty districts, each electing a senator and two representatives: TIGER
            // has them in both layers.
            ["AZ"] = new(
                "04",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 9),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 30),
                    new("sldl.geojson", "state-lower", "State House", "State", 30),
                ]),
            ["CA"] = new(
                "06",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 52),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 40),
                    new("sldl.geojson", "state-lower", "State Assembly", "State", 80),
                ]),
            // Thirty-five districts, as in Arizona.
            ["ID"] = new(
                "16",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 2),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 35),
                    new("sldl.geojson", "state-lower", "State House", "State", 35),
                ]),
            ["MT"] = new(
                "30",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 2),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 50),
                    new("sldl.geojson", "state-lower", "State House", "State", 100),
                ]),
            ["NC"] = new(
                "37",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 14),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 50),
                    new("sldl.geojson", "state-lower", "State House", "State", 120),
                ]),
            ["OR"] = new(
                "41",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 6),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 30),
                    new("sldl.geojson", "state-lower", "State House", "State", 60),
                ]),
            ["SC"] = new(
                "45",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 7),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 46),
                    new("sldl.geojson", "state-lower", "State House", "State", 124),
                ]),
            // The court-ordered map of November 2025: TIGER has it in layer 0.
            ["UT"] = new(
                "49",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 4),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 29),
                    new("sldl.geojson", "state-lower", "State House", "State", 75),
                ]),
            // One set of 49 districts, each electing a senator and two representatives:
            // TIGER has it in both layers.
            ["WA"] = new(
                "53",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 10),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 49),
                    new("sldl.geojson", "state-lower", "State House", "State", 49),
                ]),
            // One seat in the House, elected at large: numbered 1 here.
            ["WY"] = new(
                "56",
                [
                    new("cd.geojson", "congressional", "U.S. House", "Federal", 1),
                    new("sldu.geojson", "state-upper", "State Senate", "State", 31),
                    new("sldl.geojson", "state-lower", "State House", "State", 62),
                ]),
        };

        /// <summary>
        /// The local layers, the same for every state: governments with a name rather
        /// than numbered districts. TIGER has the county, the city or town and the school
        /// district, not the commission, council or trustee districts inside them. Each
        /// is optional: a state folder without the file builds without that layer, and a
        /// state with no elementary or secondary districts has nothing in those files.
        /// </summary>
        private static readonly NamedLayer[] LocalLayers =
        [
            // The detail is the rail's second line under the name.
            new("county.geojson", "county", "County"),
            new("place.geojson", "city", "City or town"),
            new("unsd.geojson", "school", "School district"),
            new("elsd.geojson", "school", "Elementary school district"),
            new("scsd.geojson", "school", "High school district"),
        ];

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
                var built = Features(Path.Combine(source, layer.File), layer.File, required: true)
                    .Select(feature => Build(layer, known.Fips, feature))
                    .ToList();

                CheckAndSort(built, layer);
                districts.AddRange(built);
            }

            foreach (var layer in LocalLayers)
            {
                var built = Features(Path.Combine(source, layer.File), layer.File, required: false)
                    .Select(feature => Build(layer, known.Fips, feature))
                    .OfType<BuiltDistrict>()
                    .ToList();

                CheckAndSort(built, layer);
                districts.AddRange(built);
            }

            return new Built(state.ToUpperInvariant(), Source, districts);
        }

        /// <summary>A layer's features, or none for a local layer the folder does not have.</summary>
        private static List<JsonElement> Features(string path, string name, bool required)
        {
            if (!File.Exists(path))
            {
                if (required)
                {
                    throw new InvalidDataException($"No such file: {path}.");
                }

                Console.WriteLine($"{name}: not in the folder, skipped");
                return [];
            }

            using var file = File.OpenRead(path);
            using var json = JsonDocument.Parse(file);
            var root = json.RootElement;

            // TIGERweb cuts an answer short at its record limit and says so here.
            if (root.TryGetProperty("exceededTransferLimit", out var exceeded)
                && exceeded.ValueKind == JsonValueKind.True)
            {
                throw new InvalidDataException($"{name} was cut short by the server.");
            }

            // Cloned, so the features outlive the document.
            return root.GetProperty("features").EnumerateArray()
                .Select(feature => feature.Clone())
                .ToList();
        }

        /// <summary>
        /// Numbered 1 to N, each once, in order: anything else is a plan this was not
        /// written for. Prints a line per district as it goes.
        /// </summary>
        private static void CheckAndSort(List<BuiltDistrict> built, Layer layer)
        {
            var numbers = built.Select(district => int.Parse(district.Number, CultureInfo.InvariantCulture)).Order().ToArray();

            if (!numbers.SequenceEqual(Enumerable.Range(1, layer.Count)))
            {
                throw new InvalidDataException(
                    $"{layer.File}: expected districts 1 to {layer.Count}, "
                    + $"found {numbers.Length}: {string.Join(", ", numbers)}.");
            }

            built.Sort((left, right) => int.Parse(left.Number, CultureInfo.InvariantCulture) - int.Parse(right.Number, CultureInfo.InvariantCulture));

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

            // "Congressional District 7", "Assembly District 7", or a state's one seat,
            // "Congressional District (at Large)".
            var match = TrailingNumber().Match(name);
            string number;

            if (match.Success)
            {
                number = int.Parse(match.Value, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture);
            }
            else if (layer.Count == 1 && name.EndsWith("(at Large)", StringComparison.Ordinal))
            {
                number = "1";
            }
            else
            {
                throw new InvalidDataException($"{layer.File}: no district number in \"{name}\".");
            }

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
        /// Each GEOID once, sorted by it so two runs write the same bytes. Prints a line
        /// per government as it goes.
        /// </summary>
        private static void CheckAndSort(List<BuiltDistrict> built, NamedLayer layer)
        {
            var repeated = built.GroupBy(district => district.Id).FirstOrDefault(ids => ids.Count() > 1);

            if (repeated is not null)
            {
                throw new InvalidDataException($"{layer.File}: {repeated.Key} is in it more than once.");
            }

            built.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));

            foreach (var district in built)
            {
                Console.WriteLine(
                    $"  {district.Id,-18} {district.RawPoints,8:N0} points -> "
                    + $"{district.Rings.Sum(ring => ring.Length / 2),6:N0}  {district.Office}");
            }

            Console.WriteLine($"{layer.File}: {built.Count} {layer.Level} areas");
        }

        /// <summary>
        /// A county, city or school district, named by TIGER's <c>NAME</c> ("Wake County")
        /// and identified by its GEOID, which is stable and URL-safe: "county-37183". It
        /// has no number. Null for the school layers' "not defined" areas, where TIGER
        /// fills land no district of that kind covers.
        /// </summary>
        private static BuiltDistrict? Build(NamedLayer layer, string fips, JsonElement feature)
        {
            var properties = feature.GetProperty("properties");
            var name = Field(properties, "NAME", layer.File);
            var geoid = Field(properties, "GEOID", layer.File);

            if (!geoid.StartsWith(fips, StringComparison.Ordinal) || !geoid.All(char.IsAsciiDigit))
            {
                throw new InvalidDataException(
                    $"{layer.File}: \"{name}\" has GEOID {geoid}, not in state {fips}.");
            }

            if (name.Contains("Not Defined", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  {geoid,-18} skipped: \"{name}\"");
                return null;
            }

            // Written into the file as it is, between quotes.
            if (name.Contains('"') || name.Contains('\\'))
            {
                throw new InvalidDataException($"{layer.File}: \"{name}\" needs escaping.");
            }

            var parts = Rings(feature.GetProperty("geometry"), layer.File, name);

            if (parts.Count == 0)
            {
                throw new InvalidDataException($"{layer.File}: \"{name}\" has no shape.");
            }

            return new BuiltDistrict(
                $"{layer.Level}-{geoid}",
                layer.Level,
                name,
                Number: "",
                layer.Detail,
                parts.Sum(part => part.Length / 2),
                parts.Select(part => Geometry.Thin(part, ToleranceDegrees)).ToList());
        }

        private static string Field(JsonElement properties, string name, string file) =>
            properties.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                && value.GetString() is { Length: > 0 } text
                ? text
                : throw new InvalidDataException(
                    $"{file}: a feature has no {name}. Fetch it with outFields=NAME,GEOID.");

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

        private sealed record NamedLayer(string File, string Level, string Detail);
    }
}
