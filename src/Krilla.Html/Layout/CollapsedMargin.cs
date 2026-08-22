namespace Krilla.Html.Layout;

/// <summary>
/// A set of adjoining margins, reduced to the single margin they collapse into.
/// </summary>
/// <remarks>
/// <para>
/// Collapsing is not "take the larger": the rule is the largest positive plus the most negative,
/// so a 20px margin adjoining a -8px one yields 12px. Keeping the two extremes separately, rather
/// than a running total, is what makes the operation associative — margins collapse in any order
/// and through arbitrary nesting, and every path has to give the same answer.
/// </para>
/// <para>
/// A default instance is the identity: no margins, zero value.
/// </para>
/// </remarks>
readonly record struct CollapsedMargin(float Positive, float Negative)
{
    /// <summary>No margins.</summary>
    public static CollapsedMargin Empty => default;

    /// <summary>The single margin this set collapses into.</summary>
    public float Value => Positive + Negative;

    /// <summary>A set holding one margin.</summary>
    public static CollapsedMargin Of(float margin) =>
        Empty.With(margin);

    /// <summary>This set with <paramref name="margin"/> adjoined.</summary>
    public CollapsedMargin With(float margin)
    {
        if (margin >= 0)
        {
            return new(Math.Max(Positive, margin), Negative);
        }

        return new(Positive, Math.Min(Negative, margin));
    }

    /// <summary>This set with every margin in <paramref name="other"/> adjoined.</summary>
    public CollapsedMargin Merge(CollapsedMargin other) =>
        new(Math.Max(Positive, other.Positive), Math.Min(Negative, other.Negative));
}
