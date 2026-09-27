using System.Diagnostics.CodeAnalysis;
using Govern.Core.Errors;

namespace Govern.Core.Engine;

/// <summary>
/// Either the <see cref="GameUpdate"/> of an accepted command or the <see cref="GameError"/> that rejected it.
/// A rejected command never changes the state.
/// </summary>
public sealed class CommandResult
{
    private CommandResult(GameUpdate update) => Update = update;

    private CommandResult(GameError error) => Error = error;

    /// <summary><see langword="true"/> when the command was accepted and <see cref="Update"/> is set.</summary>
    [MemberNotNullWhen(true, nameof(Update))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>The new state and events, or <see langword="null"/> when the command was rejected.</summary>
    public GameUpdate? Update { get; }

    /// <summary>Why the command was rejected, or <see langword="null"/> when it was accepted.</summary>
    public GameError? Error { get; }

    /// <summary>Wraps an accepted command's update.</summary>
    /// <param name="update">The update.</param>
    public static implicit operator CommandResult(GameUpdate update) => FromUpdate(update);

    /// <summary>Wraps a rule violation.</summary>
    /// <param name="error">The error.</param>
    public static implicit operator CommandResult(GameError error) => FromError(error);

    /// <summary>Named alternative to the implicit conversion from <see cref="GameUpdate"/>.</summary>
    /// <param name="update">The update.</param>
    /// <returns>An accepted result.</returns>
    public static CommandResult FromUpdate(GameUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        return new CommandResult(update);
    }

    /// <summary>Named alternative to the implicit conversion from <see cref="GameError"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>A rejected result.</returns>
    public static CommandResult FromError(GameError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new CommandResult(error);
    }
}
