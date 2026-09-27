namespace Govern.Core.Commands;

/// <summary>
/// Ends the actor's turn: their cities produce, and the next active player moves.
/// </summary>
public sealed record EndTurnCommand : GameCommand;
