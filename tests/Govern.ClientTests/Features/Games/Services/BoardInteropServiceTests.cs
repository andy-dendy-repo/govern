using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Govern.ClientTests.Features.Games.Services;

public class BoardInteropServiceTests : ClientTestContext
{
    private static readonly ElementReference Board = new("board-1");

    private BoardInteropService Interop => Services.GetRequiredService<BoardInteropService>();

    [Fact]
    public async Task InitAndMeasure_ModuleLoaded_ImportTheModuleOnceAndPassTheElement()
    {
        var module = JSInterop.SetupModule(BoardInteropService.ModulePath);
        module.SetupVoid("init", _ => true).SetVoidResult();
        module.Setup<BoardRect?>("measure", _ => true).SetResult(new BoardRect(10, 20, 640, 480));

        await Interop.InitAsync(Board, Ct);
        var rect = await Interop.MeasureAsync(Board, Ct);

        rect.ShouldBe(new BoardRect(10, 20, 640, 480));
        module.VerifyInvoke("init").Arguments.ShouldBe([Board]);
        module.VerifyInvoke("measure").Arguments.ShouldBe([Board]);
        JSInterop.Invocations.Count(static invocation => invocation.Identifier == "import").ShouldBe(1);
    }

    [Fact]
    public async Task MeasureAsync_CallFails_ReturnsNullAndKeepsTheModule()
    {
        var module = JSInterop.SetupModule(BoardInteropService.ModulePath);
        module.Setup<BoardRect?>("measure", _ => true).SetException(new JSException("detached"));

        (await Interop.MeasureAsync(Board, Ct)).ShouldBeNull();
        (await Interop.MeasureAsync(Board, Ct)).ShouldBeNull();

        JSInterop.Invocations.Count(static invocation => invocation.Identifier == "import").ShouldBe(1);
    }

    [Fact]
    public async Task MeasureAsync_ImportFails_ReturnsNullAndImportsAgainNextTime()
    {
        var js = new FailingImportRuntime();
        var interop = new BoardInteropService(js);

        (await interop.MeasureAsync(Board, Ct)).ShouldBeNull();
        (await interop.MeasureAsync(Board, Ct)).ShouldBeNull();

        js.Imports.ShouldBe(2);
    }

    [Fact]
    public async Task InitAsync_ModuleFails_DoesNotThrow()
    {
        var module = JSInterop.SetupModule(BoardInteropService.ModulePath);
        module.SetupVoid("init", _ => true).SetException(new JSException("blocked"));

        await Should.NotThrowAsync(() => Interop.InitAsync(Board, Ct));
    }

    // bUnit only fakes successful imports, so a failed one needs its own runtime.
    private sealed class FailingImportRuntime : IJSRuntime
    {
        public int Imports { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Imports++;
            return ValueTask.FromException<TValue>(new JSException("The module could not be loaded."));
        }
    }
}
