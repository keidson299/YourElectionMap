using Microsoft.Extensions.Logging.Abstractions;
using YourElectionMap.Server.Controllers;

namespace YourElectionMap.Server.Tests
{
    public class WeatherForecastControllerTests
    {
        [Fact]
        public void Get_ReturnsFiveForecasts()
        {
            var controller = new WeatherForecastController(NullLogger<WeatherForecastController>.Instance);

            var forecasts = controller.Get().ToList();

            Assert.Equal(5, forecasts.Count);
            Assert.All(forecasts, forecast => Assert.False(string.IsNullOrWhiteSpace(forecast.Summary)));
        }
    }
}
