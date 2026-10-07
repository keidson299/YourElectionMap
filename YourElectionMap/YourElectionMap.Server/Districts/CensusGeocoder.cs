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
    /// the design plan. An address the geocoder does not know comes back as no matches.
    /// A timeout, a refusal or a reply that is not JSON is a
    /// <see cref="GeocoderUnavailableException"/> instead, because it says nothing about
    /// the address and the page should offer to try again rather than doubt it.
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
        /// Every place the address could be, in the geocoder's order, or none when it
        /// cannot be placed. An address with no state can match in several. Latitude and
        /// longitude come back as WGS84, which is what <see cref="StatePlane"/> expects.
        /// </summary>
        /// <exception cref="GeocoderUnavailableException">No answer could be had.</exception>
        public async Task<IReadOnlyList<(double Latitude, double Longitude)>> FindAsync(
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
                    throw new GeocoderUnavailableException(
                        $"The geocoder answered {(int)response.StatusCode}.");
                }

                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(body, default, cancellationToken);

                return Matches(json.RootElement);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The browser gave up and went away; that is not a failure to report.
                throw;
            }
            catch (Exception failure) when (
                failure is HttpRequestException or TaskCanceledException or JsonException)
            {
                // A DNS failure, a timeout, a reply that was not JSON. None of them say
                // anything about the address.
                throw new GeocoderUnavailableException("The geocoder could not be reached.", failure);
            }
        }

        /// <summary>
        /// Reads every match out of
        /// <c>result.addressMatches[].coordinates.{x, y}</c>, where x is the longitude.
        /// Anything missing or of the wrong kind reads as no match rather than throwing.
        /// </summary>
        private static List<(double Latitude, double Longitude)> Matches(JsonElement root)
        {
            var found = new List<(double Latitude, double Longitude)>();

            if (!root.TryGetProperty("result", out var result)
                || !result.TryGetProperty("addressMatches", out var matches)
                || matches.ValueKind != JsonValueKind.Array)
            {
                return found;
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

                found.Add((latitude, longitude));
            }

            return found;
        }
    }

    /// <summary>
    /// The geocoder could not be asked, or did not answer: the address may be fine.
    /// </summary>
    public sealed class GeocoderUnavailableException(string message, Exception? inner = null)
        : Exception(message, inner);
}
