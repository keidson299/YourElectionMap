using System.Text.Json;

namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// Every district the site knows, held in memory for the life of the process.
    /// <para>
    /// The file behind it is built by YourElectionMap.Tools from the North Carolina
    /// precinct shapefiles — see that project's DATA-SOURCE.md. It is read once, at
    /// startup, because it is four megabytes that never change while the app runs.
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
        /// Every district covering a point in map units, broad to local, with each
        /// boundary thinned to what a view of that district can actually show. Empty for
        /// a point outside the state.
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

                found.Add(district.ToDistrict(ToleranceFor(district)));
            }

            return found;
        }

        /// <summary>
        /// How finely to draw this district: 1/1500 of its longer side, roughly a pixel
        /// at the frame the map fits it into. A congressional district then sends about
        /// a thousand points instead of tens of thousands, and looks no different.
        /// </summary>
        private static double ToleranceFor(Stored district)
        {
            var width = district.Bbox[2] - district.Bbox[0];
            var height = district.Bbox[3] - district.Bbox[1];

            return Math.Max(width, height) / 1500;
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
            public string Id { get; init; } = "";

            public string Level { get; init; } = "";

            public string Office { get; init; } = "";

            public string Number { get; init; } = "";

            public string Detail { get; init; } = "";

            public double[] Bbox { get; init; } = [];

            public double[][] Rings { get; init; } = [];

            public District ToDistrict(double tolerance) =>
                new(
                    Id,
                    Level,
                    Office,
                    "District",
                    Number,
                    Detail,
                    // Neither is in this data. The rail and the map chip leave out what
                    // they are not given.
                    Area: null,
                    Population: null,
                    Boundary: Array.ConvertAll(
                        Rings,
                        ring => ToPairs(Geometry.Thin(ring, tolerance))));

            /// <summary>Flat back to the [x, y] pairs the client's boundary type holds.</summary>
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
