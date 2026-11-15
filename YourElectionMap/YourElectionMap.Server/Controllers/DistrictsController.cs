using Microsoft.AspNetCore.Mvc;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Controllers
{
    /// <summary>
    /// The one endpoint: 200 with a <see cref="DistrictLookup"/>, 404 when nothing can be
    /// placed or found, 400 for an unusable query, and 503 when the geocoder did not
    /// answer, so the page offers to try again rather than doubt the address.
    /// </summary>
    [ApiController]
    [Route("api/districts")]
    public class DistrictsController(
        DistrictData districts,
        CensusGeocoder geocoder,
        GoogleVoterInfo voterInfo) : ControllerBase
    {
        private const int MaxAddressLength = 200;

        [HttpGet]
        // The answer names the address, so no proxy, CDN or disk cache should keep it.
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<ActionResult<DistrictLookup>> Get(
            [FromQuery] string? address,
            [FromQuery] double? lat,
            [FromQuery] double? lon,
            CancellationToken cancellationToken)
        {
            var typed = address?.Trim();
            DistrictLookup? lookup;

            // An address wins over coordinates, as it does in the client's parseRoute.
            if (!string.IsNullOrEmpty(typed))
            {
                if (typed.Length > MaxAddressLength)
                {
                    ModelState.AddModelError(
                        nameof(address),
                        $"An address can be at most {MaxAddressLength} characters.");

                    return ValidationProblem(ModelState);
                }

                try
                {
                    lookup = await LookupAddressAsync(typed, cancellationToken);
                }
                catch (GeocoderUnavailableException)
                {
                    return Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Address lookup unavailable",
                        detail: "The address service did not answer. Try again in a moment.");
                }
            }
            else if (lat is not null && lon is not null)
            {
                if (!double.IsFinite(lat.Value) || !double.IsFinite(lon.Value)
                    || Math.Abs(lat.Value) > 90 || Math.Abs(lon.Value) > 180)
                {
                    ModelState.AddModelError(
                        nameof(lat),
                        "Latitude must be within ±90 and longitude within ±180.");

                    return ValidationProblem(ModelState);
                }

                lookup = At(lat.Value, lon.Value, "Your location");
            }
            else
            {
                ModelState.AddModelError(nameof(address), "Give an address, or both lat and lon.");

                return ValidationProblem(ModelState);
            }

            if (lookup is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "No districts found",
                    detail: "That address or location could not be placed in any district.");
            }

            return lookup;
        }

        /// <summary>
        /// An address with no state can match in several places; the first inside a
        /// covered state wins. An address outside the fifty states is placed, then found
        /// in no district: the same null as one that could not be placed.
        /// </summary>
        private async Task<DistrictLookup?> LookupAddressAsync(
            string address,
            CancellationToken cancellationToken)
        {
            // Named as typed: the geocoder's spelling is in capitals, and no more correct.
            var line = address.Split(',')[0].Trim();
            var label = line.Length == 0 ? address : line;

            foreach (var (latitude, longitude, matched) in await geocoder.FindAsync(address, cancellationToken))
            {
                if (At(latitude, longitude, label) is { } lookup)
                {
                    // Google is asked about the geocoder's spelling, which names the state,
                    // so it cannot place a "100 Main St, Greenville" somewhere else.
                    return lookup with
                    {
                        Election = await voterInfo.GetAsync(matched, cancellationToken),
                    };
                }
            }

            return null;
        }

        private DistrictLookup? At(double latitude, double longitude, string label) =>
            districts.Covering(longitude, latitude) is { } found
                ? new DistrictLookup(label, [longitude, latitude], found, Election: null)
                : null;
    }
}
