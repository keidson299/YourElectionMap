namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// Turns an address or a point into the districts covering it.
    /// <para>
    /// An address is placed by the Census geocoder; a point is used as given. Either way
    /// the point is projected into the map's own coordinates and handed to
    /// <see cref="DistrictData"/>, which knows which boundaries contain it.
    /// </para>
    /// <para>
    /// The data covers North Carolina and nothing else, so an address in another state
    /// is placed perfectly well and then found to be in no district — the same answer as
    /// an address that could not be placed at all. Both read as "no districts here",
    /// which is what the page says.
    /// </para>
    /// </summary>
    public class DistrictLookupService
    {
        private readonly DistrictData _districts;
        private readonly CensusGeocoder _geocoder;

        public DistrictLookupService(DistrictData districts, CensusGeocoder geocoder)
        {
            _districts = districts;
            _geocoder = geocoder;
        }

        /// <summary>
        /// The districts covering a typed address, or null when it cannot be placed in
        /// any of them. An address with no state can match in several; the first match
        /// inside North Carolina wins.
        /// </summary>
        /// <exception cref="GeocoderUnavailableException">The geocoder did not answer.</exception>
        public async Task<DistrictLookup?> LookupAddressAsync(
            string address,
            CancellationToken cancellationToken)
        {
            var matches = await _geocoder.FindAsync(address, cancellationToken);

            foreach (var (latitude, longitude) in matches)
            {
                // The page names the address the way it was typed. The geocoder's own
                // spelling of it comes back in capitals, and is no more correct.
                var lookup = At(latitude, longitude, StreetLine(address));

                if (lookup is not null)
                {
                    return lookup;
                }
            }

            return null;
        }

        /// <summary>
        /// The districts covering a point, or null when it is not in any of them.
        /// </summary>
        public Task<DistrictLookup?> LookupLocationAsync(
            double latitude,
            double longitude,
            CancellationToken cancellationToken) =>
            Task.FromResult(At(latitude, longitude, "Your location"));

        private DistrictLookup? At(double latitude, double longitude, string label)
        {
            var (x, y) = StatePlane.ToMapUnits(latitude, longitude);
            var covering = _districts.Covering(x, y);

            if (covering.Count == 0)
            {
                return null;
            }

            return new DistrictLookup(
                label,
                [x, y],
                covering,
                // No election data yet, which hides the rail's footer.
                Election: null,
                _districts.Source);
        }

        /// <summary>'1420 Maple Street, Springfield' is named '1420 Maple Street'.</summary>
        private static string StreetLine(string address)
        {
            var line = address.Split(',')[0].Trim();
            return line.Length == 0 ? address : line;
        }
    }
}
