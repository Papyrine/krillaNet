namespace Krilla.Html.Styling;

/// <summary>What a <see cref="CssLength"/> holds.</summary>
enum LengthKind
{
    /// <summary>The property was <c>auto</c>.</summary>
    Auto,

    /// <summary>An absolute length, already resolved to CSS pixels.</summary>
    Absolute,

    /// <summary>A percentage, resolved against a containing block at use time.</summary>
    Percent,

    /// <summary>The property was <c>none</c>, as <c>max-width</c> allows.</summary>
    None
}

/// <summary>
/// A CSS length that may still depend on something not known at parse time.
/// </summary>
/// <remarks>
/// <para>
/// Font-relative units (<c>em</c>, <c>rem</c>, <c>ex</c>, <c>ch</c>) are resolved during parsing,
/// because the font context is known there. Percentages cannot be: <c>width: 50%</c> means half
/// the containing block, and the containing block is a layout result. So they survive into layout
/// and are resolved by <see cref="Resolve"/> at the point of use.
/// </para>
/// <para>
/// This is the reason lengths are not just <see cref="float"/>. AngleSharp.Css runs the cascade
/// but leaves relative units unresolved
/// (<see href="https://github.com/AngleSharp/AngleSharp.Css/issues/136">AngleSharp.Css#136</see>),
/// so resolution is ours to do and needs somewhere to record "not yet".
/// </para>
/// </remarks>
readonly record struct CssLength(LengthKind Kind, float Value)
{
    /// <summary><c>auto</c>.</summary>
    public static CssLength Auto => new(LengthKind.Auto, 0);

    /// <summary><c>none</c>.</summary>
    public static CssLength None => new(LengthKind.None, 0);

    /// <summary>Zero, absolute.</summary>
    public static CssLength Zero => new(LengthKind.Absolute, 0);

    /// <summary>An absolute length in CSS pixels.</summary>
    public static CssLength Pixels(float value) => new(LengthKind.Absolute, value);

    /// <summary>A percentage of the containing block.</summary>
    public static CssLength Percentage(float value) => new(LengthKind.Percent, value);

    /// <summary>Whether this is <c>auto</c>.</summary>
    public bool IsAuto => Kind == LengthKind.Auto;

    /// <summary>Whether this is <c>none</c>.</summary>
    public bool IsNone => Kind == LengthKind.None;

    /// <summary>
    /// Resolves against <paramref name="containing"/>, returning
    /// <paramref name="fallback"/> for <c>auto</c> and <c>none</c>.
    /// </summary>
    public float Resolve(float containing, float fallback = 0) =>
        Kind switch
        {
            LengthKind.Absolute => Value,
            LengthKind.Percent => containing * Value / 100f,
            _ => fallback
        };

    /// <summary>
    /// Resolves to a definite length, or null when the value is <c>auto</c> or <c>none</c>.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Resolve"/> because block layout has to branch on "was it auto",
    /// not merely substitute something: an auto width fills the containing block while an auto
    /// margin centres it, and neither is a fallback number.
    /// </remarks>
    public float? ResolveOrNull(float containing) =>
        Kind switch
        {
            LengthKind.Absolute => Value,
            LengthKind.Percent => containing * Value / 100f,
            _ => null
        };
}
