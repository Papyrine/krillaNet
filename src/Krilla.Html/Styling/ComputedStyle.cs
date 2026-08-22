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
    /// An inline-level box whose contents are laid out as a block.
    /// </summary>
    /// <remarks>
    /// An atomic inline, like an image: it occupies one unbreakable box on a line rather than
    /// contributing runs to it, and a line can break before or after it but never inside. Unlike
    /// an image it has a box tree of its own, which is laid out in a formatting context of its own
    /// and reached through the line rather than through <see cref="LayoutBox.Children"/>.
    /// </remarks>
    InlineBlock,

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

/// <summary>How a box is positioned relative to normal flow.</summary>
enum PositionKind
{
    /// <summary>Positioned by flow alone. The offsets do not apply.</summary>
    Static,

    /// <summary>
    /// Laid out in flow, then painted offset. The space it occupied stays occupied.
    /// </summary>
    /// <remarks>
    /// The offset is applied after layout and affects nothing else — a sibling sits where it would
    /// have sat had the box never moved, and the parent keeps the height it would have had. That
    /// is the whole of what makes relative positioning cheap: it never invalidates a measurement.
    /// </remarks>
    Relative,

    /// <summary>
    /// Taken out of flow and positioned against the nearest positioned ancestor.
    /// </summary>
    Absolute,

    /// <summary>
    /// Taken out of flow and positioned against the viewport.
    /// </summary>
    /// <remarks>
    /// Laid out as <see cref="Absolute"/> against the page. What paged media should do with it —
    /// repeat it on every page, as the specification says, or place it once — is not settled here,
    /// so it reports through <see cref="HtmlOptions.OnDiagnostic"/>.
    /// </remarks>
    Fixed
}

/// <summary>Which side a box floats to, if any.</summary>
/// <remarks>
/// A float is taken out of normal flow: it does not advance its parent's flow position and does
/// not contribute to its parent's height. What it does do is shorten the LINE boxes beside it —
/// block boxes beside a float keep their full width and simply overlap it, which is what CSS
/// requires and what makes floats worth the trouble.
/// </remarks>
enum FloatKind
{
    /// <summary>In normal flow.</summary>
    None,

    /// <summary>Floated to the left edge of the containing block.</summary>
    Left,

    /// <summary>Floated to the right edge.</summary>
    Right
}

/// <summary>Which floats a box must be placed below.</summary>
enum ClearKind
{
    /// <summary>Placed wherever flow puts it.</summary>
    None,

    /// <summary>Below every left float in the formatting context.</summary>
    Left,

    /// <summary>Below every right float.</summary>
    Right,

    /// <summary>Below every float on either side.</summary>
    Both
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

/// <summary>What a declared <c>width</c> or <c>height</c> measures.</summary>
enum BoxSizingKind
{
    /// <summary>The content box: padding and border add to the declared size.</summary>
    ContentBox,

    /// <summary>The border box: padding and border come out of the declared size.</summary>
    BorderBox
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

    /// <summary>
    /// What <see cref="Width"/>, <see cref="Height"/> and the four min/max properties measure.
    /// </summary>
    /// <remarks>
    /// It applies to all six, which is the part worth stating: <c>max-width: 100px</c> under
    /// <c>border-box</c> caps the border box at 100px rather than the content box, so a box with
    /// padding clamps narrower than it otherwise would.
    /// </remarks>
    public BoxSizingKind BoxSizing { get; init; } = BoxSizingKind.ContentBox;

    /// <summary>Declared width, or <c>auto</c>. Measures <see cref="BoxSizing"/>.</summary>
    public CssLength Width { get; init; } = CssLength.Auto;

    /// <summary>Declared height, or <c>auto</c>. Measures <see cref="BoxSizing"/>.</summary>
    public CssLength Height { get; init; } = CssLength.Auto;

    /// <summary>Maximum width, or <c>none</c>. Measures <see cref="BoxSizing"/>.</summary>
    public CssLength MaxWidth { get; init; } = CssLength.None;

    /// <summary>Minimum width. Measures <see cref="BoxSizing"/>.</summary>
    public CssLength MinWidth { get; init; } = CssLength.Zero;

    /// <summary>Maximum height, or <c>none</c>. Measures <see cref="BoxSizing"/>.</summary>
    /// <remarks>
    /// Honoured only when it is an absolute length, for the reason <see cref="Height"/> is: a
    /// percentage resolves against a containing height that is indefinite throughout a paginated
    /// document, and CSS says such a percentage behaves as though it were not there.
    /// </remarks>
    public CssLength MaxHeight { get; init; } = CssLength.None;

    /// <summary>Minimum height. Measures <see cref="BoxSizing"/>.</summary>
    public CssLength MinHeight { get; init; } = CssLength.Zero;

    /// <summary>Background fill, or null when transparent.</summary>
    public Color? BackgroundColor { get; init; }

    /// <summary>Text colour.</summary>
    public Color Color { get; init; } = Color.Black;

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

    /// <summary>
    /// A unitless <c>line-height</c>, kept as the number rather than as a length.
    /// </summary>
    /// <remarks>
    /// The distinction is the whole of why inheriting <c>line-height</c> needs care. A number is
    /// inherited AS A NUMBER and re-resolved against each descendant's own font size, so
    /// <c>line-height: 1.5</c> on a container gives 24px to 16px text inside it and 48px to 32px
    /// text. A length is inherited as the length, so every descendant gets the same spacing
    /// whatever its font size. Storing only the resolved pixels would collapse the two and give
    /// the container's spacing to text of every size.
    /// </remarks>
    public float? LineHeightScale { get; init; }

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

    /// <summary>
    /// How far the FIRST line of a block container is indented from its start edge.
    /// </summary>
    /// <remarks>
    /// Inherited, and applied by the block that generates the line rather than by the one carrying
    /// the declaration — which is what makes <c>body { text-indent: 2em }</c> indent every
    /// paragraph rather than only the first. A negative value hangs the first line outside the
    /// content box, which is the other half of what the property is for.
    /// </remarks>
    public CssLength TextIndent { get; init; } = CssLength.Zero;

    /// <summary>How white space and wrapping are handled.</summary>
    public WhiteSpaceKind WhiteSpace { get; init; } = WhiteSpaceKind.Normal;

    /// <summary>How this box is positioned.</summary>
    public PositionKind Position { get; init; } = PositionKind.Static;

    /// <summary>Offset from the containing block's top edge.</summary>
    public CssLength Top { get; init; } = CssLength.Auto;

    /// <summary>Offset from the containing block's right edge.</summary>
    public CssLength Right { get; init; } = CssLength.Auto;

    /// <summary>Offset from the containing block's bottom edge.</summary>
    public CssLength Bottom { get; init; } = CssLength.Auto;

    /// <summary>Offset from the containing block's left edge.</summary>
    public CssLength Left { get; init; } = CssLength.Auto;

    /// <summary>Whether this box is taken out of flow by positioning.</summary>
    public bool IsAbsolute => Position is PositionKind.Absolute or PositionKind.Fixed;

    /// <summary>
    /// Whether this box is the containing block for absolutely positioned descendants.
    /// </summary>
    public bool IsPositioned => Position != PositionKind.Static;

    /// <summary>
    /// Whether this box is laid out as one unbreakable box on a line rather than in flow.
    /// </summary>
    public bool IsAtomicInline => Display == DisplayKind.InlineBlock;

    /// <summary>Which side this box floats to.</summary>
    public FloatKind Float { get; init; } = FloatKind.None;

    /// <summary>Which floats this box must clear.</summary>
    public ClearKind Clear { get; init; } = ClearKind.None;

    /// <summary>Whether this box is taken out of flow by a float.</summary>
    public bool IsFloating => Float != FloatKind.None;

    /// <summary>
    /// The content size a declared length asks for, given the padding and border on that axis.
    /// </summary>
    /// <remarks>
    /// The one place <c>box-sizing</c> is applied. Under <c>border-box</c> the padding and border
    /// come out of the declared length rather than being added to it, and the remainder floors at
    /// zero — a box declared narrower than its own padding is as narrow as its padding allows, not
    /// negative. Every site that turns a declared <c>width</c> or <c>height</c> into a used size
    /// goes through here, so that the property is honoured in one place rather than in eight.
    /// </remarks>
    /// <param name="declared">The declared length, already resolved to pixels.</param>
    /// <param name="surround">The padding and border on the same axis.</param>
    public float ContentSize(float declared, float surround) =>
        Math.Max(0, BoxSizing == BoxSizingKind.BorderBox ? declared - surround : declared);

    /// <summary>
    /// <see cref="ContentSize(float,float)"/> for a length that may be absent.
    /// </summary>
    public float? ContentSize(float? declared, float surround) =>
        declared is {} value ? ContentSize(value, surround) : null;

    /// <summary>
    /// The horizontal padding and border, with percentages resolved against
    /// <paramref name="containing"/>.
    /// </summary>
    /// <remarks>
    /// What <c>box-sizing: border-box</c> takes out of a declared width, and what a content width
    /// has to have added back to reach a border-box one.
    /// </remarks>
    public float SurroundX(float containing) =>
        PaddingLeft.Resolve(containing) + PaddingRight.Resolve(containing) + BorderWidthX;

    /// <summary>
    /// The vertical padding and border, with percentages resolved against
    /// <paramref name="containing"/> — which is the containing block's WIDTH, as CSS requires for
    /// a vertical percentage padding.
    /// </summary>
    public float SurroundY(float containing) =>
        PaddingTop.Resolve(containing) + PaddingBottom.Resolve(containing) + BorderWidthY;

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
        LineHeightScale is {} scale
            ? scale * FontSize
            : LineHeight ?? face.NormalLineHeight(FontSize);
}
