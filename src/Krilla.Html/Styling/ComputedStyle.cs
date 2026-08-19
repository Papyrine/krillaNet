namespace Krilla.Html.Styling;

/// <summary>How a box participates in layout.</summary>
enum DisplayKind
{
    /// <summary>Generates no box at all.</summary>
    None,

    /// <summary>A block-level box, stacked vertically in its parent.</summary>
    Block,

    /// <summary>An inline-level box, flowed into a line.</summary>
    Inline,

    /// <summary>
    /// A block-level box that also generates a list marker.
    /// </summary>
    /// <remarks>
    /// Lays out exactly as <see cref="Block"/> does. The marker is not a box in the tree: it sits
    /// outside the principal box, so it neither affects the geometry of anything nor appears in
    /// the browser's <c>getBoundingClientRect()</c> for the element.
    /// </remarks>
    ListItem,

    /// <summary>
    /// A table wrapper, which lays out its descendants itself rather than as blocks.
    /// </summary>
    /// <remarks>
    /// The one display value that changes how DESCENDANTS are laid out rather than only the box
    /// carrying it. Everything from here down to <see cref="TableCell"/> is positioned by
    /// <see cref="TableLayout"/> as one unit, because a column's width is a property of the whole
    /// table and no single cell can know it.
    /// </remarks>
    Table,

    /// <summary>A table's caption, laid out above the grid.</summary>
    TableCaption,

    /// <summary>A <c>thead</c>: rows that come first however the source ordered them.</summary>
    TableHeaderGroup,

    /// <summary>A <c>tbody</c>.</summary>
    TableRowGroup,

    /// <summary>A <c>tfoot</c>: rows that come last however the source ordered them.</summary>
    TableFooterGroup,

    /// <summary>A row of cells.</summary>
    TableRow,

    /// <summary>A cell, which is a block container sized by its column and row.</summary>
    TableCell,

    /// <summary>
    /// A <c>col</c> or <c>colgroup</c>. Generates no box and contributes no content.
    /// </summary>
    /// <remarks>
    /// Present so the box builder can drop them deliberately rather than laying their (empty)
    /// content out as blocks. Their <c>width</c> contribution to column sizing is not read yet.
    /// </remarks>
    TableColumn
}

/// <summary>How a table's column widths are decided.</summary>
enum TableLayoutKind
{
    /// <summary>From the content: columns size to what the cells contain.</summary>
    Auto,

    /// <summary>
    /// From the first row and the specified widths alone, ignoring content.
    /// </summary>
    Fixed,
}

/// <summary>How a cell's content sits within a row taller than it.</summary>
enum VerticalAlignKind
{
    /// <summary>Against the baseline of the row's first line.</summary>
    Baseline,

    /// <summary>Against the top of the row.</summary>
    Top,

    /// <summary>Centred in the row.</summary>
    Middle,

    /// <summary>Against the bottom of the row.</summary>
    Bottom
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

/// <summary>What a list item's marker shows.</summary>
/// <remarks>
/// The subset of <c>list-style-type</c> that is drawn. A value outside it falls back to
/// <see cref="Disc"/> rather than to nothing, so an exotic counter style still marks its items.
/// </remarks>
enum ListStyleKind
{
    /// <summary>No marker at all.</summary>
    None,

    /// <summary>A filled circle.</summary>
    Disc,

    /// <summary>A hollow circle.</summary>
    Circle,

    /// <summary>A filled square.</summary>
    Square,

    /// <summary>1, 2, 3.</summary>
    Decimal,

    /// <summary>01, 02, 03 — padded to two digits.</summary>
    DecimalLeadingZero,

    /// <summary>a, b, c.</summary>
    LowerAlpha,

    /// <summary>A, B, C.</summary>
    UpperAlpha,

    /// <summary>i, ii, iii.</summary>
    LowerRoman,

    /// <summary>I, II, III.</summary>
    UpperRoman
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
/// <para>
/// A record for the <c>with</c> expression: an <c>&lt;img&gt;</c>'s <c>width</c> and
/// <c>height</c> content attributes are presentational hints that AngleSharp does not surface as
/// declarations, so they are layered on after the cascade has run rather than during it.
/// </para>
/// </remarks>
sealed record ComputedStyle
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

    /// <summary>Whether text is underlined.</summary>
    /// <remarks>
    /// Treated as inherited, which <c>text-decoration</c> strictly is not — CSS says a decoration
    /// is drawn ACROSS descendants by the element that declared it, rather than being inherited by
    /// them. The distinction shows only where a descendant sets its own colour, since a propagated
    /// decoration keeps the colour of the element that declared it while an inherited one does not.
    /// Inheriting is the far simpler model and agrees with propagation everywhere else.
    /// </remarks>
    public bool Underline { get; init; }

    /// <summary>What marker a list item shows.</summary>
    public ListStyleKind ListStyle { get; init; } = ListStyleKind.Disc;

    /// <summary>Horizontal gap between cells, in CSS pixels.</summary>
    /// <remarks>
    /// Inherited, which is not a quirk: <c>border-spacing</c> is set on the table and read by the
    /// layout of its cells, so the value has to travel down the tree to reach where it is used.
    /// </remarks>
    public float BorderSpacingX { get; init; }

    /// <summary>Vertical gap between rows, in CSS pixels.</summary>
    public float BorderSpacingY { get; init; }

    /// <summary>How this table's column widths are decided.</summary>
    public TableLayoutKind TableLayout { get; init; } = TableLayoutKind.Auto;

    /// <summary>How a cell's content sits within a taller row.</summary>
    public VerticalAlignKind VerticalAlign { get; init; } = VerticalAlignKind.Baseline;

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

    /// <summary>Whether this box is laid out by the table algorithm rather than as a block.</summary>
    public bool IsTablePart =>
        Display is DisplayKind.Table or DisplayKind.TableCaption or DisplayKind.TableHeaderGroup or
            DisplayKind.TableRowGroup or DisplayKind.TableFooterGroup or DisplayKind.TableRow or
            DisplayKind.TableCell;

    /// <summary>Whether this box holds rows rather than content.</summary>
    public bool IsRowGroup =>
        Display is DisplayKind.TableHeaderGroup or DisplayKind.TableRowGroup or
            DisplayKind.TableFooterGroup;

    /// <summary>Whether this marker is a shape rather than a counter.</summary>
    public bool HasSymbolMarker =>
        ListStyle is ListStyleKind.Disc or ListStyleKind.Circle or ListStyleKind.Square;

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
