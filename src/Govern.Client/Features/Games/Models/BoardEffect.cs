using Govern.Client.Features.Games.Enums;

namespace Govern.Client.Features.Games.Models;

/// <summary>A short effect on the board.</summary>
/// <param name="Key">Unique while it is shown (a render key, which also restarts its CSS animation).</param>
/// <param name="Kind">What is drawn.</param>
/// <param name="At">Where.</param>
/// <param name="Text">The number shown by a <see cref="BoardEffectKind.Damage"/> effect.</param>
public sealed record BoardEffect(int Key, BoardEffectKind Kind, BoardPoint At, string? Text = null);
