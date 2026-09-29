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
    /// <item>400 when the query itself is wrong, which only a bug can cause.</item>
    /// </list>
    /// The address arrives in the query string, as it already does in the page URL,
    /// and is not logged.
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
        [ProducesResponseType(typeof(DistrictLookup), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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

                lookup = await _lookups.LookupAddressAsync(typed, cancellationToken);
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

                lookup = await _lookups.LookupLocationAsync(
                    lat.Value,
                    lon.Value,
                    cancellationToken);
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
