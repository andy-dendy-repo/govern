using Microsoft.JSInterop.Infrastructure;
using System.Collections.Concurrent;

namespace Govern.ClientTests.Support;

/// <summary>
/// Answers the <c>localStorage</c> JS calls made by <c>TokenStore</c> from an in-memory dictionary, so tests see what the
/// app wrote (bUnit's planned invocations only return fixed results).
/// </summary>
public sealed class FakeBrowserStorage
{
    private readonly ConcurrentDictionary<string, string> _items = new();

    public FakeBrowserStorage(BunitJSInterop jsInterop)
    {
        jsInterop.AddInvocationHandler(new GetItemHandler(_items));
        jsInterop.AddInvocationHandler(new ChangeItemHandler(_items));
    }

    /// <summary>The stored value, or <see langword="null"/>.</summary>
    public string? this[string key]
    {
        get => _items.TryGetValue(key, out var value) ? value : null;
        set
        {
            if (value is null)
            {
                _items.TryRemove(key, out _);
            }
            else
            {
                _items[key] = value;
            }
        }
    }

    private sealed class GetItemHandler(ConcurrentDictionary<string, string> items)
        : JSRuntimeInvocationHandlerBase<string?>(static invocation => invocation.Identifier == "localStorage.getItem", isCatchAllHandler: false)
    {
        protected override Task<string?> HandleAsync(JSRuntimeInvocation invocation) =>
            Task.FromResult(items.TryGetValue((string)invocation.Arguments[0]!, out var value) ? value : null);
    }

    private sealed class ChangeItemHandler(ConcurrentDictionary<string, string> items)
        : JSRuntimeInvocationHandler(
            static invocation => invocation.Identifier is "localStorage.setItem" or "localStorage.removeItem",
            isCatchAllHandler: false)
    {
        protected override Task<IJSVoidResult> HandleAsync(JSRuntimeInvocation invocation)
        {
            var key = (string)invocation.Arguments[0]!;
            if (invocation.Identifier == "localStorage.setItem")
            {
                items[key] = (string)invocation.Arguments[1]!;
            }
            else
            {
                items.TryRemove(key, out _);
            }

            return Task.FromResult<IJSVoidResult>(null!);
        }
    }
}
