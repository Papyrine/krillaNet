namespace Krilla.Html.Styling;

/// <summary>
/// One of the sixteen boxes CSS Paged Media puts in a page's margins.
/// </summary>
/// <remarks>
/// The reason most documents have an <c>@page</c> rule at all: a running header, a footer, and a
/// page number. Each is named by an at-rule inside <c>@page</c> — <c>@top-center</c> and the rest —
/// and the name decides both which strip of the margin it sits in and where along that strip.
/// </remarks>
enum PageMarginSlot
{
    TopLeftCorner,
    TopLeft,
    TopCenter,
    TopRight,
    TopRightCorner,
    BottomLeftCorner,
    BottomLeft,
    BottomCenter,
    BottomRight,
    BottomRightCorner,
    LeftTop,
    LeftMiddle,
    LeftBottom,
    RightTop,
    RightMiddle,
    RightBottom
}

/// <summary>
/// Where each slot sits on the page, and how its content is aligned in it.
/// </summary>
/// <remarks>
/// <para>
/// The four corners are the rectangles where two margins meet, and take their size from both. The
/// top and bottom strips run between the corners; the left and right strips run between them
/// vertically.
/// </para>
/// <para>
/// CSS Paged Media §5.3 divides a strip between its three boxes by computing each one's
/// max-content and min-content widths and distributing what is left over. This gives each box the
/// whole strip and positions it by its own alignment instead. The two agree wherever one box in a
/// strip has content, which is nearly always — a header is a title, or a page number, or both at
/// opposite ends — and differ only when two long ones share a strip, where this lets them overlap
/// rather than wrapping them early. Chromium implements none of it, so there is no reference to
/// measure the difference against and no way for the corpus to see it: the approximation is a
/// deliberate one, recorded here rather than in a diagnostic nobody could act on.
/// </para>
/// </remarks>
static class PageMarginSlots
{
    /// <summary>The at-rule name each slot is written as.</summary>
    static readonly Dictionary<string, PageMarginSlot> names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["top-left-corner"] = PageMarginSlot.TopLeftCorner,
        ["top-left"] = PageMarginSlot.TopLeft,
        ["top-center"] = PageMarginSlot.TopCenter,
        ["top-centre"] = PageMarginSlot.TopCenter,
        ["top-right"] = PageMarginSlot.TopRight,
        ["top-right-corner"] = PageMarginSlot.TopRightCorner,
        ["bottom-left-corner"] = PageMarginSlot.BottomLeftCorner,
        ["bottom-left"] = PageMarginSlot.BottomLeft,
        ["bottom-center"] = PageMarginSlot.BottomCenter,
        ["bottom-centre"] = PageMarginSlot.BottomCenter,
        ["bottom-right"] = PageMarginSlot.BottomRight,
        ["bottom-right-corner"] = PageMarginSlot.BottomRightCorner,
        ["left-top"] = PageMarginSlot.LeftTop,
        ["left-middle"] = PageMarginSlot.LeftMiddle,
        ["left-bottom"] = PageMarginSlot.LeftBottom,
        ["right-top"] = PageMarginSlot.RightTop,
        ["right-middle"] = PageMarginSlot.RightMiddle,
        ["right-bottom"] = PageMarginSlot.RightBottom
    };

    /// <summary>
    /// The slot an at-rule name selects, or null when the name is not one.
    /// </summary>
    /// <remarks>
    /// <c>-centre</c> is accepted beside <c>-center</c>. Not CSS, and free: the name is never seen
    /// by anything but this table, and an author writing the other spelling gets a running header
    /// rather than silence.
    /// </remarks>
    public static PageMarginSlot? Parse(string name) =>
        names.TryGetValue(name.Trim(), out var slot) ? slot : null;

    /// <summary>
    /// The rectangle a slot occupies, in CSS pixels from the page's top-left corner.
    /// </summary>
    /// <param name="slot">Which of the sixteen.</param>
    /// <param name="options">The page geometry, whose margins are the strips these sit in.</param>
    public static Rect Area(PageMarginSlot slot, HtmlOptions options)
    {
        var left = options.MarginLeft;
        var right = options.MarginRight;
        var top = options.MarginTop;
        var bottom = options.MarginBottom;

        var width = options.PageWidth;
        var height = options.PageHeight;

        // The span each strip has between its corners, which is the content width for the
        // horizontal strips and the content height for the vertical ones.
        var across = Math.Max(0, width - left - right);
        var down = Math.Max(0, height - top - bottom);

        return slot switch
        {
            PageMarginSlot.TopLeftCorner => new(0, 0, left, top),
            PageMarginSlot.TopLeft or PageMarginSlot.TopCenter or PageMarginSlot.TopRight =>
                new(left, 0, across, top),
            PageMarginSlot.TopRightCorner => new(width - right, 0, right, top),

            PageMarginSlot.BottomLeftCorner => new(0, height - bottom, left, bottom),
            PageMarginSlot.BottomLeft or PageMarginSlot.BottomCenter or PageMarginSlot.BottomRight =>
                new(left, height - bottom, across, bottom),
            PageMarginSlot.BottomRightCorner => new(width - right, height - bottom, right, bottom),

            PageMarginSlot.LeftTop or PageMarginSlot.LeftMiddle or PageMarginSlot.LeftBottom =>
                new(0, top, left, down),

            _ => new(width - right, top, right, down)
        };
    }

    /// <summary>
    /// Where along its strip a slot's content sits horizontally, when the style declares no
    /// <c>text-align</c> of its own.
    /// </summary>
    /// <remarks>
    /// The name says it: <c>@top-left</c> is flush left and <c>@top-right</c> flush right, which is
    /// what makes "title on the left, page number on the right" two rules and no alignment
    /// declarations. A vertical strip's boxes are centred across their narrow width, and the
    /// corners are too.
    /// </remarks>
    public static TextAlignKind Align(PageMarginSlot slot) =>
        slot switch
        {
            PageMarginSlot.TopLeft or PageMarginSlot.BottomLeft => TextAlignKind.Left,
            PageMarginSlot.TopRight or PageMarginSlot.BottomRight => TextAlignKind.Right,
            _ => TextAlignKind.Center
        };

    /// <summary>
    /// Where in its strip a slot's box sits vertically, as a fraction of the room left over.
    /// </summary>
    /// <remarks>
    /// A horizontal strip centres its box, which is what a running header in a wide top margin
    /// wants. The vertical strips are the only place the three-way split is vertical, so those are
    /// what the fractions are really for.
    /// </remarks>
    public static float Vertical(PageMarginSlot slot) =>
        slot switch
        {
            PageMarginSlot.LeftTop or PageMarginSlot.RightTop => 0,
            PageMarginSlot.LeftBottom or PageMarginSlot.RightBottom => 1,
            _ => 0.5f
        };
}
