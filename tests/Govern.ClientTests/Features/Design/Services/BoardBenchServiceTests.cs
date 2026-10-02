using Bunit;
using Govern.Client.Features.Design.Models;
using Govern.Client.Features.Design.Services;
using Govern.Client.Features.Games.Services;

namespace Govern.ClientTests.Features.Design.Services;

public class BoardBenchServiceTests : BunitContext
{
    [Fact]
    public async Task MeasureAsync_AskTheBoardModule_ReturnsItsNumbers()
    {
        var numbers = new BoardBenchResult(0, 5, new Dictionary<string, double> { ["pieces"] = 2 }, 60, 16, 20, 1, 1, 129, 62, 1000, 1, 2048, new Dictionary<string, int> { ["terrain"] = 10 });
        var module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        module.Setup<BoardBenchResult?>("measure").SetResult(numbers);
        await using var bench = new BoardBenchService(JSInterop.JSRuntime);

        var result = await bench.MeasureAsync(Xunit.TestContext.Current.CancellationToken);

        result.ShouldBe(numbers);
    }

    [Fact]
    public async Task MeasureAsync_NoBoard_ReturnsNull()
    {
        var module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        module.Setup<BoardBenchResult?>("measure").SetResult(null);
        await using var bench = new BoardBenchService(JSInterop.JSRuntime);

        (await bench.MeasureAsync(Xunit.TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
