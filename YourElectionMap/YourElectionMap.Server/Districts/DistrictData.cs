using System.Text.Json;

namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// Every district the site knows, held in memory for the life of the process.
    /// <para>
    /// The file behind it is built by YourElectionMap.Tools from the North Carolina
    /// General Assembly's enacted district plans — see that project's DATA-SOURCE.md.
    /// It is read once, at startup, because it is megabytes that never change while
    /// the app runs.
    /// </para>
    /// </summary>
    public sealed class DistrictData
    {
        private const string FileName = "nc-districts.json";

        private static readonly JsonSerializerOptions ReadOptions =
            new() { PropertyNameCaseInsensitive = true };

        private readonly Stored[] _districts;

        public DistrictData()
            : this(Path.Combine(AppContext.BaseDirectory, "Districts", "Data", FileName))
        {
        }

        /// <summary>Loads from a given path, which is how a test reaches a small one.</summary>
        public DistrictData(string path)
        {
            using var file = File.OpenRead(path);

            var document = JsonSerializer.Deserialize<Document>(file, ReadOptions)
                ?? throw new InvalidDataException($"{path} held no district data.");

            Source = document.Source;
            _districts = document.Districts;

            // Broad to local, which is the order the rail lists them in, so the lookup
            // never has to sort. Within a level, by number.
            Array.Sort(_districts, Compare);
        }

        /// <summary>What to credit the boundaries to, as the page shows it.</summary>
        public string Source { get; }

        public int Count => _districts.Length;

        /// <summary>
        /// Every district covering a point in map units, broad to local, each with its
        /// boundary in latitude and longitude for the page's map. Empty for a point
        /// outside the state.
        /// </summary>
        public List<District> Covering(double x, double y)
        {
            var found = new List<District>();

            foreach (var district in _districts)
            {
                if (!Geometry.BoxCovers(district.Bbox, x, y)
                    || !Geometry.Covers(district.Rings, x, y))
                {
                    continue;
                }

                found.Add(district.ToDistrict());
            }

            return found;
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

        /// <summary>A district as the file stores it, rings flattened to <c>[x, y, x, y, …]</c>.</summary>
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

            /// <summary>Residents at the 2020 census, or null in a file that has none.</summary>
            public int? Population { get; init; }

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
                    // The plans name no place; the map chip leaves out what it is not
                    // given. Population is the 2020 census count the plan was drawn to.
                    Area: null,
                    Population: Population,
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
                var degrees = Array.ConvertAll(Rings, ToDegrees);

                return Geometry.ToPolygons(degrees)
                    .Select(polygon => polygon.Select(ToPairs).ToArray())
                    .ToArray();
            }

            /// <summary>A ring in map units as a flat ring of [longitude, latitude].</summary>
            private static double[] ToDegrees(double[] ring)
            {
                var degrees = new double[ring.Length];

                for (var i = 0; i < ring.Length; i += 2)
                {
                    var (latitude, longitude) = StatePlane.ToLatLon(ring[i], ring[i + 1]);

                    degrees[i] = Math.Round(longitude, Decimals);
                    degrees[i + 1] = Math.Round(latitude, Decimals);
                }

                return degrees;
            }

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

        /// <summary>The file as a whole.</summary>
        private sealed class Document
        {
            public string Source { get; init; } = "";

            public Stored[] Districts { get; init; } = [];
        }
    }
}
