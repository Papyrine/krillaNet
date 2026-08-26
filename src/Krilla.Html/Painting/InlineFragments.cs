/// <summary>
/// The runs and edge boxes of one inline element on one line, grouped into the single rectangle a
/// browser rounds the corners of.
/// </summary>
/// <remarks>
/// <para>
/// An inline element's background is painted per RUN, which is invisible while the fills are square
/// — three abutting rectangles and one long one are the same picture — and stops being invisible
/// the moment a corner is rounded. A <c>&lt;span&gt;</c> holding a <c>&lt;b&gt;</c> is three runs,
/// and rounding each of them would put a notch at every element boundary inside a word.
/// </para>
/// <para>
/// So the grouping is the whole of what <c>border-radius</c> on an inline element needed. The
/// rectangle it produces spans from the element's opening edge, or its first run where it has no
/// surround, to its closing edge on that line, and is the same box the ordinary per-run fills
/// would have covered between them.
/// </para>
/// <para>
/// Built only for an element that actually declares a radius, so an ordinary paragraph pays one
/// walk of its runs and nothing else — and, more usefully, every scenario without one keeps
/// exactly the painting path it had.
/// </para>
/// </remarks>
sealed class InlineFragments
{
    readonly Dictionary<LineBox, List<InlineFragment>> lines = [];

    /// <summary>
    /// The fragments <paramref name="box"/>'s lines carry, or null when no inline element in it is
    /// rounded.
    /// </summary>
    public static InlineFragments? For(LayoutBox box)
    {
        // Every fragment built, keyed by element, in the order the lines were walked — which is
        // what says which of them carries the element's opening edge and which its closing one.
        // Neither can be known while a line is being read: an element that turns out to occupy one
        // line owns all four corners, and the same element wrapped owns two on each of two lines.
        Dictionary<object, List<Fragment>> byElement = [];
        List<(LineBox Line, List<Fragment> OnLine)> walked = [];

        foreach (var line in box.Lines)
        {
            Dictionary<object, Fragment> onLine = [];

            foreach (var run in line.Runs)
            {
                // A run with no advance fills nothing, so it neither extends a fragment nor is a
                // place one could be painted at.
                if (run.Width <= 0)
                {
                    continue;
                }

                foreach (var (identity, style, face, depth) in Owners(run))
                {
                    if (!style.HasRadius)
                    {
                        continue;
                    }

                    Extend(onLine, identity, style, face, depth, run.X, run.X + run.Width, run.Y, run);
                }
            }

            foreach (var edge in line.Edges)
            {
                foreach (var (identity, style, face, depth) in Owners(edge))
                {
                    if (!style.HasRadius)
                    {
                        continue;
                    }

                    Extend(
                        onLine,
                        identity,
                        style,
                        face,
                        depth,
                        edge.Bounds.X,
                        edge.Bounds.Right,
                        edge.Baseline,
                        run: null);
                }
            }

            if (onLine.Count == 0)
            {
                continue;
            }

            // Outermost first, so a rounded element nested inside another is painted over it rather
            // than under it. The depth is the run's own backdrop count, which is exactly the
            // nesting the painter already walks in.
            var ordered = onLine.Values.OrderBy(_ => _.Depth).ToList();
            walked.Add((line, ordered));

            foreach (var fragment in ordered)
            {
                if (!byElement.TryGetValue(fragment.Identity, out var all))
                {
                    all = [];
                    byElement[fragment.Identity] = all;
                }

                all.Add(fragment);
            }
        }

        if (walked.Count == 0)
        {
            return null;
        }

        var fragments = new InlineFragments();

        // An element's fragments laid end to end, which is the box a background gradient runs
        // across — measured: a span carrying a ramp and wrapping continues it where the first line
        // left off rather than restarting. Recorded here rather than asked of `InlineRamps`,
        // because that counts RUNS and a fragment reaches past its first and last run by whatever
        // padding and border the element carries.
        Dictionary<object, float> advance = [];

        foreach (var (line, ordered) in walked)
        {
            fragments.lines[line] =
            [
                ..ordered.Select(
                    fragment =>
                    {
                        var all = byElement[fragment.Identity];
                        var before = advance.GetValueOrDefault(fragment.Identity);
                        advance[fragment.Identity] = before + fragment.Right - fragment.Left;

                        return fragment.Build(
                            opening: ReferenceEquals(all[0], fragment),
                            closing: ReferenceEquals(all[^1], fragment),
                            before,
                            total: all.Sum(_ => _.Right - _.Left));
                    })
            ];
        }

        return fragments;
    }

    static void Extend(
        Dictionary<object, Fragment> onLine,
        object identity,
        ComputedStyle style,
        FontFace face,
        int depth,
        float left,
        float right,
        float baseline,
        TextRun? run)
    {
        if (onLine.TryGetValue(identity, out var fragment))
        {
            fragment.Left = MathF.Min(fragment.Left, left);
            fragment.Right = MathF.Max(fragment.Right, right);
            fragment.First ??= run;
            return;
        }

        // The baseline of the FIRST contributor, not the union of them all. An element's rectangle
        // is its own font box — a plain span holding a nested one is two pixels shorter at each end
        // than the union would be, which is measured — and the first contributor is the one sitting
        // on the element's own baseline in every arrangement but a vertically aligned descendant.
        onLine[identity] = new(identity, style, face, depth, left, right, baseline, run);
    }

    /// <summary>The rounded fragments on <paramref name="line"/>, outermost first.</summary>
    public IReadOnlyList<InlineFragment>? On(LineBox line) =>
        lines.GetValueOrDefault(line);

    /// <summary>
    /// The elements a run paints a background for, with the face and nesting depth each needs.
    /// </summary>
    /// <remarks>
    /// The same sequence <see cref="InlineRamps.Owners"/> yields, with two things it does not need:
    /// the face, because the rectangle is measured against the OWNER's font rather than the run's,
    /// and the depth, because a nested rounded element has to be painted after the one containing
    /// it.
    /// </remarks>
    static IEnumerable<(object Identity, ComputedStyle Style, FontFace Face, int Depth)> Owners(
        TextRun run)
    {
        var depth = 0;

        if (run.Backdrops is {} backdrops)
        {
            foreach (var backdrop in backdrops)
            {
                yield return (backdrop.Style, backdrop.Style, backdrop.Face, depth++);
            }
        }

        if (run.Selector is {} selector)
        {
            yield return (selector, run.Style, run.Face, depth);
        }
        else if (run.Generated)
        {
            yield return (run.Style, run.Style, run.Face, depth);
        }
    }

    /// <summary>The same, for an edge box, whose identity has to agree with its runs'.</summary>
    static IEnumerable<(object Identity, ComputedStyle Style, FontFace Face, int Depth)> Owners(
        InlineEdgeBox edge)
    {
        var depth = 0;

        if (edge.Ancestors is {} ancestors)
        {
            foreach (var ancestor in ancestors)
            {
                yield return (ancestor.Style, ancestor.Style, ancestor.Face, depth++);
            }
        }

        yield return ((object?) edge.Selector ?? edge.Style, edge.Style, edge.Face, depth);
    }

    /// <summary>One fragment while it is still being accumulated.</summary>
    sealed class Fragment(
        object identity,
        ComputedStyle style,
        FontFace face,
        int depth,
        float left,
        float right,
        float baseline,
        TextRun? first)
    {
        public object Identity { get; } = identity;

        public int Depth { get; } = depth;

        public float Left { get; set; } = left;

        public float Right { get; set; } = right;

        public TextRun? First { get; set; } = first;

        public InlineFragment Build(bool opening, bool closing, float before, float total)
        {
            var (top, bottom) = InlineMetrics.Extent(style, face, baseline);

            return new(
                Identity,
                style,
                new(X: Left, Y: top, Width: Right - Left, Height: bottom - top),
                opening,
                closing,
                First,
                before,
                total);
        }
    }
}

/// <summary>
/// One inline element's rectangle on one line, and which of its four corners it owns.
/// </summary>
/// <param name="Identity">The element, as the painter keys backgrounds by it.</param>
/// <param name="Style">Its style, for the radii, the colours and the border widths.</param>
/// <param name="Bounds">The border box of this fragment, unsnapped.</param>
/// <param name="Opening">
/// Whether this fragment carries the element's opening edge, and so its two left corners. A
/// fragment that does not is open at the break — a browser rounds neither corner where a line
/// ended, because the element did not end there.
/// </param>
/// <param name="Closing">Whether it carries the closing edge, and so the two right corners.</param>
/// <param name="First">
/// The first run inside it, or null for a fragment made of edge boxes alone. It decides WHEN the
/// fragment is painted: at the moment the per-run fill it replaces would first have happened, which
/// is what keeps a rounded element in the same place in the paint order as an unrounded one.
/// </param>
/// <param name="Before">How much of the element's advance stands before this fragment.</param>
/// <param name="Total">
/// The element's whole advance, its fragments laid end to end. With <paramref name="Before"/> it
/// gives the box a background gradient runs across, so a ramp continues over a break rather than
/// restarting on the next line.
/// </param>
readonly record struct InlineFragment(
    object Identity,
    ComputedStyle Style,
    Rect Bounds,
    bool Opening,
    bool Closing,
    TextRun? First,
    float Before,
    float Total);
