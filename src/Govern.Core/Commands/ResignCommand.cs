namespace Govern.Core.Commands;

/// <summary>
/// Gives up. Allowed at any time, not only on the actor's turn.
/// </summary>
public sealed record ResignCommand : GameCommand;
