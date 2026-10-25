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
            // The Kansas State Capitol, Topeka.
            ("Topeka", [new(39.0481, -95.6780, "300 SW 10TH AVE, TOPEKA, KS, 66612")]),
            // The Minnesota State Capitol, St. Paul.
            ("Paul", [new(44.9551, -93.1022, "75 REV DR MARTIN LUTHER KING JR BLVD, ST PAUL, MN, 55155")]),
            // The North Dakota State Capitol, Bismarck.
            ("Bismarck", [new(46.8209, -100.7827, "600 E BOULEVARD AVE, BISMARCK, ND, 58505")]),
            // The Nebraska State Capitol, Lincoln: by the state's name, since Oklahoma's
            // capitol is on a Lincoln Boulevard.
            ("Nebraska", [new(40.8081, -96.6997, "1445 K ST, LINCOLN, NE, 68508")]),
            // The South Dakota State Capitol, Pierre.
            ("Pierre", [new(44.3671, -100.3462, "500 E CAPITOL AVE, PIERRE, SD, 57501")]),
            // The Arkansas State Capitol, Little Rock.
            ("Woodlane", [new(34.7466, -92.2890, "500 WOODLANE ST, LITTLE ROCK, AR, 72201")]),
            // The Iowa State Capitol, Des Moines.
            ("Moines", [new(41.5912, -93.6038, "1007 E GRAND AVE, DES MOINES, IA, 50319")]),
            // The Louisiana State Capitol, Baton Rouge.
            ("Baton", [new(30.4571, -91.1874, "900 N 3RD ST, BATON ROUGE, LA, 70802")]),
            // The Missouri State Capitol, Jefferson City: by the state's name, since Boise's
            // capitol is on a Jefferson Street.
            ("Missouri", [new(38.5792, -92.1729, "201 W CAPITOL AVE, JEFFERSON CITY, MO, 65101")]),
            // The Mississippi State Capitol, Jackson.
            ("Jackson", [new(32.3036, -90.1823, "400 HIGH ST, JACKSON, MS, 39201")]),
            // The Alabama State Capitol, Montgomery.
            ("Montgomery", [new(32.3771, -86.3008, "600 DEXTER AVE, MONTGOMERY, AL, 36104")]),
            // The Florida Capitol, Tallahassee.
            ("Tallahassee", [new(30.4381, -84.2816, "400 S MONROE ST, TALLAHASSEE, FL, 32399")]),
            // The Georgia State Capitol, Atlanta.
            ("Atlanta", [new(33.7490, -84.3880, "206 WASHINGTON ST SW, ATLANTA, GA, 30334")]),
            // The Kentucky State Capitol, Frankfort.
            ("Frankfort", [new(38.1867, -84.8753, "700 CAPITAL AVE, FRANKFORT, KY, 40601")]),
            // The Tennessee State Capitol, Nashville.
            ("Nashville", [new(36.1658, -86.7844, "600 DR MARTIN L KING JR BLVD, NASHVILLE, TN, 37243")]),
            // The Illinois State Capitol, Springfield.
            ("Springfield", [new(39.7983, -89.6544, "401 S 2ND ST, SPRINGFIELD, IL, 62701")]),
            // The Indiana Statehouse, Indianapolis.
            ("Indianapolis", [new(39.7686, -86.1625, "200 W WASHINGTON ST, INDIANAPOLIS, IN, 46204")]),
            // The Michigan State Capitol, Lansing.
            ("Lansing", [new(42.7336, -84.5555, "100 N CAPITOL AVE, LANSING, MI, 48933")]),
            // The Ohio Statehouse, Columbus.
            ("Columbus", [new(39.9612, -82.9988, "1 CAPITOL SQ, COLUMBUS, OH, 43215")]),
            // The Wisconsin State Capitol, Madison.
            ("Madison", [new(43.0747, -89.3842, "2 E MAIN ST, MADISON, WI, 53703")]),
            // Legislative Hall, Dover.
            ("Dover", [new(39.1573, -75.5197, "411 LEGISLATIVE AVE, DOVER, DE, 19901")]),
            // The Maryland State House, Annapolis.
            ("Annapolis", [new(38.9787, -76.4910, "100 STATE CIR, ANNAPOLIS, MD, 21401")]),
            // The Pennsylvania State Capitol, Harrisburg.
            ("Harrisburg", [new(40.2645, -76.8836, "501 N 3RD ST, HARRISBURG, PA, 17120")]),
            // The Virginia State Capitol, Richmond.
            ("Richmond", [new(37.5385, -77.4336, "1000 BANK ST, RICHMOND, VA, 23219")]),
            // The West Virginia State Capitol, Charleston: by its street, since South
            // Carolina has a Charleston too.
            ("Kanawha", [new(38.3365, -81.6123, "1900 KANAWHA BLVD E, CHARLESTON, WV, 25305")]),
            // The White House, which is in no state the site covers.
            ("Pennsylvania", [new(38.8977, -77.0365, "1600 PENNSYLVANIA AVE NW, WASHINGTON, DC, 20500")]),
            // A town name with no state: Greenville ME first, then Greenville NC.
            ("Greenville",
            [
                new(45.4595, -69.5906, "100 MAIN ST, GREENVILLE, ME, 04441"),
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
