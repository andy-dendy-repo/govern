using Govern.Application.Common.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Govern.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the real Api against the test Postgres container. Only external integrations are replaced (email).
/// </summary>
public sealed class GovernApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-tests-signing-key-0123456789";

    public FakeEmailService Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Govern", connectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");
        builder.UseSetting("RateLimiting:HealthPermitLimit", "10000");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);
        });
    }
}
