namespace Govern.Client.Features.Games.Enums;

/// <summary>
/// Why a game's event stream ended (the last event of <c>GamesClient.WatchAsync</c>).
/// </summary>
public enum GameStreamEnd
{
    /// <summary>The Api ended the stream normally: the game finished, the caller can no longer read it, or the token expired.</summary>
    Closed,

    /// <summary>The game does not exist or the caller does not play in it (404).</summary>
    NotFound,

    /// <summary>The session is gone and could not be refreshed (401).</summary>
    Unauthorized,

    /// <summary>Too many streams are open or were opened recently (429).</summary>
    RateLimited,

    /// <summary>Anything else: a network or server error, an unavailable token refresh, or unreadable data.</summary>
    Failed,
}
