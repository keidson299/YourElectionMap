using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// The next election at an address, from the Google Civic Information API, with the
    /// key in <c>GoogleCivic:ApiKey</c>. The election is extra to the districts, so
    /// nothing here can fail a lookup: every way of getting no answer comes back null,
    /// which hides the rail's footer.
    /// <para>
    /// Asked without an election ID, Google answers "Election unknown" even weeks before
    /// a general election, so the election list is read first (and kept for an hour) and
    /// the next election covering the address's state is asked about by ID.
    /// </para>
    /// </summary>
    public sealed class GoogleVoterInfo(HttpClient client, IConfiguration configuration, IMemoryCache cache)
    {
        private const string ElectionsEndpoint = "https://www.googleapis.com/civicinfo/v2/elections";
        private const string VoterInfoEndpoint = "https://www.googleapis.com/civicinfo/v2/voterinfo?address=";

        /// <summary>Google's "VIP Test Election", always listed and always in the future.</summary>
        private const string TestElectionId = "2000";

        private const string Nationwide = "ocd-division/country:us";
        private const string ListCacheKey = "GoogleVoterInfo.Elections";
        private static readonly TimeSpan ListLifetime = TimeSpan.FromHours(1);

        /// <summary>Both calls together, after the geocoder's eight seconds and inside the client's fifteen.</summary>
        private static readonly TimeSpan Budget = TimeSpan.FromSeconds(4);

        /// <summary>
        /// Where "today" is decided: Pacific midnight comes after every state's polls have
        /// closed, Alaska's and Hawaii's included. A host with no time zone data gets
        /// Pacific standard time all year.
        /// </summary>
        private static readonly TimeZoneInfo LastToClose =
            TimeZoneInfo.TryFindSystemTimeZoneById("America/Los_Angeles", out var pacific)
                ? pacific
                : TimeZoneInfo.CreateCustomTimeZone("Pacific", TimeSpan.FromHours(-8), "Pacific", "Pacific");

        private readonly string? _apiKey = configuration["GoogleCivic:ApiKey"];

        /// <summary>
        /// The next election for an address in the geocoder's spelling, or null. Only the
        /// browser cancelling propagates.
        /// </summary>
        public async Task<ElectionSummary?> GetAsync(string address, CancellationToken cancellationToken)
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
                var electionId = elections is null ? null : NextElectionId(elections, State(address), today);

                if (electionId is null)
                {
                    return null;
                }

                var reply = await GetAsync<VoterInfoReply>(
                    VoterInfoEndpoint + Uri.EscapeDataString(address)
                        + "&electionId=" + Uri.EscapeDataString(electionId),
                    budget.Token);

                return reply is null ? null : Summary(reply, today);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // A refusal, a timeout, a reply that is not JSON, or anything else.
                return null;
            }
        }

        /// <summary>Google's election list, kept for an hour. A failure is not kept.</summary>
        private async Task<List<Listed>?> ElectionsAsync(CancellationToken cancellationToken)
        {
            if (cache.TryGetValue(ListCacheKey, out List<Listed>? kept))
            {
                return kept;
            }

            var reply = await GetAsync<ElectionsReply>(ElectionsEndpoint, cancellationToken);
            var elections = reply?.Elections?
                .Select(election =>
                    election.Id is { } id && id != TestElectionId
                        && election.OcdDivisionId is { } division
                        && TryParseDay(election.ElectionDay, out var day)
                        ? new Listed(id, day, division)
                        : null)
                .OfType<Listed>()
                .ToList();

            if (elections is not null)
            {
                cache.Set(ListCacheKey, elections, ListLifetime);
            }

            return elections;
        }

        /// <summary>
        /// The parsed reply, or null for any status but success. Google answers an address
        /// with no election as an error, so that is the usual "nothing to show".
        /// </summary>
        private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // In a header rather than the URL, so the key is in no logged request line.
            request.Headers.Add("X-Goog-Api-Key", _apiKey);

            using var response = await client.SendAsync(request, cancellationToken);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                : default;
        }

        /// <summary>
        /// The soonest coming election for the whole country or the address's state. One
        /// for a county of the state still counts: Google then has nothing for an address
        /// outside it, which reads as no election.
        /// </summary>
        private static string? NextElectionId(List<Listed> elections, string? state, DateOnly today)
        {
            var statewide = state is null ? null : $"{Nationwide}/state:{state.ToLowerInvariant()}";

            return elections
                .Where(election => election.Day >= today
                    && (election.Division == Nationwide
                        || (statewide is not null
                            && (election.Division == statewide
                                || election.Division.StartsWith(statewide + "/", StringComparison.Ordinal)))))
                .MinBy(election => election.Day)?
                .Id;
        }

        /// <summary>
        /// Candidates across every contest, and referenda as measures. A reply with no
        /// contests (Google leaves them out when two elections share a day) gives null
        /// counts, so the page never says "0 candidates" about a ballot it has not seen.
        /// </summary>
        private static ElectionSummary? Summary(VoterInfoReply reply, DateOnly today)
        {
            if (!TryParseDay(reply.Election?.ElectionDay, out var date) || date < today)
            {
                return null;
            }

            if (reply.Contests is not { Count: > 0 } contests)
            {
                return new ElectionSummary(date, Candidates: null, Measures: null);
            }

            // Google marks a referendum by its type, and only a referendum has a title.
            bool IsReferendum(Contest contest) =>
                string.Equals(contest.Type, "Referendum", StringComparison.OrdinalIgnoreCase)
                || contest.ReferendumTitle is not null;

            return new ElectionSummary(
                date,
                contests.Where(contest => !IsReferendum(contest)).Sum(contest => contest.Candidates?.Count ?? 0),
                contests.Count(IsReferendum));
        }

        /// <summary>
        /// The state in the geocoder's spelling, "16 W JONES ST, RALEIGH, NC, 27601": the
        /// part before the ZIP code. Null leaves only nationwide elections.
        /// </summary>
        private static string? State(string address)
        {
            var parts = address.Split(',', StringSplitOptions.TrimEntries);

            return parts.Length >= 2 && parts[^2].Length == 2 && parts[^2].All(char.IsAsciiLetter)
                ? parts[^2]
                : null;
        }

        private static bool TryParseDay(string? text, out DateOnly day) =>
            DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

        /// <summary>One election in Google's list, as kept between lookups.</summary>
        private sealed record Listed(string Id, DateOnly Day, string Division);

        private sealed record ElectionsReply(List<Election>? Elections);

        private sealed record Election(string? Id, string? ElectionDay, string? OcdDivisionId);

        private sealed record VoterInfoReply(Election? Election, List<Contest>? Contests);

        private sealed record Contest(string? Type, string? ReferendumTitle, List<object>? Candidates);
    }
}
