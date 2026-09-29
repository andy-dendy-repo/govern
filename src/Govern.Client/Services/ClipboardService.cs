using Microsoft.JSInterop;

namespace Govern.Client.Services;

/// <summary>
/// Copies text to the clipboard with the browser's asynchronous Clipboard API.
/// </summary>
public sealed class ClipboardService(IJSRuntime js)
{
    /// <summary>Copies <paramref name="text"/>.</summary>
    /// <param name="text">The text to copy.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// Whether the text was copied; <see langword="false"/> when the browser refused (no permission, an insecure page, or no
    /// Clipboard API), so the caller can ask the player to copy it by hand.
    /// </returns>
    public async Task<bool> CopyAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            await js.InvokeVoidAsync("navigator.clipboard.writeText", cancellationToken, text);
            return true;
        }
        catch (JSException)
        {
            return false;
        }
    }
}
