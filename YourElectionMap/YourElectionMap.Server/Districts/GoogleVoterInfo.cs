using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// The next election at an address, from the Google Civic Information API's voter
    /// lookup, whose data comes from election officials through the Voting Information
    /// Project. It is free with an API key, read from <c>GoogleCivic:ApiKey</c>: user
    /// secrets in development, an environment variable when deployed.
    /// <para>
    /// The election is extra to the districts, so nothing here can fail a lookup. No key,
    /// no election, a refusal, a timeout, a reply that is not JSON or anything else that
    /// goes wrong all come back as null, which hides the rail's footer. Google is sent the
    /// address and nothing else, from the server, so it never sees the visitor; nothing
    /// is logged or kept.
    /// </para>
    /// <para>
    /// Google answers an address asked about without an election ID with "Election
    /// unknown", even with a general election weeks away, so the election list is read
    /// first and the next election covering the address's state is asked about by ID.
    /// The list is the same for every address, so it is kept for an hour.
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
        /// Carolinas' stays up until 3 a.m. there, which does no harm. A host with no
        /// time zone data gets Pacific standard time all year, an hour off in summer.
        /// </summary>
        private static readonly TimeZoneInfo LastToClose =
            TimeZoneInfo.TryFindSystemTimeZoneById("America/Los_Angeles", out var pacific)
                ? pacific
                : TimeZoneInfo.CreateCustomTimeZone("Pacific", TimeSpan.FromHours(-8), "Pacific", "Pacific");

        /// <summary>How long the election list is kept. Google adds to it a few times a month.</summary>
        private static readonly TimeSpan ListLifetime = TimeSpan.FromHours(1);

        private const string ListCacheKey = "GoogleVoterInfo.Elections";

        /// <summary>
        /// Both calls together. They run after the geocoder's eight seconds, and the client
        /// gives up on the whole lookup at fifteen.
        /// </summary>
        private static readonly TimeSpan Budget = TimeSpan.FromSeconds(4);

        private readonly HttpClient _client;
        private readonly IMemoryCache _cache;
        private readonly string? _apiKey;

        public GoogleVoterInfo(HttpClient client, IConfiguration configuration, IMemoryCache cache)
        {
            _client = client;
            _cache = cache;
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
                var elections = await ElectionsAsync(budget.Token);
                var electionId = elections is null
                    ? null
                    : NextElectionId(elections, State(address), today);

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
            catch (Exception)
            {
                // A refusal, a timeout, a reply that is not JSON, or anything nobody
                // thought of: the election is extra, and never costs the districts.
                return null;
            }
        }

        /// <summary>
        /// Google's election list, as read within the last hour, or null when it cannot
        /// be read. A failure is not kept, so the next lookup asks again.
        /// </summary>
        private async Task<IReadOnlyList<Election>?> ElectionsAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(ListCacheKey, out IReadOnlyList<Election>? kept))
            {
                return kept;
            }

            using var document = await GetJsonAsync(ElectionsEndpoint, cancellationToken);
            var elections = document is null ? null : ReadElections(document.RootElement);

            if (elections is not null)
            {
                _cache.Set<IReadOnlyList<Election>>(ListCacheKey, elections, ListLifetime);
            }

            return elections;
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
        /// Every election in Google's list that can be read, leaving out Google's test
        /// election, or null when the answer is not a list at all.
        /// </summary>
        private static List<Election>? ReadElections(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("elections", out var elections)
                || elections.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var read = new List<Election>();

            foreach (var election in elections.EnumerateArray())
            {
                if (election.ValueKind != JsonValueKind.Object
                    || Text(election, "id") is not { } id
                    || id == TestElectionId
                    || Text(election, "electionDay") is not { } dayText
                    || !TryParseDay(dayText, out var day)
                    || Text(election, "ocdDivisionId") is not { } division)
                {
                    continue;
                }

                read.Add(new Election(id, day, division));
            }

            return read;
        }

        /// <summary>
        /// The soonest election in the list that has not passed and covers the whole
        /// country or the address's state. An election for one county of the state still
        /// counts: Google then has nothing for an address outside it, which reads as no
        /// election.
        /// </summary>
        private static string? NextElectionId(IReadOnlyList<Election> elections, string? state, DateOnly today)
        {
            var statewide = state is null ? null : $"{Nationwide}/state:{state.ToLowerInvariant()}";
            Election? next = null;

            foreach (var election in elections)
            {
                var division = election.Division;
                var covers = division == Nationwide
                    || (statewide is not null
                        && (division == statewide || division.StartsWith(statewide + "/", StringComparison.Ordinal)));

                if (covers && election.Day >= today && (next is null || election.Day < next.Day))
                {
                    next = election;
                }
            }

            return next?.Id;
        }

        /// <summary>Google's "2026-11-03", read the same whatever the server's culture.</summary>
        private static bool TryParseDay(string? text, out DateOnly day) =>
            DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

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
                || !TryParseDay(Text(election, "electionDay"), out var date)
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
            string.Equals(Text(contest, "type"), "Referendum", StringComparison.OrdinalIgnoreCase)
            || contest.TryGetProperty("referendumTitle", out _);

        /// <summary>One election in Google's list, as kept between lookups.</summary>
        private sealed record Election(string Id, DateOnly Day, string Division);
    }
}
