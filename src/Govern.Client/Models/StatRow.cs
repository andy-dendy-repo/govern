namespace Govern.Client.Models;

/// <summary>One row of a <c>StatTable</c>: an icon, what the number is, the number, and optionally where it comes from.</summary>
/// <param name="Icon">The symbol id (see <c>Helpers/Icons</c>).</param>
/// <param name="Label">What the number is ("Movement").</param>
/// <param name="Value">The number as shown ("1 of 2").</param>
/// <param name="Detail">The exact number and where it comes from, shown on hover, focus or tap; <see langword="null"/> for none.</param>
/// <param name="Class">A CSS class for the row, so tests and styles can find it.</param>
public sealed record StatRow(string Icon, string Label, string Value, string? Detail = null, string? Class = null);
