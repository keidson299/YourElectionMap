using Microsoft.AspNetCore.Mvc;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Server.Controllers
{
    /// <summary>
    /// The one endpoint the result page calls. It takes either an address or a point,
    /// the same two things the page URL carries, and answers:
    /// <list type="bullet">
    /// <item>200 with a <see cref="DistrictLookup"/>, holding at least one district;</item>
    /// <item>404 when the place could not be found or has no districts;</item>
    /// <item>400 when the query itself is wrong, which only a hand-edited URL can send;</item>
    /// <item>503 when the geocoder did not answer, so the page can offer to try again.</item>
    /// </list>
    /// The address arrives in the query string, as it does in the page URL, and is not
    /// logged.
    /// </summary>
    [ApiController]
    [Route("api/districts")]
    public class DistrictsController : ControllerBase
    {
        private const int MaxAddressLength = 200;

        private readonly DistrictLookupService _lookups;

        public DistrictsController(DistrictLookupService lookups)
        {
            _lookups = lookups;
        }

        [HttpGet]
        // The answer names the address it was asked about, so no proxy, CDN or disk
        // cache should hold on to it.
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [ProducesResponseType(typeof(DistrictLookup), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
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
                    lookup = await _lookups.LookupAddressAsync(typed, cancellationToken);
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
                if (!IsOnEarth(lat.Value, lon.Value))
                {
                    ModelState.AddModelError(
                        nameof(lat),
                        "Latitude must be within ±90 and longitude within ±180.");

                    return ValidationProblem(ModelState);
                }

                lookup = _lookups.LookupLocation(lat.Value, lon.Value);
            }
            else
            {
                ModelState.AddModelError(
                    nameof(address),
                    "Give an address, or both lat and lon.");

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

        private static bool IsOnEarth(double latitude, double longitude) =>
            double.IsFinite(latitude)
            && double.IsFinite(longitude)
            && Math.Abs(latitude) <= 90
            && Math.Abs(longitude) <= 180;
    }
}
