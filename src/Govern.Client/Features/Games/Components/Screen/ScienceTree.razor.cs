using Govern.Client.Features.Games.Services;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.Science;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components.Screen;

/// <summary>The science tree over the board: the player's science budget and every node with its state and Unlock button.</summary>
public partial class ScienceTree
{
    /// <summary>The game screen's UI state, which opens and closes the tree.</summary>
    [CascadingParameter]
    public GameUiService Ui { get; set; } = null!;

    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The game's ruleset.</summary>
    [Parameter, EditorRequired]
    public Ruleset Ruleset { get; set; } = null!;

    /// <summary>Whether a command is in flight (the buttons are disabled).</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Raised with the command a button sends.</summary>
    [Parameter]
    public EventCallback<GameCommand> Command { get; set; }

    private ScienceView Science => View.Science ?? new ScienceView(0);

    private string BudgetText
    {
        get
        {
            var used = ScienceCalculator.Used(Ruleset, Science.NodeList);
            return $"Your items take {used} of the {Science.Income} science your cities make a turn; {Math.Max(0, Science.Income - used)} free.";
        }
    }

    private NodeState StateOf(ScienceNode node) => Science.NodeList.FirstOrDefault(owned => owned.Node == node.Id) switch
    {
        { Disabled: true } => new(true, "off", "Switched off: your cities make too little science for it.", null),
        not null => new(true, "working", "Working.", null),
        null => SciencePreviewCalculator.CheckUnlockNode(Ruleset, View, node.Id) is { } problem
            ? new(false, "locked", null, problem)
            : new(false, "available", null, null),
    };

    private string UnitText(UnitType unit) => Ruleset.Unit(unit).Replaces is { } old
        ? $"{Ruleset.UnitName(unit)}, replaces the {Ruleset.UnitName(old)}"
        : Ruleset.UnitName(unit);

    private string BuildingText(BuildingType building) => Ruleset.Building(building).Upgrades is { } old
        ? $"{Ruleset.BuildingName(building)}, a builder upgrades the {Ruleset.BuildingName(old)}"
        : Ruleset.BuildingName(building);

    // A node's state on screen: whether the player owns it, its CSS class, its text when owned and why it can't be unlocked.
    private sealed record NodeState(bool Owned, string Class, string? Text, RuleViolation? Problem);
}
