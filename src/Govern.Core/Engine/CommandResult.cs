using System.Diagnostics.CodeAnalysis;

namespace Govern.Core.Engine;

/// <summary>
/// Either the <see cref="GameUpdate"/> of an accepted command or the <see cref="RuleViolation"/> that rejected it.
/// A rejected command never changes the state.
/// </summary>
public sealed class CommandResult
{
    private CommandResult(GameUpdate update) => Update = update;

    private CommandResult(RuleViolation violation) => Violation = violation;

    /// <summary><see langword="true"/> when the command was accepted and <see cref="Update"/> is set.</summary>
    [MemberNotNullWhen(true, nameof(Update))]
    [MemberNotNullWhen(false, nameof(Violation))]
    public bool IsSuccess => Violation is null;

    /// <summary>The new state and events, or <see langword="null"/> when the command was rejected.</summary>
    public GameUpdate? Update { get; }

    /// <summary>Why the command was rejected, or <see langword="null"/> when it was accepted.</summary>
    public RuleViolation? Violation { get; }

    /// <summary>Wraps an accepted command's update.</summary>
    /// <param name="update">The update.</param>
    public static implicit operator CommandResult(GameUpdate update) => FromUpdate(update);

    /// <summary>Wraps a rule violation.</summary>
    /// <param name="violation">The rule violation.</param>
    public static implicit operator CommandResult(RuleViolation violation) => FromViolation(violation);

    /// <summary>Named alternative to the implicit conversion from <see cref="GameUpdate"/>.</summary>
    /// <param name="update">The update.</param>
    /// <returns>An accepted result.</returns>
    public static CommandResult FromUpdate(GameUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        return new CommandResult(update);
    }

    /// <summary>Named alternative to the implicit conversion from <see cref="RuleViolation"/>.</summary>
    /// <param name="violation">The rule violation.</param>
    /// <returns>A rejected result.</returns>
    public static CommandResult FromViolation(RuleViolation violation)
    {
        ArgumentNullException.ThrowIfNull(violation);
        return new CommandResult(violation);
    }
}
