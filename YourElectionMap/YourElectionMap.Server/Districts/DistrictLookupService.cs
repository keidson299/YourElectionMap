namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// Turns an address or a point into the districts covering it.
    /// <para>
    /// Every value below is invented: the districts, the boundaries, the counts and
    /// the election. They are the sample the design used, and they come back for any
    /// address. The boundaries are in the stand-in drawing's survey units, not real
    /// geometry. Slice 4 replaces the bodies of these two methods with a real
    /// geocoder and real district data; the lookup they return keeps its shape.
    /// </para>
    /// </summary>
    public class DistrictLookupService
    {
        /// <summary>The sample address, the point every sample boundary surrounds.</summary>
        private static readonly double[] AddressPoint = [1127, 629];

        private static readonly ElectionSummary SampleElection =
            new(new DateOnly(2026, 11, 3), Candidates: 14, Measures: 2);

        private static readonly District[] SampleDistricts =
        [
            new District(
                Id: "congressional-7",
                Level: "congressional",
                Office: "U.S. House",
                Unit: "District",
                Number: "7",
                Detail: "Federal",
                Area: null,
                Population: 761200,
                Boundary: [[[640, 250], [1560, 270], [1540, 985], [680, 960]]]),
            new District(
                Id: "state-upper-22",
                Level: "state-upper",
                Office: "State Senate",
                Unit: "District",
                Number: "22",
                Detail: "State",
                Area: null,
                Population: 142800,
                Boundary: [[[800, 360], [1420, 390], [1400, 880], [830, 850]]]),
            new District(
                Id: "state-lower-44",
                Level: "state-lower",
                Office: "State House",
                Unit: "District",
                Number: "44",
                Detail: "State",
                Area: "Greenlake County",
                Population: 71400,
                Boundary: [[[930, 495], [1310, 520], [1270, 735], [1000, 765]]]),
            new District(
                Id: "county-3",
                Level: "county",
                Office: "County Commission",
                Unit: "District",
                Number: "3",
                Detail: "Greenlake County",
                Area: "Greenlake County",
                Population: 58900,
                Boundary: [[[700, 300], [1520, 330], [1480, 930], [740, 900]]]),
            new District(
                Id: "city-2",
                Level: "city",
                Office: "City Council",
                Unit: "Ward",
                Number: "2",
                Detail: "City of Springfield",
                Area: "City of Springfield",
                Population: 24600,
                Boundary: [[[1030, 540], [1250, 555], [1235, 720], [1050, 735]]]),
            new District(
                Id: "school-5",
                Level: "school",
                Office: "School Board",
                Unit: "Zone",
                Number: "5",
                Detail: "Springfield Unified",
                Area: "Springfield Unified",
                Population: 31200,
                Boundary: [[[965, 455], [1345, 478], [1305, 800], [1010, 825]]]),
        ];

        /// <summary>
        /// The districts covering a typed address, or null when it cannot be placed.
        /// The stub asks only for a house number, which a real geocoder needs too.
        /// </summary>
        public Task<DistrictLookup?> LookupAddressAsync(
            string address,
            CancellationToken cancellationToken)
        {
            if (!address.Any(char.IsDigit))
            {
                return Task.FromResult<DistrictLookup?>(null);
            }

            return Task.FromResult<DistrictLookup?>(SampleFor(StreetLine(address)));
        }

        /// <summary>
        /// The districts covering a point, or null when it is outside the country.
        /// </summary>
        public Task<DistrictLookup?> LookupLocationAsync(
            double latitude,
            double longitude,
            CancellationToken cancellationToken)
        {
            if (!IsInUnitedStates(latitude, longitude))
            {
                return Task.FromResult<DistrictLookup?>(null);
            }

            return Task.FromResult<DistrictLookup?>(SampleFor("Your location"));
        }

        private static DistrictLookup SampleFor(string label) =>
            new(label, AddressPoint, SampleDistricts, SampleElection, Sample: true);

        /// <summary>'1420 Maple Street, Springfield' is named '1420 Maple Street'.</summary>
        private static string StreetLine(string address)
        {
            var line = address.Split(',')[0].Trim();
            return line.Length == 0 ? address : line;
        }

        /// <summary>
        /// A rough box over the mainland, Alaska, Hawaii and most of Puerto Rico. It
        /// misses the western Aleutians, Guam and the Marianas, which sit at positive
        /// longitude, along with the US Virgin Islands and American Samoa. Slice 4 asks
        /// the real data whether a point is covered; until then this keeps the ocean out.
        /// </summary>
        private static bool IsInUnitedStates(double latitude, double longitude) =>
            latitude is >= 18 and <= 72 && longitude is >= -180 and <= -64;
    }
}
