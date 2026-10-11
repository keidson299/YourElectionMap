namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// Turns an address or a point into the districts covering it: an address is placed
    /// by the Census geocoder, then the point is handed to <see cref="DistrictData"/>.
    /// An address that lands in a state the site covers is then sent to Google for its
    /// next election.
    /// <para>
    /// The data covers North Carolina and California, so an address in another state is
    /// placed and then found in no district — the same null as an address that could not
    /// be placed.
    /// </para>
    /// </summary>
    public class DistrictLookupService
    {
        private readonly DistrictData _districts;
        private readonly CensusGeocoder _geocoder;
        private readonly GoogleVoterInfo _voterInfo;

        public DistrictLookupService(
            DistrictData districts,
            CensusGeocoder geocoder,
            GoogleVoterInfo voterInfo)
        {
            _districts = districts;
            _geocoder = geocoder;
            _voterInfo = voterInfo;
        }

        /// <summary>
        /// The districts covering a typed address, or null when it cannot be placed in
        /// any of them. An address with no state can match in several; the first match
        /// inside a state the site covers wins.
        /// </summary>
        /// <exception cref="GeocoderUnavailableException">The geocoder did not answer.</exception>
        public async Task<DistrictLookup?> LookupAddressAsync(
            string address,
            CancellationToken cancellationToken)
        {
            var matches = await _geocoder.FindAsync(address, cancellationToken);

            foreach (var (latitude, longitude, matched) in matches)
            {
                // The page names the address the way it was typed. The geocoder's own
                // spelling of it comes back in capitals, and is no more correct.
                var lookup = At(latitude, longitude, StreetLine(address));

                if (lookup is not null)
                {
                    // Google is asked about the geocoder's spelling, which names the state,
                    // so it cannot place a "100 Main St, Greenville" somewhere else.
                    return lookup with
                    {
                        Election = await _voterInfo.GetAsync(matched, cancellationToken),
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// The districts covering a point, or null when it is not in any of them. There
        /// is no election for it: Google's voter lookup takes an address, and a point has
        /// none to give it.
        /// </summary>
        public DistrictLookup? LookupLocation(double latitude, double longitude) =>
            At(latitude, longitude, "Your location");

        private DistrictLookup? At(double latitude, double longitude, string label)
        {
            var found = _districts.Covering(longitude, latitude);

            if (found is null)
            {
                return null;
            }

            return new DistrictLookup(
                label,
                [longitude, latitude],
                found.Districts,
                Election: null,
                found.Source);
        }

        /// <summary>'1420 Maple Street, Springfield' is named '1420 Maple Street'.</summary>
        private static string StreetLine(string address)
        {
            var line = address.Split(',')[0].Trim();
            return line.Length == 0 ? address : line;
        }
    }
}
