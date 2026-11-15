using System.Net.Http.Json;
using System.Text.Json;

namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// Places a typed address with the US Census Bureau's geocoder, which is free and needs
    /// no key. The address is all it sends, and nothing is logged or kept.
    /// </summary>
    public sealed class CensusGeocoder
    {
        private const string Endpoint =
            "https://geocoding.geo.census.gov/geocoder/locations/onelineaddress"
            + "?benchmark=Public_AR_Current&format=json&address=";

        private readonly HttpClient _client;

        public CensusGeocoder(HttpClient client)
        {
            _client = client;
            // Google's four seconds come after this, inside the client's fifteen.
            _client.Timeout = TimeSpan.FromSeconds(8);
        }

        /// <summary>
        /// Every place the address could be, in the geocoder's order: an address with no
        /// state can match in several. Each carries the geocoder's spelling, state
        /// included: "16 W JONES ST, RALEIGH, NC, 27601". The datum differs from the
        /// files' by about a metre, well inside their fifteen feet.
        /// </summary>
        /// <exception cref="GeocoderUnavailableException">
        /// A timeout, a refusal or a reply that is not the geocoder's JSON. This says
        /// nothing about the address.
        /// </exception>
        public async Task<IReadOnlyList<(double Latitude, double Longitude, string Address)>> FindAsync(
            string address,
            CancellationToken cancellationToken)
        {
            Reply? reply;

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

                reply = await response.Content.ReadFromJsonAsync<Reply>(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The browser went away; that is not a failure to report.
                throw;
            }
            catch (Exception failure) when (
                failure is HttpRequestException or TaskCanceledException or JsonException)
            {
                throw new GeocoderUnavailableException("The geocoder could not be reached.", failure);
            }

            // x is the longitude. A match with no coordinates is no match; one with no
            // spelling falls back to the address as typed.
            return (reply?.Result?.AddressMatches ?? [])
                .Where(match => match.Coordinates is { X: not null, Y: not null })
                .Select(match => (
                    match.Coordinates!.Y!.Value,
                    match.Coordinates.X!.Value,
                    string.IsNullOrWhiteSpace(match.MatchedAddress) ? address : match.MatchedAddress))
                .ToList();
        }

        private sealed record Reply(Result? Result);

        private sealed record Result(List<Match>? AddressMatches);

        private sealed record Match(string? MatchedAddress, Point? Coordinates);

        private sealed record Point(double? X, double? Y);
    }

    /// <summary>The geocoder could not be asked, or did not answer: the address may be fine.</summary>
    public sealed class GeocoderUnavailableException(string message, Exception? inner = null)
        : Exception(message, inner);
}
