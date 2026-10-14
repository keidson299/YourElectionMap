using System.Globalization;
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
            // The Oregon State Capitol, Salem.
            ("Salem", [new(44.9385, -123.0301, "900 COURT ST NE, SALEM, OR, 97301")]),
            // The South Carolina State House, Columbia.
            ("Gervais", [new(34.0004, -81.0330, "1100 GERVAIS ST, COLUMBIA, SC, 29201")]),
            // The Washington Legislative Building, Olympia.
            ("Olympia", [new(47.0358, -122.9048, "416 SID SNYDER AVE SW, OLYMPIA, WA, 98504")]),
            // The Arizona State Capitol, Phoenix.
            ("Phoenix", [new(33.4481, -112.0970, "1700 W WASHINGTON ST, PHOENIX, AZ, 85007")]),
            // The Idaho State Capitol, Boise.
            ("Boise", [new(43.6177, -116.1996, "700 W JEFFERSON ST, BOISE, ID, 83702")]),
            // The Montana State Capitol, Helena.
            ("Helena", [new(46.5857, -112.0184, "1301 E 6TH AVE, HELENA, MT, 59601")]),
            // The Utah State Capitol, Salt Lake City.
            ("Salt", [new(40.7774, -111.8882, "350 N STATE ST, SALT LAKE CITY, UT, 84103")]),
            // The Wyoming State Capitol, Cheyenne.
            ("Cheyenne", [new(41.1403, -104.8202, "200 W 24TH ST, CHEYENNE, WY, 82001")]),
            // The Colorado State Capitol, Denver.
            ("Colfax", [new(39.7393, -104.9848, "200 E COLFAX AVE, DENVER, CO, 80203")]),
            // The New Mexico State Capitol, Santa Fe.
            ("Santa", [new(35.6824, -105.9397, "490 OLD SANTA FE TRL, SANTA FE, NM, 87501")]),
            // The Nevada State Capitol, Carson City.
            ("Carson", [new(39.1640, -119.7663, "101 N CARSON ST, CARSON CITY, NV, 89701")]),
            // The Oklahoma State Capitol, Oklahoma City.
            ("Oklahoma", [new(35.4922, -97.5034, "2300 N LINCOLN BLVD, OKLAHOMA CITY, OK, 73105")]),
            // The Texas State Capitol, Austin.
            ("Austin", [new(30.2747, -97.7403, "1100 CONGRESS AVE, AUSTIN, TX, 78701")]),
            // The White House, which is in no state the site covers.
            ("Pennsylvania", [new(38.8977, -77.0365, "1600 PENNSYLVANIA AVE NW, WASHINGTON, DC, 20500")]),
            // A town name with no state: Greenville MS first, then Greenville NC.
            ("Greenville",
            [
                new(33.4101, -91.0618, "100 MAIN ST, GREENVILLE, MS, 38701"),
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
            // In the invariant culture: a German machine would otherwise write -78,6398.
            var matches = points.Select(point =>
                $$"""{ "matchedAddress": "{{point.Address}}", "coordinates": { "x": {{point.Longitude.ToString(CultureInfo.InvariantCulture)}}, "y": {{point.Latitude.ToString(CultureInfo.InvariantCulture)}} } }""");

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
