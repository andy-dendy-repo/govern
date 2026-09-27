using Govern.Api;
using Govern.Api.Extensions;
using Govern.Api.Middlewares;
using Govern.Api.Models;
using Govern.Application;
using Govern.Application.Common.Models;
using Govern.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages(); // empty 4xx/5xx responses (e.g. 401 from authentication) get a ProblemDetails body
app.UseMiddleware<HostAllowListMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthChecks(HealthCheckRoutes.Live, new HealthCheckOptions { Predicate = static _ => false }).AllowAnonymous();
app.MapHealthChecks(HealthCheckRoutes.Ready, new HealthCheckOptions { Predicate = static check => check.Tags.Contains(HealthCheckTags.Ready) })
    .AllowAnonymous()
    .RequireRateLimiting(RateLimitingPolicies.Health);
app.MapEndpoints();

await app.RunAsync();
