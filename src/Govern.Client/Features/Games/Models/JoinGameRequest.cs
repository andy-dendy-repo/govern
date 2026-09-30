namespace Govern.Client.Features.Games.Models;

/// <summary>
/// The join form and request body for <c>POST /api/games/join</c>. The code travels only in this body, never in a URL.
/// Settable so it can bind to a form.
/// </summary>
public sealed class JoinGameRequest
{
    /// <summary>The invite code, or an invite link pasted into the form.</summary>
    public string InviteCode { get; set; } = string.Empty;
}
