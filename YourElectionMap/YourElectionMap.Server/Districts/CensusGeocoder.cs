using System.Text.Json;

namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// Turns a typed address into a point, using the US Census Bureau's geocoder. It is
    /// free, needs no key, and is the same agency's view of the ground as the boundary
    /// data, so an address and a district line agree about where they are.
    /// <para>
    /// This is the only call this app makes out to the internet, and the address is what
    /// it sends. Nothing is logged here, and nothing is kept: see the privacy section of
    /// the design plan. A miss, a timeout, a refusal and a reply this does not recognise
    /// are all the same answer — null — because none of them leave an address to place.
    /// </para>
    /// </summary>
    public sealed class CensusGeocoder
    {
        /// <summary>
        /// The one-line address endpoint. <c>benchmark</c> picks the vintage of the
        /// address file; "Public_AR_Current" is the current one.
        /// </summary>
        private const string Endpoint =
            "https://geocoding.geo.census.gov/geocoder/locations/onelineaddress"
            + "?benchmark=Public_AR_Current&format=json&address=";

        private readonly HttpClient _client;

        public CensusGeocoder(HttpClient client)
        {
            _client = client;

            // Someone is watching a spinner while this runs, and the client gives up on
            // the whole lookup at fifteen seconds.
            _client.Timeout = TimeSpan.FromSeconds(8);
        }

        /// <summary>
        /// Where the address is, or null when it cannot be placed. Latitude and longitude
        /// come back as WGS84, which is what <see cref="StatePlane"/> expects.
        /// </summary>
        public async Task<(double Latitude, double Longitude)?> FindAsync(
            string address,
            CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _client.GetAsync(
                    Endpoint + Uri.EscapeDataString(address),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(body, default, cancellationToken);

                return FirstMatch(json.RootElement);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The browser gave up and went away; that is not a failure to report.
                throw;
            }
            catch (Exception)
            {
                // A timeout, a DNS failure, a reply that was not JSON. None of them say
                // anything about the address, and the page says so.
                return null;
            }
        }

        /// <summary>
        /// Reads the first match out of
        /// <c>result.addressMatches[].coordinates.{x, y}</c>, where x is the longitude.
        /// Anything missing or of the wrong kind reads as no match rather than throwing.
        /// </summary>
        private static (double Latitude, double Longitude)? FirstMatch(JsonElement root)
        {
            if (!root.TryGetProperty("result", out var result)
                || !result.TryGetProperty("addressMatches", out var matches)
                || matches.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var match in matches.EnumerateArray())
            {
                if (!match.TryGetProperty("coordinates", out var point)
                    || !point.TryGetProperty("x", out var x)
                    || !point.TryGetProperty("y", out var y)
                    || !x.TryGetDouble(out var longitude)
                    || !y.TryGetDouble(out var latitude))
                {
                    continue;
                }

                return (latitude, longitude);
            }

            return null;
        }
    }
}
