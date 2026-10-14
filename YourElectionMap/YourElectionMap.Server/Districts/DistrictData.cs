using System.Text.Json;

namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// Every district the site knows, held in memory for the life of the process.
    /// <para>
    /// There is one file per state, <c>nc-districts.json</c> and so on, each built by
    /// YourElectionMap.Tools — see that project's DATA-SOURCE.md. Every one in the data
    /// folder is read once, at startup, because they are megabytes that never change
    /// while the app runs. Each holds its rings in longitude and latitude, which is
    /// what an address is tested against and what the page is sent.
    /// </para>
    /// </summary>
    public sealed class DistrictData
    {
        private const string FilePattern = "*-districts.json";

        private static readonly JsonSerializerOptions ReadOptions =
            new() { PropertyNameCaseInsensitive = true };

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

        /// <summary>The states loaded, by postal code: "AZ", "CA", "ID".</summary>
        public IReadOnlyList<string> States => Array.ConvertAll(_states, state => state.State);

        public int Count => _states.Sum(state => state.Districts.Length);

        /// <summary>
        /// Every district covering a point, broad to local, each with its boundary for the
        /// page's map, and what the state's boundaries are credited to. Null for a point
        /// in no state the site covers.
        /// </summary>
        public Found? Covering(double longitude, double latitude)
        {
            foreach (var state in _states)
            {
                var found = new List<Stored>();

                foreach (var district in state.Districts)
                {
                    if (Geometry.BoxCovers(district.Bbox, longitude, latitude)
                        && Geometry.Covers(district.Rings, longitude, latitude))
                    {
                        found.Add(district);
                    }
                }

                // States do not overlap, so the first to cover the point is the only one,
                // but for a strip a few feet wide where two thinned borders cross.
                if (found.Count > 0)
                {
                    var settled = Settle(state.Districts, found, longitude, latitude);

                    return new Found(state.Source, WithCounty(settled.ConvertAll(district => district.ToDistrict())));
                }
            }

            return null;
        }

        /// <summary>
        /// Each district is thinned on its own, so two that share an edge no longer meet
        /// exactly: a few feet either side of it, both cover a point, or neither does. A
        /// point covered twice keeps the district it is deeper inside. A level that
        /// covers the whole state, left with nothing, takes the nearest district within
        /// <see cref="Reach"/>. A city or school district is never filled in that way,
        /// because plenty of land has none. Either answer is a few feet from a line
        /// that is itself only good to fifteen.
        /// </summary>
        private static List<Stored> Settle(Stored[] districts, List<Stored> found, double x, double y)
        {
            double Depth(Stored district) => Geometry.DistanceToRings(district.Rings, x, y);

            // Two of a kind, where there can only be one: an elementary district inside
            // a high school district is a different kind, and stays.
            var settled = found
                .GroupBy(district => (district.Level, district.Detail))
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

                foreach (var district in districts)
                {
                    // Twice the reach, so the box allows for a degree of longitude being short.
                    if (district.Level != level || !Geometry.BoxCovers(Widened(district.Bbox, 2 * Reach), x, y))
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

        /// <summary>The converter's detail for a school district that runs every grade.</summary>
        private const string UnifiedSchool = "School district";

        /// <summary>
        /// Fifty feet, in degrees of latitude: past the thirty a gap between two thinned
        /// edges can be at most, and well short of the next street.
        /// </summary>
        private const double Reach = 50 / 364_000.0;

        /// <summary>The levels every point in the state has one of.</summary>
        private static readonly string[] WholeState = ["congressional", "state-upper", "state-lower", "county"];

        private static double[] Widened(double[] box, double margin) =>
            [box[0] - margin, box[1] - margin, box[2] + margin, box[3] + margin];

        /// <summary>
        /// The county the point is in, named on the map chip of every other district:
        /// "State House · District 38" over "Wake County". Left as it was where the
        /// file has no counties.
        /// </summary>
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

            // Broad to local, which is the order the rail lists them in, so the lookup
            // never has to sort. Within a level, by number; an elementary school
            // district after the secondary one covering it; then by id, so the order
            // never depends on the sort.
            Array.Sort(document.Districts, Compare);

            return document;
        }

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

            var byGrades = Elementary(left) - Elementary(right);

            return byGrades != 0 ? byGrades : string.CompareOrdinal(left.Id, right.Id);
        }

        /// <summary>1 for the converter's "Elementary school district", the more local of two.</summary>
        private static int Elementary(Stored district) =>
            district.Detail.StartsWith("Elementary", StringComparison.Ordinal) ? 1 : 0;

        /// <summary>
        /// The client's DISTRICT_LEVELS order. A level this does not know sorts last
        /// rather than throwing, so adding one to the data file cannot take the site down.
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
            /// <summary>
            /// Degrees to five places, about a metre — finer than the fifteen feet the
            /// file keeps, and no longer on the wire than it needs to be.
            /// </summary>
            private const int Decimals = 5;

            private double[][][][]? _boundary;
            public string Id { get; init; } = "";

            public string Level { get; init; } = "";

            public string Office { get; init; } = "";

            public string Number { get; init; } = "";

            public string Detail { get; init; } = "";

            public double[] Bbox { get; init; } = [];

            public double[][] Rings { get; init; } = [];

            public District ToDistrict() =>
                new(
                    Id,
                    Level,
                    Office,
                    // A county, city or school district is stored with no number: it is
                    // named, "Wake County", not numbered.
                    Unit: Number.Length == 0 ? null : "District",
                    Number: Number.Length == 0 ? null : Number,
                    Detail,
                    // Filled from the county by Covering, where the file has counties.
                    Area: null,
                    // Worked out the first time the district is asked for and kept, since
                    // the data never changes while the app runs.
                    Boundary: _boundary ??= Boundary());

            /// <summary>
            /// The rings at the detail the file stores, not thinned: the page's map zooms
            /// to street level, where a simplified line would land on the wrong side of
            /// the street. The map simplifies for wider views itself.
            /// </summary>
            private double[][][][] Boundary()
            {
                var rounded = Array.ConvertAll(Rings, Round);

                return Geometry.ToPolygons(rounded)
                    .Select(polygon => polygon.Select(ToPairs).ToArray())
                    .ToArray();
            }

            private static double[] Round(double[] ring) =>
                Array.ConvertAll(ring, value => Math.Round(value, Decimals));

            /// <summary>Flat back to the pairs GeoJSON holds.</summary>
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

        /// <summary>One state's file as a whole.</summary>
        private sealed class Document
        {
            public string State { get; init; } = "";

            public string Source { get; init; } = "";

            public string Units { get; init; } = "";

            public Stored[] Districts { get; init; } = [];
        }
    }

    /// <summary>
    /// The districts covering a point, broad to local, and what that state's boundaries
    /// are credited to: "U.S. Census Bureau TIGER".
    /// </summary>
    public sealed record Found(string Source, List<District> Districts);
}
