using System.Text.Json;

namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// Turns a typed address into a point, using the US Census Bureau's geocoder, which
    /// is free and needs no key. The address is all it sends, and nothing is logged or
    /// kept.
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
        /// longitude are in degrees, as the boundaries are stored; any difference between
        /// the two datums is about a metre, well inside the fifteen feet the files are
        /// thinned to. Each match carries the geocoder's own full spelling of the address,
        /// state included: "16 W JONES ST, RALEIGH, NC, 27601".
        /// </summary>
        /// <exception cref="GeocoderUnavailableException">
        /// No usable answer: a timeout, a refusal or a reply that is not JSON. This says
        /// nothing about the address, so the page offers to try again rather than doubt it.
        /// </exception>
        public async Task<IReadOnlyList<(double Latitude, double Longitude, string Address)>> FindAsync(
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

                return Matches(json.RootElement, address);
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
        /// <c>result.addressMatches[].coordinates.{x, y}</c>, where x is the longitude,
        /// with its <c>matchedAddress</c>. Missing or misshapen coordinates read as no
        /// match rather than throwing; a missing address falls back to the one typed.
        /// </summary>
        private static List<(double Latitude, double Longitude, string Address)> Matches(
            JsonElement root,
            string typed)
        {
            var found = new List<(double Latitude, double Longitude, string Address)>();

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

                var matched = match.TryGetProperty("matchedAddress", out var spelled)
                    && spelled.ValueKind == JsonValueKind.String
                    ? spelled.GetString()
                    : null;

                found.Add((
                    latitude,
                    longitude,
                    string.IsNullOrWhiteSpace(matched) ? typed : matched));
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
