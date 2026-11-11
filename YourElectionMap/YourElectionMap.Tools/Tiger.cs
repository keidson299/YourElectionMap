using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Tools
{
    /// <summary>
    /// A state's districts from the Census Bureau's TIGERweb layers, saved as GeoJSON in
    /// longitude and latitude: <c>cd.geojson</c>, <c>sldu.geojson</c> and, but for
    /// Nebraska's one chamber, <c>sldl.geojson</c> in the folder given, and, where the folder has them, the
    /// county, town or township, city and school district layers (<see cref="LocalLayers"/>).
    /// DATA-SOURCE.md
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
            // One seat in the House, elected at large; its Senate districts are lettered, A
            // to T, each two House districts; and its counties are boroughs and the census
            // areas of the land no borough governs.
            ["AK"] = new("02", [UsHouse(1), Senate(20) with { Lettered = true }, House(40)], County: "Borough or census area"),
            ["AL"] = new("01", [UsHouse(7), Senate(35), House(105)]),
            ["AR"] = new("05", [UsHouse(4), Senate(35), House(100)]),
            // Thirty districts, each electing a senator and two representatives: TIGER
            // has them in both layers.
            ["AZ"] = new("04", [UsHouse(9), Senate(30), House(30)]),
            ["CA"] = new("06", [UsHouse(52), Senate(40), House(80, "State Assembly")]),
            ["CO"] = new("08", [UsHouse(8), Senate(35), House(65)]),
            // Its counties are nine planning regions.
            ["CT"] = new("09", [UsHouse(5), Senate(36), House(151)], County: "Planning region"),
            // One seat in the House, elected at large, as in Wyoming.
            ["DE"] = new("10", [UsHouse(1), Senate(21), House(41)]),
            ["FL"] = new("12", [UsHouse(28), Senate(40), House(120)]),
            ["GA"] = new("13", [UsHouse(14), Senate(56), House(180)]),
            ["HI"] = new("15", [UsHouse(2), Senate(25), House(51)]),
            // Thirty-five districts, as in Arizona.
            ["ID"] = new("16", [UsHouse(2), Senate(35), House(35)]),
            ["IA"] = new("19", [UsHouse(4), Senate(50), House(100)]),
            ["IL"] = new("17", [UsHouse(17), Senate(59), House(118)]),
            ["IN"] = new("18", [UsHouse(9), Senate(50), House(100)]),
            ["KS"] = new("20", [UsHouse(4), Senate(40), House(125)]),
            ["KY"] = new("21", [UsHouse(6), Senate(38), House(100)]),
            // Its counties are parishes.
            ["LA"] = new("22", [UsHouse(6), Senate(39), House(105)], County: "Parish"),
            // Its districts are named, "Second Middlesex District", not numbered.
            ["MA"] = new("25", [UsHouse(9), Senate(40) with { Named = true }, House(160) with { Named = true }]),
            // Forty-seven districts, each electing a senator and three delegates: some from
            // the whole district, some from its halves, 2A and 2B, or its thirds, 1A, 1B and
            // 1C, as the Legislature drew them.
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
            // Each Senate district is halved into two House districts, 1A and 1B.
            ["MN"] = new("27", [UsHouse(8), Senate(67), House(67) with { Halved = [.. Enumerable.Range(1, 67)] }]),
            // The 2022 map, which is TIGER's 119th Congress layer: the courts kept the 2025
            // map, which its 120th Congress layer holds, out of the 2026 elections, and
            // Proposition A on the November 2026 ballot decides whether it is used after.
            ["MO"] = new("29", [UsHouse(8, "cd-119.geojson"), Senate(34), House(163)]),
            ["MS"] = new("28", [UsHouse(4), Senate(52), House(122)]),
            ["MT"] = new("30", [UsHouse(2), Senate(50), House(100)]),
            ["NC"] = new("37", [UsHouse(14), Senate(50), House(120)]),
            // Forty-seven districts, each electing a senator and two representatives, as in
            // Washington, but for District 4, whose representatives are elected from its
            // halves, 4A and 4B.
            ["ND"] = new("38", [UsHouse(1), Senate(47), House(47) with { Halved = [4] }]),
            // One chamber, the Legislature, with no lower house: TIGER has its districts
            // in the upper layer and nothing in the lower, so there is no sldl.geojson.
            ["NE"] = new("31", [UsHouse(3), Senate(49, "Legislature")]),
            // Its House districts are numbered within each county, "Belknap 1": named here.
            ["NH"] = new("33", [UsHouse(2), Senate(24), House(164) with { Named = true }]),
            // Forty districts, each electing a senator and two members of the General
            // Assembly: TIGER has them in both layers.
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
            // Thirty-five districts, as in North Dakota, but for Districts 26 and 28, each
            // halved.
            ["SD"] = new("46", [UsHouse(1), Senate(35), House(35) with { Halved = [26, 28] }]),
            ["TN"] = new("47", [UsHouse(9), Senate(33), House(99)]),
            ["TX"] = new("48", [UsHouse(38), Senate(31), House(150)]),
            // The court-ordered map of November 2025: TIGER has it in layer 0.
            ["UT"] = new("49", [UsHouse(4), Senate(29), House(75)]),
            ["VA"] = new("51", [UsHouse(11), Senate(40), House(100, "House of Delegates")]),
            // One seat in the House, elected at large, and its districts are named for
            // their counties, "Addison-1", not numbered.
            ["VT"] = new("50", [UsHouse(1), Senate(16) with { Named = true }, House(109) with { Named = true }]),
            // One set of 49 districts, each electing a senator and two representatives:
            // TIGER has it in both layers.
            ["WA"] = new("53", [UsHouse(10), Senate(49), House(49)]),
            ["WI"] = new("55", [UsHouse(8), Senate(33), House(99, "State Assembly")]),
            // Seventeen Senate districts, each electing two senators.
            ["WV"] = new("54", [UsHouse(2), Senate(17), House(100, "House of Delegates")]),
            // One seat in the House, elected at large: numbered 1 here.
            ["WY"] = new("56", [UsHouse(1), Senate(31), House(62)]),
        };

        private static Layer UsHouse(int count, string file = "cd.geojson") =>
            new(file, "congressional", "U.S. House", "Federal", count);

        private static Layer Senate(int count, string office = "State Senate") =>
            new("sldu.geojson", "state-upper", office, "State", count);

        private static Layer House(int count, string office = "State House") =>
            new("sldl.geojson", "state-lower", office, "State", count);

        /// <summary>
        /// A state's local layers: governments with a name rather than numbered
        /// districts. TIGER has the county, the city or town and the school district, not
        /// the commission, council or trustee districts inside them. Each is optional: a
        /// state folder without the file builds without that layer, and a state with no
        /// elementary or secondary districts has nothing in those files.
        /// </summary>
        private static NamedLayer[] LocalLayers(State state) =>
        [
            // The detail is the rail's second line under the name. A county's is the
            // state's own word for one.
            new("county.geojson", "county", state.County),
            // The towns and townships that are governments of their own, whose detail is
            // "Town" or "Township" by their kind, then the cities, towns and villages.
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
        /// Numbered 1 to N, each once, in order, with a halved district's two halves, or a
        /// district in three's three parts, in its place, or lettered A onwards: anything
        /// else is a plan this was not written for. Named districts are N, each GEOID
        /// once, sorted by it. Prints a
        /// line per district as it goes.
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

            Print(built, withOffice: false);
            Console.WriteLine($"{layer.Level}: {built.Count} districts");
        }

        /// <summary>
        /// A numbered district, or a named one where the state names them, as
        /// Massachusetts does, or null for TIGER's "not defined" area, which fills water
        /// no district takes in, as Lake Pontchartrain in Louisiana's Senate layer: GEOID
        /// <c>17ZZ</c> in a congressional layer, <c>22ZZZ</c> in a legislative one.
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
            // "State House District 4A" or "State Legislative Subdistrict 1C", or a state's one seat, "Congressional District
            // (at Large)".
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

            // Only a named district's name goes into the file, so only it must be quotable.
            var (points, thinned) = Shape(feature, layer.File, name, quotable: layer.Named);

            // A named district is its name over the chamber, "State Senate", as a county
            // is its name over "County", and its id is its GEOID.
            return layer.Named
                ? new BuiltDistrict($"{layer.Level}-{geoid}", layer.Level, NamedOffice(name), Number: "", layer.Office, points, thinned)
                : new BuiltDistrict($"{layer.Level}-{number}", layer.Level, layer.Office, number, layer.Detail, points, thinned);
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

            Print(built, withOffice: true);
            Console.WriteLine($"{layer.File}: {built.Count} {layer.Level} areas");
        }

        /// <summary>A line per district: its id, and how far thinning took it.</summary>
        private static void Print(List<BuiltDistrict> built, bool withOffice)
        {
            foreach (var district in built)
            {
                Console.WriteLine(
                    $"  {district.Id,-18} {district.RawPoints,8:N0} points -> "
                    + $"{district.Rings.Sum(ring => ring.Length / 2),6:N0}"
                    + (withOffice ? $"  {district.Office}" : ""));
            }
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

            if (name.Contains("Not Defined", StringComparison.OrdinalIgnoreCase)
                || (layer.Towns && !IsGovernment(properties, layer.File)))
            {
                Console.WriteLine($"  {geoid,-18} skipped: \"{name}\"");
                return null;
            }

            // A town is a town; a township, a charter township or Kansas's "Township 4" is
            // a township; and Maine's plantations, smaller towns, are plantations.
            var detail = !layer.Towns ? layer.Detail : Field(properties, "LSADC", layer.File) switch
            {
                "43" => "Town",
                "44" or "45" or "49" => "Township",
                "39" => "Plantation",
                var other => throw new InvalidDataException(
                    $"{layer.File}: \"{name}\" is a government of a kind this does not know, LSADC {other}."),
            };

            var (points, thinned) = Shape(feature, layer.File, name, quotable: true);

            return new BuiltDistrict($"{layer.Level}-{geoid}", layer.Level, name, Number: "", detail, points, thinned);
        }

        /// <summary>
        /// A feature's rings, thinned, with how many points they had before. A name that
        /// is written into the file as it is, between quotes, must need no escaping.
        /// </summary>
        private static (int RawPoints, List<double[]> Rings) Shape(
            JsonElement feature,
            string file,
            string name,
            bool quotable)
        {
            if (quotable && (name.Contains('"') || name.Contains('\\')))
            {
                throw new InvalidDataException($"{file}: \"{name}\" needs escaping.");
            }

            var parts = Rings(feature.GetProperty("geometry"), file, name);

            if (parts.Count == 0)
            {
                throw new InvalidDataException($"{file}: \"{name}\" has no shape.");
            }

            return (
                parts.Sum(part => part.Length / 2),
                parts.Select(part => Geometry.Thin(part, ToleranceDegrees)).ToList());
        }

        /// <summary>
        /// Whether a county subdivision is a town or township governing itself, by
        /// TIGER's functional status: A, active, or B, active but sharing some of its
        /// work with another government. The rest are left out: F, a city or borough the
        /// layer has again, which the places have; C, a town one with its city, as
        /// Hartford is; G, a borough of New York City; I and N, inactive or not
        /// functioning, as most of Missouri's townships are; and S, unorganized territory.
        /// </summary>
        private static bool IsGovernment(JsonElement properties, string file) =>
            Field(properties, "FUNCSTAT", file) is "A" or "B";

        private static string Field(JsonElement properties, string name, string file) =>
            properties.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                && value.GetString() is { Length: > 0 } text
                ? text
                : throw new InvalidDataException(
                    $"{file}: a feature has no {name}. Fetch it with {name} in outFields (DATA-SOURCE.md).");

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

                        // Alaska's Near Islands are past the 180th meridian, at positive
                        // longitudes, as nothing else the site covers is. They are kept
                        // west of it, below -180, so that a district reaching them is one
                        // piece rather than the width of the globe.
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

        /// <param name="County">The county rows' detail: "Parish" in Louisiana.</param>
        private sealed record State(string Fips, Layer[] Layers, string County = "County");

        /// <summary>
        /// A legislative layer: districts 1 to <paramref name="Count"/>, each of those in
        /// <paramref name="Halved"/> as two, an A and a B, and each in <paramref name="InThree"/>
        /// as three, an A, a B and a C; or, <paramref name="Lettered"/>, <paramref name="Count"/>
        /// districts lettered from A; or, <paramref name="Named"/>, <paramref name="Count"/>
        /// districts known by name.
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

        /// <param name="Towns">
        /// The county subdivisions: only the towns and townships that are governments
        /// (<see cref="IsGovernment"/>), each with the detail its kind gives it.
        /// </param>
        private sealed record NamedLayer(string File, string Level, string Detail, bool Towns = false);
    }
}
