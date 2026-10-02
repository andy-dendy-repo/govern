using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Govern.ClientTests.Features.Games.Services;

public class Board3DInteropServiceTests : ClientTestContext
{
    private static readonly ElementReference Host = new("board-3d");

    private Board3DInteropService Interop => Services.GetRequiredService<Board3DInteropService>();

    [Fact]
    public async Task MountAsync_WebGlRuns_PassesTheHostToEveryCall()
    {
        var module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        module.Setup<bool>("mount", _ => true).SetResult(true);
        module.SetupVoid("fit", _ => true).SetVoidResult();
        var interop = Interop;
        var rect = new BoardRect(0, 0, 10, 8);

        (await interop.MountAsync(Host, static _ => Task.CompletedTask, Ct)).ShouldBeTrue();
        await interop.FitAsync(rect, Ct);

        module.VerifyInvoke("mount").Arguments[0].ShouldBe(Host);
        module.VerifyInvoke("fit").Arguments.ShouldBe([Host, rect]);
    }

    [Fact]
    public async Task MountAsync_ModuleFails_ReturnsFalseAndLaterCallsDoNothing()
    {
        var js = new FailingImportRuntime();
        var interop = new Board3DInteropService(js);

        (await interop.MountAsync(Host, static _ => Task.CompletedTask, Ct)).ShouldBeFalse();
        await interop.ZoomAsync(1.2, Ct);
        await interop.DisposeAsync();

        js.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task MountAsync_DisposedWhileMounting_UnmountsAndReportsNotMounted()
    {
        var module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        var mount = module.Setup<bool>("mount", _ => true);
        module.SetupVoid("unmount", _ => true).SetVoidResult();
        var interop = Interop;

        var mounting = interop.MountAsync(Host, static _ => Task.CompletedTask, Ct);
        await interop.DisposeAsync();
        mount.SetResult(true);

        (await mounting).ShouldBeFalse();
        module.VerifyInvoke("unmount").Arguments.ShouldBe([Host]);
    }

    [Fact]
    public async Task RenderAsync_CallFails_KeepsTheBoard()
    {
        var module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        module.Setup<bool>("mount", _ => true).SetResult(true);
        module.SetupVoid("render", _ => true).SetException(new JSException("lost context"));
        var interop = Interop;
        await interop.MountAsync(Host, static _ => Task.CompletedTask, Ct);

        await Should.NotThrowAsync(() => interop.RenderAsync(new BoardScene([], new BoardRect(0, 0, 1, 1), new BoardRect(0, 0, 1, 1), [], [], [], []), Ct));
    }

    private sealed class FailingImportRuntime : IJSRuntime
    {
        public int Calls { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls++;
            return ValueTask.FromException<TValue>(new JSException("The module could not be loaded."));
        }
    }
}
