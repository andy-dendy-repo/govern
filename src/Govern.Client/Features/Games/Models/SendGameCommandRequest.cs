using Govern.Core.Engine;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// The body of <c>POST /api/games/{id}/commands</c> (the game id is in the path).
/// </summary>
/// <param name="ExpectedVersion">
/// The <c>view.version</c> the command is based on; the Api rejects the command with <c>game.stale_version</c> when the
/// caller's view has changed since.
/// </param>
/// <param name="Command">The command; serialized with its <c>type</c> discriminator (e.g. <c>move_unit</c>).</param>
public sealed record SendGameCommandRequest(long ExpectedVersion, GameCommand Command);
