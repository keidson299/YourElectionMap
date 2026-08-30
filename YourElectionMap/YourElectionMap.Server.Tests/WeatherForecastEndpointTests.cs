using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace YourElectionMap.Server.Tests
{
    /// <summary>
    /// Spins up the real server in memory, so route wiring and JSON serialization are covered too.
    /// </summary>
    public class WeatherForecastEndpointTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public WeatherForecastEndpointTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetWeatherForecast_ReturnsOkWithForecasts()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/weatherforecast");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var forecasts = await response.Content.ReadFromJsonAsync<List<WeatherForecast>>();

            Assert.NotNull(forecasts);
            Assert.Equal(5, forecasts.Count);
        }
    }
}
