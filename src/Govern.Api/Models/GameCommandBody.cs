using System.Text;
using System.Text.Json;
using Govern.Core.Engine;
using Govern.Core.Serialization;

namespace Govern.Api.Models;

/// <summary>
/// The body of <c>POST /api/games/{id}/commands</c>. Bound with <see cref="BindAsync"/>, which reads it with
/// <see cref="GameSerializer"/> (the one game JSON format) instead of the HTTP JSON defaults: those accept a command's
/// <c>type</c> only in first position and turn a missing <c>type</c> into a server error.
/// </summary>
/// <param name="ExpectedVersion">The <c>view.version</c> the command is based on.</param>
/// <param name="Command">The command, with its <c>type</c> discriminator (e.g. <c>end_turn</c>, <c>move_unit</c>).</param>
public sealed record GameCommandBody(long ExpectedVersion, GameCommand Command)
{
    /// <summary>Largest accepted body in bytes; a command is a few dozen bytes.</summary>
    public const int MaxBytes = 4 * 1024;

    /// <summary>Reads and deserializes the request body (minimal API custom binding).</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The body.</returns>
    /// <exception cref="BadHttpRequestException">
    /// 415 for a non-JSON content type, 413 for a body over <see cref="MaxBytes"/>, 400 for anything that is not a valid body.
    /// </exception>
    public static async ValueTask<GameCommandBody?> BindAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        if (!request.HasJsonContentType())
        {
            throw new BadHttpRequestException("Expected a JSON body.", StatusCodes.Status415UnsupportedMediaType);
        }

        var json = await ReadLimitedAsync(request, context.RequestAborted);
        try
        {
            return GameSerializer.Deserialize<GameCommandBody>(json);
        }
        catch (JsonException exception)
        {
            throw new BadHttpRequestException("The command is not valid JSON for a game command.", exception);
        }
    }

    // Reads at most MaxBytes (checked up front for a declared length and while reading for a chunked body).
    private static async Task<string> ReadLimitedAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBytes)
        {
            throw new BadHttpRequestException("The command is too large.", StatusCodes.Status413PayloadTooLarge);
        }

        var buffer = new byte[MaxBytes + 1];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await request.Body.ReadAsync(buffer.AsMemory(length), cancellationToken)) > 0)
        {
            length += read;
        }

        if (length > MaxBytes)
        {
            throw new BadHttpRequestException("The command is too large.", StatusCodes.Status413PayloadTooLarge);
        }

        return Encoding.UTF8.GetString(buffer, 0, length);
    }
}
