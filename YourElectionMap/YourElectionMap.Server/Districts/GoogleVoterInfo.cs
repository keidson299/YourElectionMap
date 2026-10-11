using System.Text.Json;

namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// The next election at an address, from the Google Civic Information API's voter
    /// lookup, whose data comes from election officials through the Voting Information
    /// Project. It is free with an API key, read from <c>GoogleCivic:ApiKey</c>: user
    /// secrets in development, an environment variable when deployed.
    /// <para>
    /// The election is extra to the districts, so nothing here can fail a lookup. No key,
    /// no election, a refusal, a timeout or a reply that is not JSON all come back as
    /// null, which hides the rail's footer. Google is sent the address and nothing else,
    /// from the server, so it never sees the visitor; nothing is logged or kept.
    /// </para>
    /// </summary>
    public sealed class GoogleVoterInfo
    {
        private const string Endpoint =
            "https://www.googleapis.com/civicinfo/v2/voterinfo?address=";

        /// <summary>
        /// Where "today" is decided: the westernmost time zone the site covers, the Pacific
        /// coast's, so an election counts as over once its day has ended in every state. The
        /// Carolinas' stays up until 3 a.m. there, which does no harm.
        /// </summary>
        private static readonly TimeZoneInfo LastToClose =
            TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

        private readonly HttpClient _client;
        private readonly string? _apiKey;

        public GoogleVoterInfo(HttpClient client, IConfiguration configuration)
        {
            _client = client;
            _apiKey = configuration["GoogleCivic:ApiKey"];

            // Runs after the geocoder's eight seconds, and the client gives up on the
            // whole lookup at fifteen.
            _client.Timeout = TimeSpan.FromSeconds(4);
        }

        /// <summary>
        /// The next election for an address, or null when there is none to show or no
        /// answer to be had.
        /// </summary>
        public async Task<ElectionSummary?> GetAsync(
            string address,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return null;
            }

            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    Endpoint + Uri.EscapeDataString(address));

                // In a header rather than the URL, so the key is in no logged request line.
                request.Headers.Add("X-Goog-Api-Key", _apiKey);

                using var response = await _client.SendAsync(request, cancellationToken);

                // Google answers an address with no election as an error, so this is the
                // usual way of hearing "nothing to show", not only an outage.
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(body, default, cancellationToken);

                var today = DateOnly.FromDateTime(
                    TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, LastToClose).DateTime);

                return Summary(json.RootElement, today);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The browser gave up and went away.
                throw;
            }
            catch (Exception failure) when (
                failure is HttpRequestException or TaskCanceledException or JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Reads <c>election.electionDay</c> and counts <c>contests[]</c>: candidates
        /// across every contest with them, and referenda as measures. A reply with no
        /// contests (Google leaves them out when two elections share a day, and some data
        /// has only voting places) gives null counts rather than zeros, so the page never
        /// says "0 candidates on your ballot" about a ballot it has not seen.
        /// </summary>
        private static ElectionSummary? Summary(JsonElement root, DateOnly today)
        {
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("election", out var election)
                || election.ValueKind != JsonValueKind.Object
                || !election.TryGetProperty("electionDay", out var day)
                || day.ValueKind != JsonValueKind.String
                || !DateOnly.TryParseExact(day.GetString(), "yyyy-MM-dd", out var date)
                || date < today)
            {
                return null;
            }

            if (!root.TryGetProperty("contests", out var contests)
                || contests.ValueKind != JsonValueKind.Array
                || contests.GetArrayLength() == 0)
            {
                return new ElectionSummary(date, Candidates: null, Measures: null);
            }

            var candidates = 0;
            var measures = 0;

            foreach (var contest in contests.EnumerateArray())
            {
                if (contest.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (IsReferendum(contest))
                {
                    measures++;
                }
                else if (contest.TryGetProperty("candidates", out var running)
                    && running.ValueKind == JsonValueKind.Array)
                {
                    candidates += running.GetArrayLength();
                }
            }

            return new ElectionSummary(date, candidates, measures);
        }

        /// <summary>
        /// Google marks a referendum with the type "Referendum", and only a referendum
        /// has a <c>referendumTitle</c>.
        /// </summary>
        private static bool IsReferendum(JsonElement contest) =>
            (contest.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && string.Equals(type.GetString(), "Referendum", StringComparison.OrdinalIgnoreCase))
            || contest.TryGetProperty("referendumTitle", out _);
    }
}
