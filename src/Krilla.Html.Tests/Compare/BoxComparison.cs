/// <summary>
/// Compares our layout geometry to the browser's, element by element.
///
/// This is the corpus's primary signal. A pixel diff of a text-heavy page has a noise floor — two
/// rasterisers disagree about glyph edges however correct the layout is — so it can say "close"
/// but never "right". Box geometry has no such floor: a correct layout produces zeros, and a wrong
/// one names the element and the axis. "p:nth-child(3) is 14px low" is a defect report; "the page
/// is 4% different" is not.
///
/// Both sides are keyed by the selector path <see cref="Krilla.Html.Layout.SelectorPath"/> builds,
/// which the reference generator's harvesting script reproduces by the same walk. Anonymous boxes
/// have no selector and are absent from both sides by construction.
/// </summary>
static class BoxComparison
{
    /// <summary>
    /// Below half a pixel, two layouts agree.
    ///
    /// Browsers lay out in subpixel units and report rects as doubles; we round to two decimals.
    /// Differences under half a pixel cannot move a glyph to a different pixel, so reporting them
    /// would fill every scenario with rows that mean nothing. Anything a reader could see is well
    /// above this.
    /// </summary>
    const float epsilon = 0.5f;

    public static BoxComparisonResult Compare(
        IReadOnlyList<BoxGeometry> reference,
        IReadOnlyList<BoxGeometry> actual)
    {
        var actualBySelector = new Dictionary<string, BoxGeometry>(StringComparer.Ordinal);
        foreach (var box in actual)
        {
            // Duplicate selectors would mean the path builder is not injective, which would make
            // every comparison meaningless. Keep the first and let the count mismatch below
            // surface it rather than silently pairing the wrong boxes.
            actualBySelector.TryAdd(box.Selector, box);
        }

        var diffs = new List<BoxDiff>();
        var missing = new List<string>();
        var matched = 0;

        foreach (var expected in reference)
        {
            if (!actualBySelector.Remove(expected.Selector, out var found))
            {
                missing.Add(expected.Selector);
                continue;
            }

            matched++;

            var dx = found.X - expected.X;
            var dy = found.Y - expected.Y;
            var dw = found.Width - expected.Width;
            var dh = found.Height - expected.Height;

            if (Math.Abs(dx) < epsilon &&
                Math.Abs(dy) < epsilon &&
                Math.Abs(dw) < epsilon &&
                Math.Abs(dh) < epsilon)
            {
                continue;
            }

            diffs.Add(new(expected.Selector, Round(dx), Round(dy), Round(dw), Round(dh)));
        }

        return new()
        {
            Matched = matched,
            // Reported in document order rather than by magnitude. Ordering by magnitude would
            // reshuffle the whole list whenever one number moved, turning a one-element fix into
            // an unreadable snapshot diff.
            Diffs = diffs,
            MissingFromRender = missing,
            // Whatever is left never appeared in the reference: a box we generate and the browser
            // does not.
            NotInReference = [.. actualBySelector.Keys.Order(StringComparer.Ordinal)],
            WorstOffset = diffs.Count == 0
                ? 0
                : diffs.Max(_ => Math.Max(Math.Abs(_.Dx), Math.Abs(_.Dy))),
            WorstSize = diffs.Count == 0
                ? 0
                : diffs.Max(_ => Math.Max(Math.Abs(_.Dw), Math.Abs(_.Dh)))
        };
    }

    static float Round(float value) =>
        MathF.Round(value, 2);
}

/// <summary>
/// How far our geometry is from the browser's, across one scenario.
/// </summary>
public class BoxComparisonResult
{
    /// <summary>Elements found in both trees.</summary>
    public int Matched { get; set; }

    /// <summary>
    /// The largest positional disagreement, in CSS pixels. The single number to watch while
    /// iterating: it goes to zero exactly when the layout is right.
    /// </summary>
    public float WorstOffset { get; set; }

    /// <summary>The largest size disagreement, in CSS pixels.</summary>
    public float WorstSize { get; set; }

    /// <summary>Elements the browser laid out that we did not generate a box for.</summary>
    public List<string> MissingFromRender { get; set; } = [];

    /// <summary>Boxes we generated that the browser has no element for.</summary>
    public List<string> NotInReference { get; set; } = [];

    /// <summary>Every element differing by at least half a pixel, in document order.</summary>
    public List<BoxDiff> Diffs { get; set; } = [];
}

/// <summary>
/// One element's disagreement, as our value minus the browser's. Positive <see cref="Dy"/> means
/// we placed it lower than the browser did.
/// </summary>
public record BoxDiff(string Selector, float Dx, float Dy, float Dw, float Dh);
