using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YourElectionMap.Tools
{
    /// <summary>
    /// A state's districts from TIGERweb layers saved as GeoJSON in degrees: the
    /// legislative layers, required, and the local layers, where the folder has them.
    /// DATA-SOURCE.md has the queries that fetch them.
    /// </summary>
    public static partial class Tiger
    {
        /// <summary>
        /// Fifteen feet as degrees of latitude: the finest the map ever draws a boundary.
        /// A degree of longitude is shorter, so east–west this keeps a little more.
        /// </summary>
        private const double ToleranceDegrees = 15 / 364_000.0;

        /// <summary>FIPS code, then each layer's office and count. DATA-SOURCE.md explains the oddities.</summary>
        private static readonly Dictionary<string, State> States = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AK"] = new("02", [UsHouse(1), Senate(20) with { Lettered = true }, House(40)], County: "Borough or census area"),
            ["AL"] = new("01", [UsHouse(7), Senate(35), House(105)]),
            ["AR"] = new("05", [UsHouse(4), Senate(35), House(100)]),
            ["AZ"] = new("04", [UsHouse(9), Senate(30), House(30)]),
            ["CA"] = new("06", [UsHouse(52), Senate(40), House(80, "State Assembly")]),
            ["CO"] = new("08", [UsHouse(8), Senate(35), House(65)]),
            ["CT"] = new("09", [UsHouse(5), Senate(36), House(151)], County: "Planning region"),
            ["DE"] = new("10", [UsHouse(1), Senate(21), House(41)]),
            ["FL"] = new("12", [UsHouse(28), Senate(40), House(120)]),
            ["GA"] = new("13", [UsHouse(14), Senate(56), House(180)]),
            ["HI"] = new("15", [UsHouse(2), Senate(25), House(51)]),
            ["ID"] = new("16", [UsHouse(2), Senate(35), House(35)]),
            ["IA"] = new("19", [UsHouse(4), Senate(50), House(100)]),
            ["IL"] = new("17", [UsHouse(17), Senate(59), House(118)]),
            ["IN"] = new("18", [UsHouse(9), Senate(50), House(100)]),
            ["KS"] = new("20", [UsHouse(4), Senate(40), House(125)]),
            ["KY"] = new("21", [UsHouse(6), Senate(38), House(100)]),
            ["LA"] = new("22", [UsHouse(6), Senate(39), House(105)], County: "Parish"),
            ["MA"] = new("25", [UsHouse(9), Senate(40) with { Named = true }, House(160) with { Named = true }]),
            // Some delegates are elected from halves of a district, 2A and 2B, or thirds, 1A to 1C.
            ["MD"] = new(
                "24",
                [
                    UsHouse(8),
                    Senate(47),
                    House(47, "House of Delegates") with
                    {
                        Halved = [2, 7, 9, 11, 12, 30, 34, 35, 37, 43, 44, 47],
                        InThree = [1, 27, 29, 33, 38, 42],
                    },
                ]),
            ["ME"] = new("23", [UsHouse(2), Senate(35), House(151)]),
            ["MI"] = new("26", [UsHouse(13), Senate(38), House(110)]),
            ["MN"] = new("27", [UsHouse(8), Senate(67), House(67) with { Halved = [.. Enumerable.Range(1, 67)] }]),
            // The 2022 map, TIGER's 119th Congress layer: the courts kept the 2025 map out of 2026.
            ["MO"] = new("29", [UsHouse(8, "cd-119.geojson"), Senate(34), House(163)]),
            ["MS"] = new("28", [UsHouse(4), Senate(52), House(122)]),
            ["MT"] = new("30", [UsHouse(2), Senate(50), House(100)]),
            ["NC"] = new("37", [UsHouse(14), Senate(50), House(120)]),
            ["ND"] = new("38", [UsHouse(1), Senate(47), House(47) with { Halved = [4] }]),
            // One chamber, in TIGER's upper layer; there is no sldl.geojson.
            ["NE"] = new("31", [UsHouse(3), Senate(49, "Legislature")]),
            ["NH"] = new("33", [UsHouse(2), Senate(24), House(164) with { Named = true }]),
            ["NJ"] = new("34", [UsHouse(12), Senate(40), House(40, "General Assembly")]),
            ["NM"] = new("35", [UsHouse(3), Senate(42), House(70)]),
            ["NV"] = new("32", [UsHouse(4), Senate(21), House(42, "State Assembly")]),
            ["NY"] = new("36", [UsHouse(26), Senate(63), House(150, "State Assembly")]),
            ["OH"] = new("39", [UsHouse(15), Senate(33), House(99)]),
            ["OK"] = new("40", [UsHouse(5), Senate(48), House(101)]),
            ["OR"] = new("41", [UsHouse(6), Senate(30), House(60)]),
            ["PA"] = new("42", [UsHouse(17), Senate(50), House(203)]),
            ["RI"] = new("44", [UsHouse(2), Senate(38), House(75)]),
            ["SC"] = new("45", [UsHouse(7), Senate(46), House(124)]),
            ["SD"] = new("46", [UsHouse(1), Senate(35), House(35) with { Halved = [26, 28] }]),
            ["TN"] = new("47", [UsHouse(9), Senate(33), House(99)]),
            ["TX"] = new("48", [UsHouse(38), Senate(31), House(150)]),
            ["UT"] = new("49", [UsHouse(4), Senate(29), House(75)]),
            ["VA"] = new("51", [UsHouse(11), Senate(40), House(100, "House of Delegates")]),
            ["VT"] = new("50", [UsHouse(1), Senate(16) with { Named = true }, House(109) with { Named = true }]),
            ["WA"] = new("53", [UsHouse(10), Senate(49), House(49)]),
            ["WI"] = new("55", [UsHouse(8), Senate(33), House(99, "State Assembly")]),
            ["WV"] = new("54", [UsHouse(2), Senate(17), House(100, "House of Delegates")]),
            ["WY"] = new("56", [UsHouse(1), Senate(31), House(62)]),
        };

        private static Layer UsHouse(int count, string file = "cd.geojson") =>
            new(file, "congressional", "U.S. House", "Federal", count);

        private static Layer Senate(int count, string office = "State Senate") =>
            new("sldu.geojson", "state-upper", office, "State", count);

        private static Layer House(int count, string office = "State House") =>
            new("sldl.geojson", "state-lower", office, "State", count);

        /// <summary>
        /// Governments known by name. Each is optional: a folder without the file builds
        /// without that layer. The detail is the rail's second line under the name.
        /// </summary>
        private static NamedLayer[] LocalLayers(State state) =>
        [
            new("county.geojson", "county", state.County),
            // Towns and townships that govern themselves; the detail comes from their kind.
            new("cousub.geojson", "city", "Town", Towns: true),
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

            List<BuiltDistrict> Read(string file, bool required, Func<JsonElement, BuiltDistrict?> build) =>
                Features(Path.Combine(source, file), file, required)
                    .Select(build)
                    .OfType<BuiltDistrict>()
                    .ToList();

            foreach (var layer in known.Layers)
            {
                var built = Read(layer.File, required: true, feature => Build(layer, known.Fips, feature));

                CheckAndSort(built, layer);
                districts.AddRange(built);
            }

            foreach (var layer in LocalLayers(known))
            {
                var built = Read(layer.File, required: false, feature => Build(layer, known.Fips, feature));

                CheckAndSort(built, layer);
                districts.AddRange(built);
            }

            return new Built(state.ToUpperInvariant(), districts);
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
        /// Numbered 1 to N with halves and thirds in place, or lettered A onwards, or N
        /// named districts each once: anything else is a plan this was not written for.
        /// </summary>
        private static void CheckAndSort(List<BuiltDistrict> built, Layer layer)
        {
            if (layer.Named)
            {
                built.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));

                if (built.Count != layer.Count || built.DistinctBy(district => district.Id).Count() != built.Count)
                {
                    throw new InvalidDataException(
                        $"{layer.File}: expected {layer.Count} named districts, found {built.Count}: "
                        + string.Join(", ", built.Select(district => district.Id)) + ".");
                }
            }
            else
            {
                var expected = layer.Lettered
                    ? Enumerable.Range(0, layer.Count).Select(letter => ((char)('A' + letter)).ToString()).ToList()
                    : Enumerable.Range(1, layer.Count)
                    .SelectMany(number =>
                    {
                        var name = number.ToString(CultureInfo.InvariantCulture);

                        return layer.Halved?.Contains(number) == true ? new[] { name + "A", name + "B" }
                            : layer.InThree?.Contains(number) == true ? [name + "A", name + "B", name + "C"]
                            : [name];
                    })
                    .ToList();

                built.Sort((left, right) => expected.IndexOf(left.Number) - expected.IndexOf(right.Number));

                if (!built.Select(district => district.Number).SequenceEqual(expected))
                {
                    throw new InvalidDataException(
                        $"{layer.File}: expected districts {string.Join(", ", expected)}, "
                        + $"found {built.Count}: {string.Join(", ", built.Select(district => district.Number))}.");
                }
            }

            Console.WriteLine($"{layer.Level}: {built.Count} districts");
        }

        /// <summary>
        /// A numbered, lettered or named district, or null for TIGER's "not defined" area
        /// of water no district takes in: GEOID <c>17ZZ</c> or <c>22ZZZ</c>.
        /// </summary>
        private static BuiltDistrict? Build(Layer layer, string fips, JsonElement feature)
        {
            var properties = feature.GetProperty("properties");
            var name = Field(properties, "NAME", layer.File);
            var geoid = Field(properties, "GEOID", layer.File);

            if (!geoid.StartsWith(fips, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"{layer.File}: \"{name}\" has GEOID {geoid}, not in state {fips}.");
            }

            if (geoid == fips + "ZZ" || geoid == fips + "ZZZ")
            {
                Console.WriteLine($"  {geoid,-18} skipped: \"{name}\"");
                return null;
            }

            // "Congressional District 7", "Assembly District 7", part of a district,
            // "State House District 4A" or "State Legislative Subdistrict 1C", or a
            // state's one seat, "Congressional District (at Large)".
            var match = TrailingNumber().Match(name);
            string number;

            if (layer.Named)
            {
                number = "";
            }
            else if (layer.Lettered)
            {
                // "State Senate District A".
                number = TrailingLetter().Match(name) is { Success: true } letter
                    ? letter.Groups[1].Value
                    : throw new InvalidDataException($"{layer.File}: no district letter in \"{name}\".");
            }
            else if (match.Success)
            {
                number = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture) + match.Groups[2].Value;
            }
            else if (layer.Count == 1 && name.EndsWith("(at Large)", StringComparison.Ordinal))
            {
                number = "1";
            }
            else
            {
                throw new InvalidDataException($"{layer.File}: no district number in \"{name}\".");
            }

            var rings = Shape(feature, layer.File, name);

            // A named district is its name over its chamber, as a county is over "County".
            return layer.Named
                ? new BuiltDistrict($"{layer.Level}-{geoid}", layer.Level, NamedOffice(name), Number: "", layer.Office, rings)
                : new BuiltDistrict($"{layer.Level}-{number}", layer.Level, layer.Office, number, layer.Detail, rings);
        }

        /// <summary>
        /// A named district's name, without the chamber it is shown over: Massachusetts's
        /// as TIGER has it, "Third Suffolk District"; Vermont's "Addison-1 State House
        /// District" and "Addison Senatorial District" as "Addison-1 District" and
        /// "Addison District"; and New Hampshire's "State House District Belknap 01" as
        /// "Belknap District 1".
        /// </summary>
        private static string NamedOffice(string name)
        {
            var inCounty = CountyAndNumber().Match(name);

            if (inCounty.Success)
            {
                return $"{inCounty.Groups[1].Value} District {inCounty.Groups[2].Value}";
            }

            return ChamberSuffix().Replace(name, " District");
        }

        /// <summary>Each GEOID once, sorted by it so two runs write the same bytes.</summary>
        private static void CheckAndSort(List<BuiltDistrict> built, NamedLayer layer)
        {
            var repeated = built.GroupBy(district => district.Id).FirstOrDefault(ids => ids.Count() > 1);

            if (repeated is not null)
            {
                throw new InvalidDataException($"{layer.File}: {repeated.Key} is in it more than once.");
            }

            built.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));

            Console.WriteLine($"{layer.File}: {built.Count} {layer.Level} areas");
        }

        /// <summary>
        /// A county, city or school district: its <c>NAME</c> as the office, no number, and
        /// "county-37183" from its GEOID as the id. Null for a "not defined" area, or a
        /// county subdivision that does not govern itself.
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

            // FUNCSTAT A is an active government and B one sharing its work with another;
            // DATA-SOURCE.md lists the others.
            if (name.Contains("Not Defined", StringComparison.OrdinalIgnoreCase)
                || (layer.Towns && Field(properties, "FUNCSTAT", layer.File) is not ("A" or "B")))
            {
                Console.WriteLine($"  {geoid,-18} skipped: \"{name}\"");
                return null;
            }

            // Townships include charter townships and Kansas's "Township 4"; Maine has plantations.
            var detail = !layer.Towns ? layer.Detail : Field(properties, "LSADC", layer.File) switch
            {
                "43" => "Town",
                "44" or "45" or "49" => "Township",
                "39" => "Plantation",
                var other => throw new InvalidDataException(
                    $"{layer.File}: \"{name}\" is a government of a kind this does not know, LSADC {other}."),
            };

            return new BuiltDistrict($"{layer.Level}-{geoid}", layer.Level, name, Number: "", detail, Shape(feature, layer.File, name));
        }

        /// <summary>A feature's rings, thinned.</summary>
        private static List<double[]> Shape(JsonElement feature, string file, string name)
        {
            var parts = Rings(feature.GetProperty("geometry"), file, name);

            return parts.Count > 0
                ? parts.ConvertAll(part => Thin(part, ToleranceDegrees))
                : throw new InvalidDataException($"{file}: \"{name}\" has no shape.");
        }

        private static string Field(JsonElement properties, string name, string file) =>
            properties.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                && value.GetString() is { Length: > 0 } text
                ? text
                : throw new InvalidDataException(
                    $"{file}: a feature has no {name}. Fetch it with {name} in outFields (DATA-SOURCE.md).");

        /// <summary>Every ring of a Polygon or MultiPolygon, loose: the server works out which is a hole.</summary>
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

                        // A layer fetched without outSR=4326 comes back in Web Mercator metres.
                        if (Math.Abs(longitude) > 180 || Math.Abs(latitude) > 90)
                        {
                            throw new InvalidDataException(
                                $"{file}: \"{name}\" is not in degrees ({longitude}, {latitude}).");
                        }

                        // Alaska's Near Islands, past the 180th meridian, are kept below -180
                        // so a district reaching them is one piece, not the globe's width.
                        flat[i++] = longitude > 0 ? longitude - 360 : longitude;
                        flat[i++] = latitude;
                    }

                    rings.Add(flat);
                }
            }

            return rings;
        }

        [GeneratedRegex(@"(\d+)([ABC]?)\s*$")]
        private static partial Regex TrailingNumber();

        [GeneratedRegex(@" ([A-Z])\s*$")]
        private static partial Regex TrailingLetter();

        [GeneratedRegex(@"^State House District (.+) 0*(\d+)$")]
        private static partial Regex CountyAndNumber();

        [GeneratedRegex(@" (State House|Senatorial) District$")]
        private static partial Regex ChamberSuffix();

        private sealed record State(string Fips, Layer[] Layers, string County = "County");

        /// <summary>
        /// Districts 1 to <paramref name="Count"/>, those in <paramref name="Halved"/> as an A
        /// and a B and those in <paramref name="InThree"/> as A to C; or lettered from A; or named.
        /// </summary>
        private sealed record Layer(
            string File,
            string Level,
            string Office,
            string Detail,
            int Count,
            int[]? Halved = null,
            int[]? InThree = null,
            bool Named = false,
            bool Lettered = false);

        private sealed record NamedLayer(string File, string Level, string Detail, bool Towns = false);

        /// <summary>
        /// Douglas-Peucker: drop every vertex within <paramref name="tolerance"/> of the line
        /// its neighbours already draw, never leaving fewer than four points.
        /// </summary>
        private static double[] Thin(double[] ring, double tolerance)
        {
            var count = ring.Length / 2;

            if (count < 5)
            {
                return ring;
            }

            var keep = new bool[count];
            keep[0] = true;
            keep[count - 1] = true;

            // A ring ends where it began, so the chord between those two points has no
            // length and everything would measure as being on it. Anchoring the vertex
            // furthest from the start gives the halves a chord each.
            var far = FurthestFromStart(ring, count);
            keep[far] = true;

            Thin(ring, keep, 0, far, tolerance);
            Thin(ring, keep, far, count - 1, tolerance);

            var kept = keep.Count(flag => flag);

            if (kept < 4)
            {
                return ring;
            }

            var thinned = new double[kept * 2];
            var next = 0;

            for (var i = 0; i < count; i++)
            {
                if (keep[i])
                {
                    thinned[next++] = ring[i * 2];
                    thinned[next++] = ring[i * 2 + 1];
                }
            }

            return thinned;
        }

        /// <summary>An explicit stack rather than recursion: a boundary can have tens of thousands of vertices.</summary>
        private static void Thin(
            double[] ring,
            bool[] keep,
            int first,
            int last,
            double tolerance)
        {
            var pending = new Stack<(int First, int Last)>();
            pending.Push((first, last));

            while (pending.Count > 0)
            {
                var (from, to) = pending.Pop();

                if (to - from < 2)
                {
                    continue;
                }

                var ax = ring[from * 2];
                var ay = ring[from * 2 + 1];
                var bx = ring[to * 2];
                var by = ring[to * 2 + 1];
                var dx = bx - ax;
                var dy = by - ay;
                var length = Math.Sqrt(dx * dx + dy * dy);

                var worst = -1.0;
                var worstAt = -1;

                for (var i = from + 1; i < to; i++)
                {
                    var px = ring[i * 2];
                    var py = ring[i * 2 + 1];

                    var distance = length == 0
                        ? Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay))
                        : Math.Abs(dy * px - dx * py + bx * ay - by * ax) / length;

                    if (distance > worst)
                    {
                        worst = distance;
                        worstAt = i;
                    }
                }

                if (worst <= tolerance || worstAt < 0)
                {
                    continue;
                }

                keep[worstAt] = true;
                pending.Push((from, worstAt));
                pending.Push((worstAt, to));
            }
        }

        private static int FurthestFromStart(double[] ring, int count)
        {
            var x = ring[0];
            var y = ring[1];
            var worst = -1.0;
            var worstAt = count / 2;

            for (var i = 1; i < count - 1; i++)
            {
                var dx = ring[i * 2] - x;
                var dy = ring[i * 2 + 1] - y;
                var distance = dx * dx + dy * dy;

                if (distance > worst)
                {
                    worst = distance;
                    worstAt = i;
                }
            }

            return worstAt;
        }
    }
}
