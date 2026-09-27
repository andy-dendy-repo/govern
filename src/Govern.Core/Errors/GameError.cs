namespace Govern.Core.Errors;

/// <summary>
/// A command that the rules reject. Expected during play (a stale client, an illegal click), so it is returned, never thrown.
/// </summary>
/// <param name="Code">Stable, snake_case code namespaced by topic (e.g. <c>move.unreachable</c>); clients may depend on it.</param>
/// <param name="Message">Human-readable reason, safe to show to the player.</param>
public sealed record GameError(string Code, string Message);
