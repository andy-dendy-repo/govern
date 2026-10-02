using System.Diagnostics;
using System.Globalization;
using Govern.Client.Features.Design.Helpers;
using Govern.Client.Features.Design.Models;
using Govern.Client.Features.Design.Services;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Govern.Client.Features.Design.Pages;

/// <summary>
/// The 3D board bench at <c>/design/board</c> (F15 step 5, Development only): the board on the largest map, built by
/// <see cref="BoardBenchBuilder"/>, a march to send a new view, a tap to select a unit (to try touch), and the numbers from
/// <see cref="BoardBenchService"/>.
/// </summary>
public sealed partial class BoardBenchPage
{
    private const int SceneRuns = 5;

    private readonly GameUiService _ui = new();
    private PlayerView _view = null!;
    private bool _revealed = true;
    private bool _measuring;
    private bool _missing;
    private BoardBenchResult? _result;

    [Inject] private IWebAssemblyHostEnvironment Environment { get; set; } = null!;

    [Inject] private RulesetService Rulesets { get; set; } = null!;

    [Inject] private BoardBenchService Bench { get; set; } = null!;

    private Ruleset Ruleset => Rulesets.Ruleset;

    /// <inheritdoc />
    public void Dispose() => _ui.Changed -= OnUiChanged;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _view = BoardBenchBuilder.Build(Ruleset, _revealed);
        _ui.ToggleBoard3D();
        _ui.Changed += OnUiChanged;
    }

    private void OnUiChanged() => InvokeAsync(StateHasChanged);

    private void ToggleRevealed()
    {
        _revealed = !_revealed;
        _view = BoardBenchBuilder.Build(Ruleset, _revealed) with { Version = _view.Version + 1 };
        _ui.Select(Ruleset, _view, BoardSelection.None);
        _result = null;
    }

    private void March()
    {
        _view = BoardBenchBuilder.March(Ruleset, _view);
        _ui.Select(Ruleset, _view, BoardSelection.None);
    }

    // A tap on one of the bench player's units selects it (its reachable tiles light up); anywhere else clears the selection.
    private void Select(HexCoord tile)
    {
        var unit = _view.Units.FirstOrDefault(unit => unit.Position == tile && unit.Owner == _view.Viewer);
        _ui.Select(Ruleset, _view, unit is null ? BoardSelection.None : BoardSelection.Of(unit.Id));
    }

    private async Task MeasureAsync()
    {
        _measuring = true;
        try
        {
            var watch = Stopwatch.StartNew();
            for (var run = 0; run < SceneRuns; run++)
            {
                BoardSceneBuilder.Build(_view, Ruleset.Combat.MaxHealth);
            }

            var sceneBuild = watch.Elapsed.TotalMilliseconds / SceneRuns;
            var result = await Bench.MeasureAsync(CancellationToken.None);
            _missing = result is null;
            _result = result is null ? null : result with { SceneBuild = sceneBuild };
        }
        finally
        {
            _measuring = false;
        }
    }

    private static string Ms(double milliseconds) => $"{milliseconds.ToString("0.0", CultureInfo.InvariantCulture)} ms";
}
