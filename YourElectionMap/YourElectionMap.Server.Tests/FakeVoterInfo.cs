using System.Collections.Concurrent;
using System.Net;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// Stands in for Google's voter lookup, so that no test reaches the internet. It
    /// answers by the address it is asked about, which is the geocoder's spelling.
    /// </summary>
    internal static class FakeVoterInfo
    {
        /// <summary>
        /// Every address the endpoint tests' handler was asked about, for asserting what
        /// reached Google. Those tests run one at a time, so a shared list is enough.
        /// </summary>
        public static readonly ConcurrentQueue<string> Asked = new();

        /// <summary>
        /// The handler the endpoint tests put in place of the real one. Raleigh has an
        /// election with five candidates and one measure, Durham finds Google down, and
        /// anywhere else has no election.
        /// </summary>
        public static HttpMessageHandler Handler() =>
            new FakeGeocoding.StubHandler((request, _) =>
            {
                var asked = Uri.UnescapeDataString(request.RequestUri?.Query ?? "");
                Asked.Enqueue(asked);

                if (asked.Contains("RALEIGH", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(FakeGeocoding.Reply(Election(DaysFromToday(30))));
                }

                if (asked.Contains("DURHAM", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(FakeGeocoding.Reply("{}", HttpStatusCode.ServiceUnavailable));
                }

                return Task.FromResult(FakeGeocoding.Reply(NoElection, HttpStatusCode.BadRequest));
            });

        /// <summary>
        /// Relative to today, so the suite does not start failing once a real election
        /// date has passed.
        /// </summary>
        public static string DaysFromToday(int days) =>
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(days)).ToString("yyyy-MM-dd");

        /// <summary>
        /// A reply shaped the way the API's reference describes: two races with three and
        /// two candidates, and one referendum.
        /// </summary>
        public static string Election(string electionDay) => $$"""
            {
              "kind": "civicinfo#voterInfoResponse",
              "election": { "id": "9000", "name": "General Election", "electionDay": "{{electionDay}}" },
              "contests": [
                { "type": "General", "office": "U.S. House", "candidates": [ { "name": "A" }, { "name": "B" }, { "name": "C" } ] },
                { "type": "General", "office": "State Senate", "candidates": [ { "name": "D" }, { "name": "E" } ] },
                { "type": "Referendum", "referendumTitle": "Constitutional Amendment 1" }
              ]
            }
            """;

        /// <summary>What Google says to an address with no election in its data.</summary>
        public const string NoElection = """
            { "error": { "code": 400, "message": "Election unknown", "errors": [ { "reason": "invalid" } ] } }
            """;
    }
}
