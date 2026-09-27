using Govern.Application.Common.Abstractions;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Govern.IntegrationTests.Pipeline;

public class AbortedRequestTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    // A client disconnect cancels HttpContext.RequestAborted, which flows through the mediator into EF Core and
    // cancels the database work. The request ends with 499 and is not reported as a server error.
    [Fact]
    public async Task ClientAbortsDuringHandler_StopsWorkWith499AndNoErrorLogs()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outcome = new RequestOutcome();
        using var factory = Fixture.Factory.WithWebHostBuilder(builder =>
        {
            // Production: no developer exception page, so the Api's own middleware must handle the abort.
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureLogging(logging => logging.AddFakeLogging());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IApplicationDbContextFactory>();
                services.AddScoped<IApplicationDbContextFactory>(_ => new HangingDbContextFactory(entered, cancelled));
                services.AddSingleton(outcome);
                services.AddSingleton<IStartupFilter, OutcomeRecordingStartupFilter>();
            });
        });
        using var client = factory.CreateClient();
        using var abort = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var request = UsersApi.SignInAsync(client, UsersApi.NewEmail(), UsersApi.Password, abort.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await abort.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () => await request);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var status = await outcome.StatusCode.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        status.ShouldBe(StatusCodes.Status499ClientClosedRequest);

        var logs = factory.Services.GetRequiredService<FakeLogCollector>().GetSnapshot();
        logs.ShouldNotBeEmpty();
        logs.Where(static r => r.Level >= LogLevel.Error).Select(static r => $"{r.Category}: {r.Message}").ShouldBeEmpty();
    }

    /// <summary>Simulates a slow database: waits until the request is cancelled and reports that it was.</summary>
    private sealed class HangingDbContextFactory(TaskCompletionSource entered, TaskCompletionSource cancelled) : IApplicationDbContextFactory
    {
        public async Task<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }

            throw new InvalidOperationException("Unreachable");
        }
    }

    public sealed class RequestOutcome
    {
        public TaskCompletionSource<int> StatusCode { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Outermost middleware: records the final status code the Api settled on.</summary>
    private sealed class OutcomeRecordingStartupFilter(RequestOutcome outcome) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                try
                {
                    await nextMiddleware(context);
                }
                finally
                {
                    outcome.StatusCode.TrySetResult(context.Response.StatusCode);
                }
            });
            next(app);
        };
    }
}
