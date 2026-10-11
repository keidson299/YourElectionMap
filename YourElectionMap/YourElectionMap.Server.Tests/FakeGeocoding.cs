using System.Net;
using System.Text;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// Stands in for the Census geocoder, so that no test reaches the internet. Google's
    /// voter lookup has its own stand-in, <see cref="FakeVoterInfo"/>.
    /// </summary>
    internal static class FakeGeocoding
    {
        /// <summary>
        /// A handler that places any address holding one of these words, and finds no
        /// match for anything else. Coordinates are the real ones, so the real boundary
        /// data answers for them, and each match has the geocoder's spelling of it.
        /// </summary>
        public static readonly (string Word, Place[] Points)[] Places =
        [
            // The State Legislative Building, Raleigh.
            ("Jones", [new(35.7822, -78.6398, "16 W JONES ST, RALEIGH, NC, 27601")]),
            // Downtown Charlotte.
            ("Tryon", [new(35.2271, -80.8431, "101 S TRYON ST, CHARLOTTE, NC, 28202")]),
            // Downtown Durham.
            ("Mangum", [new(35.9960, -78.9017, "101 N MANGUM ST, DURHAM, NC, 27701")]),
            // The California State Capitol, Sacramento.
            ("Sacramento", [new(38.5767, -121.4934, "1315 10TH ST, SACRAMENTO, CA, 95814")]),
            // The White House, which is in neither state the site covers.
            ("Pennsylvania", [new(38.8977, -77.0365, "1600 PENNSYLVANIA AVE NW, WASHINGTON, DC, 20500")]),
            // A town name with no state: Greenville SC first, then Greenville NC.
            ("Greenville",
            [
                new(34.8526, -82.3940, "100 MAIN ST, GREENVILLE, SC, 29601"),
                new(35.6127, -77.3664, "100 MAIN ST, GREENVILLE, NC, 27858"),
            ]),
        ];

        /// <summary>One match: where it is, and how the geocoder spells it.</summary>
        public sealed record Place(double Latitude, double Longitude, string Address);

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
        public static string Match(params Place[] points)
        {
            var matches = points.Select(point =>
                $$"""{ "matchedAddress": "{{point.Address}}", "coordinates": { "x": {{point.Longitude}}, "y": {{point.Latitude}} } }""");

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

            /// <summary>The last request itself, for its headers.</summary>
            public HttpRequestMessage? LastRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                LastQuery = request.RequestUri?.Query;
                LastRequest = request;

                return reply(request, cancellationToken);
            }
        }
    }
}
