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

        /// <summary>The states loaded, by postal code: "CA", "NC", "OR".</summary>
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
                var found = new List<District>();

                foreach (var district in state.Districts)
                {
                    if (Geometry.BoxCovers(district.Bbox, longitude, latitude)
                        && Geometry.Covers(district.Rings, longitude, latitude))
                    {
                        found.Add(district.ToDistrict());
                    }
                }

                // States do not overlap, so the first to cover the point is the only one.
                if (found.Count > 0)
                {
                    return new Found(state.Source, found);
                }
            }

            return null;
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
            // never has to sort. Within a level, by number.
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

            return leftNumber - rightNumber;
        }

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
                    "District",
                    Number,
                    Detail,
                    // TIGER names no place; the map chip leaves out what it is not given.
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
