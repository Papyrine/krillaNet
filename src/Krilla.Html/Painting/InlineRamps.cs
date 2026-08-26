/// <summary>
/// Where each fragment of an inline element sits along that element's own advance, so a background
/// gradient can run ACROSS a wrapped element rather than restarting on every line.
/// </summary>
/// <remarks>
/// <para>
/// Measured: a span carrying <c>linear-gradient(to right, red, blue)</c> and wrapping onto a second
/// line continues the ramp where the first line left off. So the gradient's box is the element's
/// fragments laid end to end — 149px of first line and 278px of second make one 427px ramp — and
/// each fragment shows its own slice of it. Restarting per fragment is the obvious reading and puts
/// a full red-to-blue ramp on every line.
/// </para>
/// <para>
/// A pre-pass over the box's lines, because the offset a fragment needs is the sum of the fragments
/// BEFORE it and the painter reaches them one page at a time — a fragment on the second page of a
/// long paragraph still has to know what stood before it on the first, which a running total
/// accumulated while painting could not tell it.
/// </para>
/// <para>
/// Built only for a box that holds a gradient somewhere in its inline content, so an ordinary
/// paragraph pays one walk of its runs and nothing else.
/// </para>
/// </remarks>
sealed class InlineRamps
{
    readonly Dictionary<object, List<(float X, float Y, float Before)>> fragments = [];
    readonly Dictionary<object, float> totals = [];

    /// <summary>
    /// The ramps <paramref name="box"/>'s own lines need, or null when none of them do.
    /// </summary>
    public static InlineRamps? For(LayoutBox box)
    {
        InlineRamps? ramps = null;

        foreach (var line in box.Lines)
        {
            foreach (var run in line.Runs)
            {
                foreach (var (identity, style) in Owners(run))
                {
                    if (style.BackgroundImage is null)
                    {
                        continue;
                    }

                    ramps ??= new();
                    ramps.Add(identity, run);
                }
            }
        }

        return ramps;
    }

    /// <summary>
    /// The elements a run paints a background for: its inline ancestors, then its own.
    /// </summary>
    /// <remarks>
    /// The identity is the SELECTOR for a run's own element and the style INSTANCE for an
    /// ancestor's, which is what each one has: a backdrop carries no selector, and a style instance
    /// is shared by every run inside the element that resolved it.
    /// </remarks>
    public static IEnumerable<(object Identity, ComputedStyle Style)> Owners(TextRun run)
    {
        if (run.Backdrops is {} backdrops)
        {
            foreach (var backdrop in backdrops)
            {
                yield return (backdrop.Style, backdrop.Style);
            }
        }

        if (run.Selector is {} selector)
        {
            yield return (selector, run.Style);
        }
        else if (run.Generated)
        {
            yield return (run.Style, run.Style);
        }
    }

    void Add(object identity, TextRun run)
    {
        if (!fragments.TryGetValue(identity, out var list))
        {
            list = [];
            fragments[identity] = list;
            totals[identity] = 0;
        }

        list.Add((run.X, run.Y, totals[identity]));
        totals[identity] += run.Width;
    }

    /// <summary>
    /// The element's whole advance, and how much of it stands before the fragment at
    /// <paramref name="x"/>, <paramref name="y"/>.
    /// </summary>
    /// <remarks>
    /// Matched on the fragment's own origin, which is unique among an element's fragments — two of
    /// them are on different lines or at different positions on one. Null when the element has no
    /// gradient, which is every element in almost every document.
    /// </remarks>
    public (float Before, float Total)? Span(object identity, float x, float y)
    {
        if (!fragments.TryGetValue(identity, out var list))
        {
            return null;
        }

        foreach (var (fragmentX, fragmentY, before) in list)
        {
            if (fragmentX == x && fragmentY == y)
            {
                return (before, totals[identity]);
            }
        }

        return null;
    }
}
