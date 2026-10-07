using System.Net;
using System.Text;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// Stands in for the Census geocoder, the app's only outward call, so that no test
    /// reaches the internet.
    /// </summary>
    internal static class FakeGeocoding
    {
        /// <summary>
        /// A handler that places any address holding one of these words, and finds no
        /// match for anything else. Coordinates are the real ones, so the real boundary
        /// data answers for them.
        /// </summary>
        public static readonly (string Word, (double Latitude, double Longitude)[] Points)[] Places =
        [
            // The State Legislative Building, Raleigh.
            ("Jones", [(35.7822, -78.6398)]),
            // Downtown Charlotte.
            ("Tryon", [(35.2271, -80.8431)]),
            // The White House, which is in no North Carolina district.
            ("Pennsylvania", [(38.8977, -77.0365)]),
            // A town name with no state: Greenville SC first, then Greenville NC.
            ("Greenville", [(34.8526, -82.3940), (35.6127, -77.3664)]),
        ];

        /// <summary>Any address holding this word finds the geocoder down.</summary>
        public const string Outage = "Outage";

        /// <summary>The handler the endpoint tests put in place of the real one.</summary>
        public static HttpMessageHandler Handler() =>
            new StubHandler((request, _) =>
            {
                var asked = request.RequestUri?.Query ?? "";

                if (asked.Contains(Outage, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(Reply("{}", HttpStatusCode.ServiceUnavailable));
                }

                foreach (var (word, points) in Places)
                {
                    if (asked.Contains(word, StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.FromResult(Reply(Match(points)));
                    }
                }

                return Task.FromResult(Reply(NoMatch));
            });

        /// <summary>
        /// A reply shaped the way the geocoder's documentation describes, with one match
        /// for each point.
        /// </summary>
        public static string Match(params (double Latitude, double Longitude)[] points)
        {
            var matches = points.Select(point =>
                $$"""{ "matchedAddress": "16 W JONES ST, RALEIGH, NC, 27601", "coordinates": { "x": {{point.Longitude}}, "y": {{point.Latitude}} } }""");

            return $$"""
                {
                  "result": {
                    "input": { "address": { "address": "whatever was asked" } },
                    "addressMatches": [ {{string.Join(", ", matches)}} ]
                  }
                }
                """;
        }

        /// <summary>An address the geocoder could not place: a match list with nothing in it.</summary>
        public const string NoMatch = """
            { "result": { "addressMatches": [] } }
            """;

        public static HttpResponseMessage Reply(
            string body,
            HttpStatusCode status = HttpStatusCode.OK) =>
            new(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

        /// <summary>An HttpClient handler that answers however the test says to.</summary>
        public sealed class StubHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply)
            : HttpMessageHandler
        {
            /// <summary>The query of the last request, for asserting what was sent.</summary>
            public string? LastQuery { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                LastQuery = request.RequestUri?.Query;

                return reply(request, cancellationToken);
            }
        }
    }
}
