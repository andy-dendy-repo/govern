using System.Reflection;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Govern.Core.Map;
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

        (await interop.MountAsync(Host, new BoardEvents(static _ => Task.CompletedTask), Ct)).ShouldBeTrue();
        await interop.FitAsync(rect, Ct);

        module.VerifyInvoke("mount").Arguments[0].ShouldBe(Host);
        module.VerifyInvoke("fit").Arguments.ShouldBe([Host, rect]);
    }

    [Fact]
    public async Task MountAsync_ModuleFails_ReturnsFalseAndLaterCallsDoNothing()
    {
        var js = new FailingImportRuntime();
        var interop = new Board3DInteropService(js);

        (await interop.MountAsync(Host, new BoardEvents(static _ => Task.CompletedTask), Ct)).ShouldBeFalse();
        await interop.ZoomAsync(1.2, Ct);
        await interop.RenderOverlayAsync(BoardOverlay.None, Ct);
        await interop.RenderMotionAsync(SceneMotionBuilder.Build(BoardMotion.None, null), Ct);
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

        var mounting = interop.MountAsync(Host, new BoardEvents(static _ => Task.CompletedTask), Ct);
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
        await interop.MountAsync(Host, new BoardEvents(static _ => Task.CompletedTask), Ct);

        await Should.NotThrowAsync(() => interop.RenderAsync(new BoardScene([], new BoardRect(0, 0, 1, 1), new BoardRect(0, 0, 1, 1), [], [], [], [], []), Ct));
    }

    [Fact]
    public async Task MountAsync_RendererReports_ReachTheirCallbacks()
    {
        var module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        module.Setup<bool>("mount", _ => true).SetResult(true);
        var reports = new List<string>();
        var events = new BoardEvents(
            tile => Record($"clicked {tile}"),
            tile => Record($"hovered {tile?.ToString() ?? "none"}"),
            lost => Record(lost ? "lost" : "restored"));
        await Interop.MountAsync(Host, events, Ct);
        var relay = module.VerifyInvoke("mount").Arguments[1]!;

        await Report(relay, "OnTileClicked", 1, 2);
        await Report(relay, "OnTileHovered", 3, 4);
        await Report(relay, "OnHoverEnded");
        await Report(relay, "OnContextLost");
        await Report(relay, "OnContextRestored");

        reports.ShouldBe([$"clicked {new HexCoord(1, 2)}", $"hovered {new HexCoord(3, 4)}", "hovered none", "lost", "restored"]);

        Task Record(string report)
        {
            reports.Add(report);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task MountAsync_NoHoverOrContextCallbacks_IgnoresThoseReports()
    {
        var module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        module.Setup<bool>("mount", _ => true).SetResult(true);
        await Interop.MountAsync(Host, new BoardEvents(static _ => Task.CompletedTask), Ct);
        var relay = module.VerifyInvoke("mount").Arguments[1]!;

        await Should.NotThrowAsync(async () =>
        {
            await Report(relay, "OnTileHovered", 3, 4);
            await Report(relay, "OnContextLost");
        });
    }

    [Fact]
    public async Task SetPointerAsync_Mounted_TellsTheRenderer()
    {
        var module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        module.Setup<bool>("mount", _ => true).SetResult(true);
        module.SetupVoid("setPointer", _ => true).SetVoidResult();
        var interop = Interop;
        await interop.MountAsync(Host, new BoardEvents(static _ => Task.CompletedTask), Ct);

        await interop.SetPointerAsync(true, Ct);

        module.VerifyInvoke("setPointer").Arguments.ShouldBe([Host, true]);
    }

    // What the renderer does: it calls a method of the .NET object it was mounted with.
    private static Task Report(object relay, string method, params object[] args)
    {
        var target = relay.GetType().GetProperty("Value")!.GetValue(relay)!;
        return (Task)target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance)!.Invoke(target, args)!;
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
