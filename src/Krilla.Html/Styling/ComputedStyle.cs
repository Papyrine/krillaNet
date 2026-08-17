namespace Krilla.Html.Styling;

/// <summary>How a box participates in layout.</summary>
enum DisplayKind
{
    /// <summary>Generates no box at all.</summary>
    None,

    /// <summary>A block-level box, stacked vertically in its parent.</summary>
    Block,

    /// <summary>An inline-level box, flowed into a line.</summary>
    Inline
}

/// <summary>How lines are aligned within their containing block.</summary>
enum TextAlignKind
{
    /// <summary>Aligned to the start edge.</summary>
    Left,

    /// <summary>Centred.</summary>
    Center,

    /// <summary>Aligned to the end edge.</summary>
    Right,

    /// <summary>Stretched to both edges, except on the last line of a block.</summary>
    Justify
}

/// <summary>How white space and line breaking are handled.</summary>
enum WhiteSpaceKind
{
    /// <summary>Collapse runs of white space, wrap at soft break opportunities.</summary>
    Normal,

    /// <summary>Preserve white space and newlines, never wrap.</summary>
    Pre,

    /// <summary>Preserve white space and newlines, and wrap.</summary>
    PreWrap,

    /// <summary>Collapse white space, honour newlines, and wrap.</summary>
    PreLine,

    /// <summary>Collapse white space, never wrap.</summary>
    NoWrap
}

/// <summary>How a border edge is drawn.</summary>
enum BorderStyleKind
{
    /// <summary>Not drawn, and zero width regardless of the declared width.</summary>
    None,

    /// <summary>A solid line.</summary>
    Solid
}

/// <summary>
/// The style properties layout and painting actually read, resolved into usable values.
/// </summary>
/// <remarks>
/// <para>
/// A deliberate narrowing of the CSS cascade, not a mirror of it. AngleSharp.Css computes hundreds
/// of properties; this holds the subset the current engine honours, so that what is and is not
/// implemented is legible from one type rather than distributed across the layout code. A property
/// absent here is a property the renderer ignores.
/// </para>
/// <para>
/// Lengths are in CSS pixels, resolved as far as they can be without layout — percentages survive
/// as percentages because their basis is a layout result. See <see cref="CssLength"/>.
/// </para>
/// </remarks>
sealed class ComputedStyle
{
    /// <summary>How the box participates in layout.</summary>
    public DisplayKind Display { get; init; } = DisplayKind.Block;

    /// <summary>Top margin.</summary>
    public CssLength MarginTop { get; init; } = CssLength.Zero;

    /// <summary>Right margin.</summary>
    public CssLength MarginRight { get; init; } = CssLength.Zero;

    /// <summary>Bottom margin.</summary>
    public CssLength MarginBottom { get; init; } = CssLength.Zero;

    /// <summary>Left margin.</summary>
    public CssLength MarginLeft { get; init; } = CssLength.Zero;

    /// <summary>Top padding.</summary>
    public CssLength PaddingTop { get; init; } = CssLength.Zero;

    /// <summary>Right padding.</summary>
    public CssLength PaddingRight { get; init; } = CssLength.Zero;

    /// <summary>Bottom padding.</summary>
    public CssLength PaddingBottom { get; init; } = CssLength.Zero;

    /// <summary>Left padding.</summary>
    public CssLength PaddingLeft { get; init; } = CssLength.Zero;

    /// <summary>Top border width, already zeroed when the style is <c>none</c>.</summary>
    public float BorderTop { get; init; }

    /// <summary>Right border width.</summary>
    public float BorderRight { get; init; }

    /// <summary>Bottom border width.</summary>
    public float BorderBottom { get; init; }

    /// <summary>Left border width.</summary>
    public float BorderLeft { get; init; }

    /// <summary>Top border colour.</summary>
    public Color? BorderTopColor { get; init; }

    /// <summary>Right border colour.</summary>
    public Color? BorderRightColor { get; init; }

    /// <summary>Bottom border colour.</summary>
    public Color? BorderBottomColor { get; init; }

    /// <summary>Left border colour.</summary>
    public Color? BorderLeftColor { get; init; }

    /// <summary>Content width, or <c>auto</c>.</summary>
    public CssLength Width { get; init; } = CssLength.Auto;

    /// <summary>Content height, or <c>auto</c>.</summary>
    public CssLength Height { get; init; } = CssLength.Auto;

    /// <summary>Maximum content width, or <c>none</c>.</summary>
    public CssLength MaxWidth { get; init; } = CssLength.None;

    /// <summary>Minimum content width.</summary>
    public CssLength MinWidth { get; init; } = CssLength.Zero;

    /// <summary>Background fill, or null when transparent.</summary>
    public Color? BackgroundColor { get; init; }

    /// <summary>Text colour.</summary>
    public Color Color { get; init; } = Krilla.Color.Black;

    /// <summary><c>font-family</c>, in preference order.</summary>
    public IReadOnlyList<string> FontFamilies { get; init; } = [];

    /// <summary>Font size in CSS pixels.</summary>
    public float FontSize { get; init; } = 16;

    /// <summary>Font weight, 1-1000.</summary>
    public int FontWeight { get; init; } = 400;

    /// <summary>Whether the font is italic or oblique.</summary>
    public bool Italic { get; init; }

    /// <summary>
    /// Line height in CSS pixels, or null for <c>normal</c> — which depends on the font and so is
    /// resolved once a face has been chosen.
    /// </summary>
    public float? LineHeight { get; init; }

    /// <summary>How lines are aligned.</summary>
    public TextAlignKind TextAlign { get; init; } = TextAlignKind.Left;

    /// <summary>How white space and wrapping are handled.</summary>
    public WhiteSpaceKind WhiteSpace { get; init; } = WhiteSpaceKind.Normal;

    /// <summary>Sum of the left and right border widths.</summary>
    public float BorderWidthX => BorderLeft + BorderRight;

    /// <summary>Sum of the top and bottom border widths.</summary>
    public float BorderWidthY => BorderTop + BorderBottom;

    /// <summary>Whether any border edge would actually paint.</summary>
    public bool HasBorder =>
        (BorderTop > 0 && BorderTopColor is not null) ||
        (BorderRight > 0 && BorderRightColor is not null) ||
        (BorderBottom > 0 && BorderBottomColor is not null) ||
        (BorderLeft > 0 && BorderLeftColor is not null);

    /// <summary>Whether this style preserves white space rather than collapsing it.</summary>
    public bool PreservesSpaces =>
        WhiteSpace is WhiteSpaceKind.Pre or WhiteSpaceKind.PreWrap;

    /// <summary>Whether lines may break at soft wrap opportunities.</summary>
    public bool Wraps =>
        WhiteSpace is WhiteSpaceKind.Normal or WhiteSpaceKind.PreWrap or WhiteSpaceKind.PreLine;

    /// <summary>Whether newlines in the source force a line break.</summary>
    public bool PreservesNewlines =>
        WhiteSpace is WhiteSpaceKind.Pre or WhiteSpaceKind.PreWrap or WhiteSpaceKind.PreLine;

    /// <summary>
    /// The line height to use with <paramref name="face"/>, resolving <c>normal</c> against the
    /// face's own metrics.
    /// </summary>
    public float ResolveLineHeight(FontFace face) =>
        LineHeight ?? face.NormalLineHeight(FontSize);
}
