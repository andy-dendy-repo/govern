using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Govern.ClientTests.Features.Games.Services;

public class GameScreenInteropServiceTests : ClientTestContext
{
    private GameScreenInteropService Interop => Services.GetRequiredService<GameScreenInteropService>();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrefersReducedMotionAsync_ModuleLoaded_ReturnsTheBrowsersAnswer(bool reduced)
    {
        var module = JSInterop.SetupModule(GameScreenInteropService.ModulePath);
        module.Setup<bool>("prefersReducedMotion", _ => true).SetResult(reduced);

        (await Interop.PrefersReducedMotionAsync(Ct)).ShouldBe(reduced);
    }

    [Fact]
    public async Task PrefersReducedMotionAsync_CallFails_ReturnsFalse()
    {
        var module = JSInterop.SetupModule(GameScreenInteropService.ModulePath);
        module.Setup<bool>("prefersReducedMotion", _ => true).SetException(new JSException("blocked"));

        (await Interop.PrefersReducedMotionAsync(Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task PrefersReducedMotionAsync_ImportFails_ReturnsFalseAndImportsAgainNextTime()
    {
        var js = new FailingImportRuntime();
        await using var interop = new GameScreenInteropService(js);

        (await interop.PrefersReducedMotionAsync(Ct)).ShouldBeFalse();
        (await interop.PrefersReducedMotionAsync(Ct)).ShouldBeFalse();

        js.Imports.ShouldBe(2);
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
