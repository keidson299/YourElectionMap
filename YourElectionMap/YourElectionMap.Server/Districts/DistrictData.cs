using System.Text.Json;

namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// Every district the site knows, from one file per state built by
    /// YourElectionMap.Tools (see its DATA-SOURCE.md), read once at startup and held in
    /// memory.
    /// </summary>
    public sealed class DistrictData
    {
        private const string FilePattern = "*-districts.json";

        private static readonly JsonSerializerOptions ReadOptions =
            new() { PropertyNameCaseInsensitive = true };

        /// <summary>The converter's detail for a school district that runs every grade.</summary>
        private const string UnifiedSchool = "School district";

        /// <summary>
        /// Fifty feet, in degrees of latitude: past the thirty a gap between two thinned
        /// edges can be at most, and well short of the next street.
        /// </summary>
        private const double Reach = 50 / 364_000.0;

        /// <summary>The levels every point in the state has one of.</summary>
        private static readonly string[] WholeState = ["congressional", "state-upper", "state-lower", "county"];

        private readonly Document[] _states;

        public DistrictData()
            : this(Directory.GetFiles(
                Path.Combine(AppContext.BaseDirectory, "Districts", "Data"),
                FilePattern))
        {
        }

        /// <summary>Loads the given files, which is how a test reaches a small one.</summary>
        public DistrictData(params string[] paths)
        {
            if (paths.Length == 0)
            {
                throw new InvalidDataException(
                    $"No {FilePattern} files to load. Build them with YourElectionMap.Tools.");
            }

            _states = Array.ConvertAll(paths, Load);
            Array.Sort(_states, (left, right) => string.CompareOrdinal(left.State, right.State));
        }

        /// <summary>The states loaded, by postal code.</summary>
        public IReadOnlyList<string> States => Array.ConvertAll(_states, state => state.State);

        /// <summary>
        /// Every district covering a point, broad to local, or null for a point in no
        /// state the site covers.
        /// </summary>
        public List<District>? Covering(double longitude, double latitude)
        {
            foreach (var state in _states)
            {
                var found = state.Districts
                    .Where(district => Geometry.BoxCovers(district.Bbox, longitude, latitude)
                        && Geometry.Covers(district.Rings, longitude, latitude))
                    .ToList();

                // States do not overlap, but for a strip a few feet wide where two
                // thinned borders cross, so the first to cover the point is the one.
                if (found.Count > 0)
                {
                    var settled = Settle(state.Districts, found, longitude, latitude);

                    return WithCounty(settled.ConvertAll(district => district.ToDistrict()));
                }
            }

            return null;
        }

        /// <summary>
        /// Each district is thinned on its own, so two that share an edge no longer meet
        /// exactly: a few feet either side of it, both cover a point, or neither does. A
        /// point covered twice keeps the district it is deeper inside. A whole-state level
        /// left with nothing takes the nearest district within <see cref="Reach"/>. A
        /// city or school district is never filled in, because plenty of land has none.
        /// </summary>
        private static List<Stored> Settle(Stored[] districts, List<Stored> found, double x, double y)
        {
            double Depth(Stored district) => Geometry.DistanceToRings(district.Rings, x, y);

            // Two of a kind where there can only be one. An elementary district inside a
            // high school district is another kind, as is a city inside a township. A
            // town, township and plantation are one kind, since two can meet, as
            // Vinalhaven town and Matinicus Isle plantation do off Maine.
            var settled = found
                .GroupBy(district => (district.Level, district.Level == "city" ? Inner(district).ToString() : district.Detail))
                .Select(kind => kind.Count() == 1 ? kind.First() : kind.MaxBy(Depth)!)
                .ToList();

            // A unified district runs every grade, so it cannot share a point with a high
            // school or elementary district: keep whichever side the point is deeper in.
            var unified = settled.Find(district => district.Level == "school" && district.Detail == UnifiedSchool);
            var split = settled.FindAll(district => district.Level == "school" && district != unified);

            if (unified is not null && split.Count > 0)
            {
                if (Depth(unified) >= split.Max(Depth))
                {
                    settled.RemoveAll(split.Contains);
                }
                else
                {
                    settled.Remove(unified);
                }
            }

            foreach (var level in WholeState)
            {
                if (settled.Exists(district => district.Level == level))
                {
                    continue;
                }

                Stored? nearest = null;
                var nearestDistance = Reach;
                // Twice the reach, so the box allows for a degree of longitude being short.
                var margin = 2 * Reach;

                foreach (var district in districts)
                {
                    var box = district.Bbox;

                    if (district.Level != level
                        || x < box[0] - margin || x > box[2] + margin
                        || y < box[1] - margin || y > box[3] + margin)
                    {
                        continue;
                    }

                    var distance = Depth(district);

                    if (distance <= nearestDistance)
                    {
                        nearest = district;
                        nearestDistance = distance;
                    }
                }

                if (nearest is not null)
                {
                    settled.Add(nearest);
                }
            }

            settled.Sort(Compare);

            return settled;
        }

        /// <summary>The county, named on every other district's map chip, where the file has counties.</summary>
        private static List<District> WithCounty(List<District> found)
        {
            var county = found.Find(district => district.Level == "county")?.Office;

            return county is null
                ? found
                : found.ConvertAll(district =>
                    district.Level == "county" ? district : district with { Area = county });
        }

        private static Document Load(string path)
        {
            using var file = File.OpenRead(path);

            var document = JsonSerializer.Deserialize<Document>(file, ReadOptions)
                ?? throw new InvalidDataException($"{path} held no district data.");

            // A file in any other units would load fine and then place nothing.
            if (document.Units != "degrees")
            {
                throw new InvalidDataException(
                    $"{path} is not in degrees. Rebuild it with YourElectionMap.Tools.");
            }

            Array.Sort(document.Districts, Compare);

            return document;
        }

        /// <summary>
        /// Broad to local, then by number, then the outer of two at one level before the
        /// inner, then by id, so the order never depends on the sort.
        /// </summary>
        private static int Compare(Stored left, Stored right)
        {
            var byLevel = Rank(left.Level) - Rank(right.Level);

            if (byLevel != 0)
            {
                return byLevel;
            }

            var leftNumber = int.TryParse(left.Number, out var l) ? l : int.MaxValue;
            var rightNumber = int.TryParse(right.Number, out var r) ? r : int.MaxValue;

            if (leftNumber != rightNumber)
            {
                return leftNumber.CompareTo(rightNumber);
            }

            var byReach = Inner(left) - Inner(right);

            return byReach != 0 ? byReach : string.CompareOrdinal(left.Id, right.Id);
        }

        /// <summary>
        /// 1 for the more local of two at one level: an elementary school district inside
        /// a high school district, or a city or village inside a town, township or
        /// Maine plantation.
        /// </summary>
        private static int Inner(Stored district) =>
            district.Detail.StartsWith("Elementary", StringComparison.Ordinal)
                || (district.Level == "city" && district.Detail is not ("Town" or "Township" or "Plantation"))
                ? 1
                : 0;

        /// <summary>
        /// The client's level order. An unknown level sorts last rather than throwing, so
        /// a new one in a data file cannot take the site down.
        /// </summary>
        private static int Rank(string level) => level switch
        {
            "congressional" => 0,
            "state-upper" => 1,
            "state-lower" => 2,
            "county" => 3,
            "city" => 4,
            "school" => 5,
            _ => 6,
        };

        /// <summary>A district as the file stores it, rings flattened to <c>[lon, lat, lon, lat, …]</c>.</summary>
        private sealed class Stored
        {
            private double[][][][]? _boundary;

            public string Id { get; init; } = "";

            public string Level { get; init; } = "";

            public string Office { get; init; } = "";

            public string Number { get; init; } = "";

            public string Detail { get; init; } = "";

            public double[] Bbox { get; init; } = [];

            public double[][] Rings { get; init; } = [];

            /// <summary>
            /// At the detail the file stores, not thinned further: the map zooms to street
            /// level, where a simplified line would land on the wrong side of the street.
            /// Built the first time it is asked for, then kept: sorting a large district's
            /// rings is too slow to repeat.
            /// </summary>
            public District ToDistrict() =>
                new(
                    Id,
                    Level,
                    Office,
                    Number: Number.Length == 0 ? null : Number,
                    Detail,
                    Area: null,
                    Boundary: _boundary ??= Geometry.ToPolygons(Array.ConvertAll(Rings, Round))
                        .Select(polygon => polygon.Select(ToPairs).ToArray())
                        .ToArray());

            /// <summary>Five places is about a metre, finer than the file's fifteen feet.</summary>
            private static double[] Round(double[] ring) =>
                Array.ConvertAll(ring, value => Math.Round(value, 5));

            private static double[][] ToPairs(double[] ring)
            {
                var pairs = new double[ring.Length / 2][];

                for (var i = 0; i < pairs.Length; i++)
                {
                    pairs[i] = [ring[i * 2], ring[i * 2 + 1]];
                }

                return pairs;
            }
        }

        private sealed class Document
        {
            public string State { get; init; } = "";

            public string Units { get; init; } = "";

            public Stored[] Districts { get; init; } = [];
        }
    }
}
