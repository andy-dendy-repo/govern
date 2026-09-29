using Govern.Client.Services;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Govern.ClientTests.Services;

public class ClipboardServiceTests : ClientTestContext
{
    private ClipboardService Clipboard => Services.GetRequiredService<ClipboardService>();

    [Fact]
    public async Task CopyAsync_Allowed_WritesTheTextAndReturnsTrue()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", "hello").SetVoidResult();

        (await Clipboard.CopyAsync("hello", Ct)).ShouldBeTrue();

        JSInterop.VerifyInvoke("navigator.clipboard.writeText").Arguments.ShouldBe(["hello"]);
    }

    [Fact]
    public async Task CopyAsync_BrowserRefuses_ReturnsFalse()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", "hello").SetException(new JSException("NotAllowedError"));

        (await Clipboard.CopyAsync("hello", Ct)).ShouldBeFalse();
    }
}
