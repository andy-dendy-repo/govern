using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Govern.IntegrationTests;

public class WeatherForecastTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetWeatherForecast_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/weatherforecast", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
