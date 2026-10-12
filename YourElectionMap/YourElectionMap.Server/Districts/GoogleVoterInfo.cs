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
    /// <para>
    /// Google answers an address asked about without an election ID with "Election
    /// unknown", even with a general election weeks away, so the election list is read
    /// first and the next election covering the address's state is asked about by ID.
    /// </para>
    /// </summary>
    public sealed class GoogleVoterInfo
    {
        private const string ElectionsEndpoint =
            "https://www.googleapis.com/civicinfo/v2/elections";

        private const string Endpoint =
            "https://www.googleapis.com/civicinfo/v2/voterinfo?address=";

        /// <summary>Google's "VIP Test Election", always listed and always in the future.</summary>
        private const string TestElectionId = "2000";

        private const string Nationwide = "ocd-division/country:us";

        /// <summary>
        /// Where "today" is decided: the westernmost time zone the site covers, the Pacific
        /// coast's, so an election counts as over once its day has ended in every state. The
        /// Carolinas' stays up until 3 a.m. there, which does no harm.
        /// </summary>
        private static readonly TimeZoneInfo LastToClose =
            TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

        /// <summary>
        /// Both calls together. They run after the geocoder's eight seconds, and the client
        /// gives up on the whole lookup at fifteen.
        /// </summary>
        private static readonly TimeSpan Budget = TimeSpan.FromSeconds(4);

        private readonly HttpClient _client;
        private readonly string? _apiKey;

        public GoogleVoterInfo(HttpClient client, IConfiguration configuration)
        {
            _client = client;
            _apiKey = configuration["GoogleCivic:ApiKey"];
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

            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(Budget);

            var today = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, LastToClose).DateTime);

            try
            {
                using var elections = await GetJsonAsync(ElectionsEndpoint, budget.Token);
                var electionId = elections is null
                    ? null
                    : NextElectionId(elections.RootElement, State(address), today);

                if (electionId is null)
                {
                    return null;
                }

                using var voterInfo = await GetJsonAsync(
                    Endpoint + Uri.EscapeDataString(address)
                        + "&electionId=" + Uri.EscapeDataString(electionId),
                    budget.Token);

                return voterInfo is null ? null : Summary(voterInfo.RootElement, today);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The browser gave up and went away.
                throw;
            }
            catch (Exception failure) when (
                failure is HttpRequestException or OperationCanceledException or JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// The parsed answer, or null for any status but success. Google answers an
        /// address with no election as an error, so that is the usual way of hearing
        /// "nothing to show", not only an outage.
        /// </summary>
        private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // In a header rather than the URL, so the key is in no logged request line.
            request.Headers.Add("X-Goog-Api-Key", _apiKey);

            using var response = await _client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(body, default, cancellationToken);
        }

        /// <summary>
        /// The soonest election in Google's list that has not passed and covers the
        /// whole country or the address's state, leaving out Google's test election.
        /// An election for one county of the state still counts: Google then has
        /// nothing for an address outside it, which reads as no election.
        /// </summary>
        private static string? NextElectionId(JsonElement root, string? state, DateOnly today)
        {
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("elections", out var elections)
                || elections.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var statewide = state is null ? null : $"{Nationwide}/state:{state.ToLowerInvariant()}";
            (string Id, DateOnly Day)? next = null;

            foreach (var election in elections.EnumerateArray())
            {
                if (election.ValueKind != JsonValueKind.Object
                    || Text(election, "id") is not { } id
                    || id == TestElectionId
                    || Text(election, "electionDay") is not { } dayText
                    || !DateOnly.TryParseExact(dayText, "yyyy-MM-dd", out var day)
                    || day < today
                    || Text(election, "ocdDivisionId") is not { } division)
                {
                    continue;
                }

                var covers = division == Nationwide
                    || (statewide is not null
                        && (division == statewide || division.StartsWith(statewide + "/", StringComparison.Ordinal)));

                if (covers && (next is null || day < next.Value.Day))
                {
                    next = (id, day);
                }
            }

            return next?.Id;
        }

        private static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        /// <summary>
        /// The state in the geocoder's spelling of an address, "16 W JONES ST, RALEIGH, NC,
        /// 27601": the part before the ZIP code. Null if it is not there to read, which
        /// leaves only nationwide elections.
        /// </summary>
        private static string? State(string address)
        {
            var parts = address.Split(',', StringSplitOptions.TrimEntries);

            return parts.Length >= 2 && parts[^2].Length == 2 && parts[^2].All(char.IsAsciiLetter)
                ? parts[^2]
                : null;
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
