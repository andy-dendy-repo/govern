using Govern.Client.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Govern.ClientTests.Auth;

public class WebLocksCrossTabLockTests : BunitContext
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private WebLocksCrossTabLock CreateLock() => new(Services.GetRequiredService<IJSRuntime>());

    [Fact]
    public async Task RunAsync_LockGranted_RunsTheActionInsideTheLockAndReturnsItsResult()
    {
        var runExclusive = JSInterop.SetupModule(WebLocksCrossTabLock.ModulePath).SetupVoid("runExclusive", static _ => true);
        var sut = CreateLock();

        var run = sut.RunAsync(static () => Task.FromResult(42), Ct);
        var invocation = runExclusive.VerifyInvoke("runExclusive");
        invocation.Arguments[0].ShouldBe(WebLocksCrossTabLock.LockName);
        var callback = invocation.Arguments[1].ShouldBeOfType<DotNetObjectReference<LockedAction<int>>>();
        await callback.Value.RunLockedAsync();
        runExclusive.SetVoidResult();

        (await run).ShouldBe(42);
    }

    [Fact]
    public async Task RunAsync_ModuleReturnsWithoutRunningTheCallback_ThrowsUnavailableWithoutRunningTheAction()
    {
        JSInterop.SetupModule(WebLocksCrossTabLock.ModulePath).SetupVoid("runExclusive", static _ => true).SetVoidResult();
        var runs = 0;

        await Should.ThrowAsync<CrossTabLockUnavailableException>(() => CreateLock().RunAsync(() => Task.FromResult(++runs), Ct));

        runs.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_LockRefused_ThrowsUnavailableWithoutRunningTheAction()
    {
        JSInterop.SetupModule(WebLocksCrossTabLock.ModulePath).SetupVoid("runExclusive", static _ => true)
            .SetException(new JSException("SecurityError"));
        var runs = 0;

        await Should.ThrowAsync<CrossTabLockUnavailableException>(() => CreateLock().RunAsync(() => Task.FromResult(++runs), Ct));

        runs.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ActionThrows_RethrowsTheOriginalException()
    {
        var runExclusive = JSInterop.SetupModule(WebLocksCrossTabLock.ModulePath).SetupVoid("runExclusive", static _ => true);
        var sut = CreateLock();

        var run = sut.RunAsync<int>(static () => throw new TimeoutException("storage"), Ct);
        var callback = runExclusive.VerifyInvoke("runExclusive").Arguments[1].ShouldBeOfType<DotNetObjectReference<LockedAction<int>>>();
        await callback.Value.RunLockedAsync();
        runExclusive.SetVoidResult();

        (await Should.ThrowAsync<TimeoutException>(() => run)).Message.ShouldBe("storage");
    }
}
