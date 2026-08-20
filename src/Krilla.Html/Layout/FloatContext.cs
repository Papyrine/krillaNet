namespace Krilla.Html.Layout;

/// <summary>
/// The floats placed in one block formatting context, and the queries layout makes of them.
/// </summary>
/// <remarks>
/// <para>
/// Every rectangle here is a <b>margin box</b>, in absolute document coordinates. Both choices are
/// load-bearing. Margin box, because that is what CSS excludes line boxes from, what a later float
/// is placed clear of, and what <c>clear</c> measures to — a float with
/// <c>margin-right: 20px</c> holds text 20px further away, and using the border box would sit the
/// text on top of the margin. Absolute coordinates, because a float placed in one block is queried
/// by lines in a later sibling, and the two share no local origin.
/// </para>
/// <para>
/// One context per block formatting context. A float and a table cell each establish their own, so
/// their contents neither see the outer floats nor leak into them; everything else shares its
/// parent's. Since nothing else here establishes one, in practice a document has a single context
/// rooted at the page.
/// </para>
/// <para>
/// The rules below were measured out of Chrome rather than read off the specification, because
/// several of them are stated loosely enough to admit more than one reading. Where a case pinned
/// one down, it is named.
/// </para>
/// </remarks>
sealed class FloatContext
{
    readonly List<(Rect Margin, FloatKind Side)> placed = [];

    /// <summary>
    /// The bottom edge of the lowest float, or <paramref name="fallback"/> when there are none.
    /// </summary>
    public float Bottom(float fallback)
    {
        var bottom = fallback;

        foreach (var (margin, _) in placed)
        {
            bottom = Math.Max(bottom, margin.Bottom);
        }

        return bottom;
    }

    /// <summary>
    /// Records a float whose margin box has already been positioned.
    /// </summary>
    public void Add(Rect margin, FloatKind side) =>
        placed.Add((margin, side));

    /// <summary>
    /// The horizontal band left free between <paramref name="left"/> and <paramref name="right"/>
    /// for a box spanning vertically from <paramref name="top"/> to <paramref name="bottom"/>.
    /// </summary>
    /// <remarks>
    /// A float counts when its margin box <b>vertically overlaps the span</b>, not merely when it
    /// contains the span's top. The difference is real and was measured: a line whose top clears a
    /// narrow float but whose body reaches into a wider one below is shortened by the wider one.
    /// Sampling at the top alone would have put that line 150px too far left.
    /// </remarks>
    public (float Left, float Right) Band(float top, float bottom, float left, float right)
    {
        foreach (var (margin, side) in placed)
        {
            if (margin.Y >= bottom || top >= margin.Bottom)
            {
                continue;
            }

            if (side == FloatKind.Left)
            {
                left = Math.Max(left, margin.Right);
            }
            else
            {
                right = Math.Min(right, margin.X);
            }
        }

        return (left, right);
    }

    /// <summary>
    /// Places a float of the given size no higher than <paramref name="top"/>, as far towards
    /// <paramref name="side"/> as it will go, and records it. Returns the margin box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Descends through the candidate positions in order — the requested top first, then the
    /// bottom edge of each float that could be in the way — and takes the first where the box
    /// fits. That is CSS 2.1 §9.5.1 rules 1 through 7 in one loop, and the descent is what makes a
    /// second float drop below the first rather than overlap it.
    /// </para>
    /// <para>
    /// A float too wide to fit anywhere is placed at the top candidate and allowed to overflow,
    /// rather than descending forever looking for room that does not exist. Chrome does the same:
    /// a 500px float in a 400px container sits at the top and hangs out of it.
    /// </para>
    /// </remarks>
    public Rect Place(
        FloatKind side,
        float top,
        float width,
        float height,
        float left,
        float right)
    {
        foreach (var candidate in Candidates(top))
        {
            var (bandLeft, bandRight) = Band(candidate, candidate + height, left, right);

            if (bandRight - bandLeft < width)
            {
                continue;
            }

            return Record(side, candidate, width, height, bandLeft, bandRight);
        }

        var (fallbackLeft, fallbackRight) = Band(top, top + height, left, right);
        return Record(side, top, width, height, fallbackLeft, fallbackRight);
    }

    Rect Record(FloatKind side, float y, float width, float height, float left, float right)
    {
        var x = side == FloatKind.Left ? left : right - width;
        var margin = new Rect(x, y, width, height);
        placed.Add((margin, side));
        return margin;
    }

    /// <summary>
    /// The vertical positions worth trying, in order: the requested top, then each float bottom
    /// below it.
    /// </summary>
    /// <remarks>
    /// Sorted and de-duplicated so the descent is monotonic. Without the sort a float placed
    /// earlier but sitting lower would send the search back upward, and the first fit found would
    /// not be the highest one.
    /// </remarks>
    IEnumerable<float> Candidates(float top)
    {
        var bottoms = placed
            .Select(_ => _.Margin.Bottom)
            .Where(_ => _ > top)
            .Distinct()
            .OrderBy(_ => _);

        return new[] {top}.Concat(bottoms);
    }

    /// <summary>
    /// The lowest of <paramref name="y"/> and the bottom edges of the floats
    /// <paramref name="clear"/> names.
    /// </summary>
    /// <remarks>
    /// Measured to the margin box, so a float with a bottom margin holds the cleared box that much
    /// further down. Applies to floats as much as to blocks: a float carrying <c>clear</c> starts
    /// below the ones it clears before being pushed sideways.
    /// </remarks>
    public float ClearTo(ClearKind clear, float y)
    {
        if (clear == ClearKind.None)
        {
            return y;
        }

        foreach (var (margin, side) in placed)
        {
            var applies = clear switch
            {
                ClearKind.Left => side == FloatKind.Left,
                ClearKind.Right => side == FloatKind.Right,
                _ => true
            };

            if (applies)
            {
                y = Math.Max(y, margin.Bottom);
            }
        }

        return y;
    }

    /// <summary>
    /// The first float bottom strictly below <paramref name="y"/>, or null when none remains.
    /// </summary>
    /// <remarks>
    /// What a line box descends by when the band beside it has closed up entirely. CSS 2.1 §9.5
    /// shifts such a line down "until either it fits or there are no more floats present", and this
    /// enumerates the places worth trying.
    /// </remarks>
    public float? NextBottomBelow(float y)
    {
        float? next = null;

        foreach (var (margin, _) in placed)
        {
            if (margin.Bottom > y && (next is null || margin.Bottom < next))
            {
                next = margin.Bottom;
            }
        }

        return next;
    }
}
