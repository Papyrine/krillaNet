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

/// <summary>
/// Where a box sits vertically: within a taller table row, or on the line it is part of.
/// </summary>
/// <remarks>
/// One enum for both, because it is one property. The two uses read different subsets: a table
/// cell honours <c>Top</c>, <c>Middle</c> and <c>Bottom</c> and treats the rest as top, while an
/// inline-level box honours every value.
/// </remarks>
enum VerticalAlignKind
{
    /// <summary>Against the baseline of the row's first line, or on the line's baseline.</summary>
    Baseline,

    /// <summary>Against the top of the row, or of the line box.</summary>
    Top,

    /// <summary>Centred in the row, or half an x-height above the line's baseline.</summary>
    Middle,

    /// <summary>Against the bottom of the row, or of the line box.</summary>
    Bottom,

    /// <summary>Raised above the baseline by the superscript offset.</summary>
    Super,

    /// <summary>Lowered below the baseline by the subscript offset.</summary>
    Sub,

    /// <summary>Top edge against the top of the parent's text.</summary>
    TextTop,

    /// <summary>Bottom edge against the bottom of the parent's text.</summary>
    TextBottom,

    /// <summary>
    /// Raised off the baseline by <see cref="ComputedStyle.VerticalAlignOffset"/>.
    /// </summary>
    /// <remarks>
    /// The one value carrying a number of its own, which is why the offset is a separate property
    /// rather than folded into this. Positive raises, so a negative length lowers.
    /// </remarks>
    Length
}

/// <summary>What a box asks of the page breaks around and inside it.</summary>
/// <remarks>
/// One enum for all three of <c>break-before</c>, <c>break-after</c> and <c>break-inside</c>,
/// even though no single property takes every value: <c>break-inside</c> has no <c>always</c> and
/// the other two are the only ones honouring it. Three near-identical enums would buy nothing
/// beyond making an unreachable combination unrepresentable, and the reporter has to recognise the
/// unhonoured values anyway.
/// </remarks>
enum BreakKind
{
    /// <summary>No constraint: the page falls where pagination puts it.</summary>
    Auto,

    /// <summary>A page must start here.</summary>
    /// <remarks>
    /// <c>always</c> and <c>page</c>, which ask for a page and say nothing about which sheet it
    /// lands on.
    /// </remarks>
    Always,

    /// <summary>A page must start here, and be a RIGHT-hand sheet.</summary>
    /// <remarks>
    /// <c>right</c> and <c>recto</c>. A right-hand page is an odd-numbered one, counting the first
    /// page of the document as page one — so honouring it means inserting a blank page whenever the
    /// break would otherwise land on an even one. That blank page is the whole of the difference
    /// from <see cref="Always"/>, and it is a page COUNT difference rather than a cosmetic one.
    /// </remarks>
    Recto,

    /// <summary>A page must start here, and be a LEFT-hand sheet.</summary>
    /// <remarks><c>left</c> and <c>verso</c>: an even-numbered page.</remarks>
    Verso,

    /// <summary>A page should not start here, which is not honoured.</summary>
    /// <remarks>
    /// Honoured for <c>break-inside</c>, where it makes the box one unbreakable unit. Not for
    /// <c>break-before</c> and <c>break-after</c>, which ask for a break to be moved somewhere
    /// earlier rather than for one to be taken — reported instead.
    /// </remarks>
    Avoid
}

/// <summary>Whether a break value asks for a page at all.</summary>
static class BreakKinds
{
    /// <summary>
    /// Whether the value forces a page, whichever sheet it asks to land on.
    /// </summary>
    /// <remarks>
    /// Written as one predicate because three enumeration members now mean "a page starts here",
    /// and a site that tested for <c>Always</c> alone would silently stop honouring the two that
    /// name a side — the shape this defect took when the sided values were folded into
    /// <c>Always</c>.
    /// </remarks>
    public static bool Forces(this BreakKind kind) =>
        kind is BreakKind.Always or BreakKind.Recto or BreakKind.Verso;
}

/// <summary>The lines <c>text-decoration</c> draws through a run of text.</summary>
/// <remarks>
/// Flags rather than one value, because the property takes any combination of them and browsers
/// draw every one asked for — <c>underline overline line-through</c> puts three rules on the same
/// words.
/// </remarks>
[Flags]
enum TextDecorations
{
    /// <summary>No rule.</summary>
    None = 0,

    /// <summary>A rule below the baseline.</summary>
    Underline = 1,

    /// <summary>A rule above the text.</summary>
    Overline = 2,

    /// <summary>A rule through the middle of the text.</summary>
    LineThrough = 4
}

/// <summary>Whether a box and its descendants are painted.</summary>
/// <remarks>
/// Inherited, and about PAINTING alone — a hidden box is laid out, occupies its space and holds
/// its siblings apart exactly as a visible one does. That is the half of the property a naive
/// implementation loses by treating it like <c>display: none</c>.
/// </remarks>
enum VisibilityKind
{
    /// <summary>Painted.</summary>
    Visible,

    /// <summary>Not painted, and still occupying its space.</summary>
    Hidden,

    /// <summary>
    /// <c>visibility: collapse</c>, which this engine treats exactly as <see cref="Hidden"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A value of its own rather than folded into <see cref="Hidden"/> only so the diagnostic can
    /// name it. On a table row or column CSS says the track is REMOVED and the rows below move up,
    /// which is the one visibility value that changes layout rather than only painting — and it is
    /// NOT implemented, deliberately.
    /// </para>
    /// <para>
    /// Not because it is hard. It was written, and then measured, and Chrome turned out to disagree
    /// with ITSELF: its screen layout gives the collapsed row a height of zero and starts the next
    /// row one border-spacing after the one above, while its printed page puts everything after the
    /// row twenty pixels further down again. The corpus reads geometry from the first and pixels
    /// from the second, so a scenario for this cannot be exact on both measurements however the
    /// engine behaves. Treating it as <c>hidden</c> is what a reader of the reported diagnostic can
    /// at least predict.
    /// </para>
    /// </remarks>
    Collapse
}

/// <summary>How text is cased before it is shaped.</summary>
enum TextTransformKind
{
    /// <summary>Drawn as written.</summary>
    None,

    /// <summary>Every character upper-cased.</summary>
    Uppercase,

    /// <summary>Every character lower-cased.</summary>
    Lowercase,

    /// <summary>The first letter of each word upper-cased.</summary>
    Capitalize
}

/// <summary>Whether a box clips what overflows it.</summary>
enum OverflowKind
{
    /// <summary>Overflowing content is painted outside the box.</summary>
    Visible,

    /// <summary>Overflowing content is clipped to the padding box.</summary>
    Hidden
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

/// <summary>When a line may break INSIDE a word rather than only between words.</summary>
/// <remarks>
/// Two CSS properties collapse into this, because the engine treats their values by what they
/// permit rather than by which property asked. <c>word-break: break-all</c> and
/// <c>overflow-wrap: anywhere</c> both mean <see cref="Always"/>; <c>overflow-wrap: break-word</c>
/// means <see cref="OnOverflow"/>.
/// </remarks>
enum WordBreaking
{
    /// <summary>Only at the ordinary opportunities: spaces, dashes, atomic inlines.</summary>
    Normal,

    /// <summary>Anywhere, but only for a word too wide to fit on a line of its own.</summary>
    OnOverflow,

    /// <summary>Anywhere, whether or not the word would overflow.</summary>
    Always
}

/// <summary>
/// One shadow: an offset and a colour, with neither blur nor spread.
/// </summary>
/// <remarks>
/// <para>
/// Neither, and for two different reasons. A BLUR needs a Gaussian, which a PDF content stream does
/// not express for an arbitrary shape — and a blurred shadow drawn sharp is a hard dark copy where a
/// soft halo belongs, the same reasoning that makes an unsupported outline style draw nothing rather
/// than draw solid.
/// </para>
/// <para>
/// A SPREAD is unreachable rather than unimplementable. AngleSharp ELIDES A ZERO BLUR when it
/// serialises the value, so <c>6px 6px 0 4px</c> — offset, no blur, spread four — comes back as
/// <c>6px 6px 4px</c>, which is byte-for-byte what a real four-pixel blur comes back as. The
/// distinction is destroyed before the engine sees it, so a three-length value has to be read as a
/// blur: reading it as a spread would draw a hard shadow wherever an author asked for a soft one,
/// which is the worse of the two mistakes by far.
/// </para>
/// </remarks>
/// <param name="OffsetX">How far right the shadow is moved.</param>
/// <param name="OffsetY">How far down.</param>
/// <param name="Color">Its colour.</param>
/// <param name="Alpha">
/// Its opacity, from the colour's fourth component. A shadow is written with one far more often than
/// not — <c>rgba(0, 0, 0, 0.25)</c> is what a shadow IS — so ignoring it would leave the feature
/// drawing a solid black slab in the usual case.
/// </param>
readonly record struct BoxShadow(float OffsetX, float OffsetY, Color Color, float Alpha = 1f);

/// <summary>One of the three nested rectangles a box is made of.</summary>
enum BoxArea
{
    /// <summary>Out to the outside of the border.</summary>
    Border,

    /// <summary>Inside the border, including the padding.</summary>
    Padding,

    /// <summary>Inside the padding.</summary>
    Content
}

/// <summary>How a background image is scaled before it is tiled.</summary>
enum BackgroundSizing
{
    /// <summary>Its own pixel dimensions.</summary>
    Auto,

    /// <summary>Scaled to cover the positioning area, keeping its proportions, and clipped.</summary>
    Cover,

    /// <summary>Scaled to fit inside it, keeping its proportions.</summary>
    Contain,

    /// <summary>Scaled to the declared lengths.</summary>
    Explicit
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

/// <summary>Whether a table's cells share their borders.</summary>
enum BorderCollapseKind
{
    /// <summary>Each cell draws its own border, separated by <c>border-spacing</c>.</summary>
    Separate,

    /// <summary>Adjacent cells share one line, and there is no spacing.</summary>
    Collapse
}

/// <summary>Which side of a table its caption sits on.</summary>
enum CaptionSideKind
{
    /// <summary>Above the grid.</summary>
    Top,

    /// <summary>Below it.</summary>
    Bottom
}

/// <summary>Where a list item's marker sits.</summary>
enum ListStylePositionKind
{
    /// <summary>Outside the item's border edge, hanging in the padding of the list.</summary>
    Outside,

    /// <summary>On the item's first line, as its first content.</summary>
    Inside
}

/// <summary>How a replaced element's content fills the box it was given.</summary>
enum ObjectFitKind
{
    /// <summary>Stretched to the box, ignoring its own proportions.</summary>
    Fill,

    /// <summary>Scaled to fit inside the box, keeping its proportions.</summary>
    Contain,

    /// <summary>Scaled to cover the box, keeping its proportions, and clipped.</summary>
    Cover,

    /// <summary>Drawn at its intrinsic size, centred and clipped.</summary>
    None,

    /// <summary>The smaller of <see cref="None"/> and <see cref="Contain"/>.</summary>
    ScaleDown
}

/// <summary>How a border edge is drawn.</summary>
/// <remarks>
/// <c>none</c> is absent: <see cref="StyleResolver"/> folds it into a zero width, so an edge that
/// is not drawn is one that takes no space either and layout never has to consult a style.
/// <c>hidden</c> does the same to its width and is kept as a value all the same, because a
/// collapsed table has to be able to tell it from an absent border. Everything else CSS lists is
/// here, so a value not in this enum is a value nobody wrote.
/// </remarks>
enum BorderStyleKind
{
    /// <summary>One unbroken band.</summary>
    Solid,

    /// <summary>Dashes twice the border's width, separated by gaps its width.</summary>
    Dashed,

    /// <summary>Round dots the border's width, spaced at twice it.</summary>
    Dotted,

    /// <summary>Two bands a third of the width each, with a third-width gap between them.</summary>
    Double,

    /// <summary>
    /// Bevelled so the box looks pressed IN: the top and left edges darkened, the bottom and right
    /// lightened.
    /// </summary>
    Inset,

    /// <summary>
    /// <see cref="Inset"/> reflected — the top and left lightened, the bottom and right darkened —
    /// so the box looks raised.
    /// </summary>
    Outset,

    /// <summary>
    /// A groove carved into the canvas: the outer half of each edge drawn as <see cref="Inset"/>
    /// and the inner half as <see cref="Outset"/>.
    /// </summary>
    Groove,

    /// <summary>
    /// A ridge standing out of it, which is <see cref="Groove"/> with the two halves exchanged.
    /// </summary>
    Ridge,

    /// <summary>
    /// <c>hidden</c>: no ink and no space, and inside a COLLAPSED table it suppresses the
    /// neighbouring border too.
    /// </summary>
    /// <remarks>
    /// A style of its own rather than folded into a zero width, which it was until the collapsing
    /// model needed to tell the two apart. CSS gives <c>hidden</c> absolute priority at a shared
    /// edge — it beats even a wider border — and that is unexpressible if it arrives
    /// indistinguishable from an absent one, which is how it was reported as unimplementable.
    /// Everywhere but a collapsed table it behaves exactly as <c>none</c> does, which is why the
    /// width is still folded to zero.
    /// </remarks>
    Hidden
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

    /// <summary>Width of the outline, in CSS pixels, or zero when there is none.</summary>
    /// <remarks>
    /// An outline takes no space. It is drawn outside the border edge and moves nothing, which is
    /// what makes it the usual choice for a focus ring — a border there would shift the page every
    /// time focus moved.
    /// </remarks>
    public float OutlineWidth { get; init; }

    /// <summary>Colour of the outline, or null when it is not painted.</summary>
    public Color? OutlineColor { get; init; }

    /// <summary>How far outside the border edge the outline sits.</summary>
    public float OutlineOffset { get; init; }

    /// <summary>Whether this table's cells share their borders.</summary>
    /// <remarks>
    /// Inherited, which CSS specifies and which matters here: it is declared on the table and read
    /// on the cells, both by <see cref="CollapsedBorders"/> and by the diagnostic that reports a
    /// <c>hidden</c> border a collapsed table cannot honour.
    /// </remarks>
    public BorderCollapseKind BorderCollapse { get; init; }

    /// <summary>
    /// How far <see cref="VerticalAlignKind.Length"/> raises the box off the baseline.
    /// </summary>
    /// <remarks>
    /// A percentage resolves against the element's OWN <c>line-height</c> rather than its font
    /// size, which is CSS's rule and measures out of Chrome exactly: at a line height of 24px,
    /// <c>25%</c> lands a box in the same place <c>6px</c> does. That basis is a layout-time
    /// quantity when the line height is <c>normal</c>, so the value is carried unresolved and
    /// settled where the font is known.
    /// </remarks>
    public CssLength VerticalAlignOffset { get; init; }

    /// <summary>
    /// The colour every text decoration is drawn in, or null to take the text's own.
    /// </summary>
    /// <remarks>
    /// Inherited alongside <see cref="Decorations"/>, since a decoration declared on an ancestor
    /// is drawn through its descendants and carries the ancestor's colour with it. Null rather than
    /// the resolved colour so that "nobody said" stays distinguishable from "said black" — the
    /// first takes each run's own colour and the second does not.
    /// </remarks>
    public Color? DecorationColor { get; init; }

    /// <summary>How a text decoration's rule is drawn.</summary>
    /// <remarks>
    /// Reuses the border enumeration because the four values that matter here are four of its own,
    /// and every one of them means the same thing. <c>wavy</c> has no border counterpart and is
    /// reported rather than approximated.
    /// </remarks>
    public BorderStyleKind DecorationStyle { get; init; }

    /// <summary>
    /// How far apart the tab stops are, as a multiple of the space advance.
    /// </summary>
    /// <remarks>
    /// A count of space advances, which is what the property's initial value of 8 means and what
    /// nearly every author writes. A LENGTH is carried by <see cref="TabStop"/> instead, which wins
    /// when it is set.
    /// </remarks>
    public float TabSize { get; init; } = 8;

    /// <summary>
    /// The tab stop spacing as an absolute length in CSS pixels, or null when it is a count.
    /// </summary>
    /// <remarks>
    /// A second field rather than a flag on <see cref="TabSize"/>, because the two forms mean
    /// genuinely different things: one is a distance and the other is a multiple of something that
    /// depends on the font. A length is the more useful of the two in a PROPORTIONAL font, where
    /// "eight spaces" is a different width in every face on the page.
    /// </remarks>
    public float? TabStop { get; init; }

    /// <summary>When a line may break inside a word. Inherited, as both source properties are.</summary>
    public WordBreaking WordBreaking { get; init; }

    /// <summary>
    /// A raster image painted as the background, or null.
    /// </summary>
    /// <remarks>
    /// Resolved through the same <see cref="ImageStore"/> an <c>&lt;img&gt;</c> goes through, so a
    /// <c>url()</c> in a stylesheet is bound by <see cref="HtmlOptions.LocalImages"/> and
    /// <see cref="HtmlOptions.WebImages"/> exactly as one in the markup is. It would be a poor
    /// place to leave a hole: a stylesheet is the part of a document a reader is least likely to
    /// have read.
    /// </remarks>
    public ImageData? BackgroundPicture { get; init; }

    /// <summary>
    /// The area the whole background is painted within.
    /// </summary>
    /// <remarks>
    /// The border box by default, which is why the strip under a border is painted at all — and
    /// why it shows through a dashed or translucent one.
    /// </remarks>
    public BoxArea BackgroundClip { get; init; } = BoxArea.Border;

    /// <summary>
    /// The area a background image is positioned against.
    /// </summary>
    /// <remarks>
    /// The PADDING box by default, which is not the same as the clip and is the reason a repeated
    /// background under a border shows the tail of the previous tile rather than the head of the
    /// first.
    /// </remarks>
    public BoxArea BackgroundOrigin { get; init; } = BoxArea.Padding;

    /// <summary>Whether the background image repeats horizontally.</summary>
    public bool BackgroundRepeatX { get; init; } = true;

    /// <summary>Whether it repeats vertically.</summary>
    public bool BackgroundRepeatY { get; init; } = true;

    /// <summary>
    /// Where the first tile sits horizontally within the positioning area.
    /// </summary>
    /// <remarks>
    /// A percentage here does NOT mean a fraction of the box. It aligns that fraction of the IMAGE
    /// with the same fraction of the box, so <c>25%</c> places the tile a quarter of the way
    /// through the room left over — measured, and the reason this cannot be resolved by the
    /// ordinary percentage path.
    /// </remarks>
    public CssLength BackgroundPositionX { get; init; } = CssLength.Zero;

    /// <summary>Where the first tile sits vertically.</summary>
    public CssLength BackgroundPositionY { get; init; } = CssLength.Zero;

    /// <summary>How the background image is scaled.</summary>
    public BackgroundSizing BackgroundSize { get; init; }

    /// <summary>The declared width under <see cref="BackgroundSizing.Explicit"/>.</summary>
    public CssLength BackgroundSizeX { get; init; } = CssLength.Auto;

    /// <summary>The declared height under <see cref="BackgroundSizing.Explicit"/>.</summary>
    public CssLength BackgroundSizeY { get; init; } = CssLength.Auto;

    /// <summary>
    /// An image standing in for this list item's marker, or null.
    /// </summary>
    /// <remarks>
    /// Inherited, like the rest of the <c>list-style</c> family, so it is set on the list and read
    /// on the items. Null when the source did not resolve, which is what makes the counter style
    /// the fallback rather than an empty marker — measured: Chrome draws the square a
    /// <c>list-style-type</c> asked for when the image behind it is missing.
    /// </remarks>
    public ImageData? MarkerImage { get; init; }

    /// <summary>Where a replaced element's content sits inside its box.</summary>
    /// <remarks>
    /// The same rule <see cref="BackgroundPositionX"/> follows, and measured to be exactly that: a
    /// percentage aligns that fraction of the content with the same fraction of the box, so
    /// <c>25%</c> of the 96px left over is 24px. It applies AFTER
    /// <see cref="ObjectFit"/> has decided the content's size, so under <c>cover</c> the offsets go
    /// negative and choose which part survives the clip.
    /// </remarks>
    public CssLength ObjectPositionX { get; init; } = CssLength.Percentage(50);

    /// <summary>Where a replaced element's content sits vertically.</summary>
    public CssLength ObjectPositionY { get; init; } = CssLength.Percentage(50);

    /// <summary>
    /// Whether an empty table cell paints nothing, from <c>empty-cells: hide</c>.
    /// </summary>
    /// <remarks>
    /// Inherited, and read on the cell — which is how a declaration on the table reaches its cells,
    /// the same route <c>vertical-align: middle</c> takes. It hides the ink and nothing else: the
    /// cell keeps its place in the grid and the rows do not close up, so the geometry comparison
    /// confirms the property by staying still.
    /// </remarks>
    public bool HideEmptyCells { get; init; }

    /// <summary>
    /// The fewest lines of a block that may be left at the FOOT of a page.
    /// </summary>
    /// <remarks>
    /// Inherited, and 2 by CSS's own initial value — which is why a browser never leaves a single
    /// line of a paragraph stranded at the bottom of a page, and why ignoring the property is a
    /// visible difference on any document long enough to paginate rather than an omission only
    /// pedants notice.
    /// </remarks>
    public int Orphans { get; init; } = 2;

    /// <summary>The fewest lines that may be carried to the HEAD of the next page.</summary>
    public int Widows { get; init; } = 2;

    /// <summary>
    /// The counters this element resets, each with the value it resets to.
    /// </summary>
    /// <remarks>
    /// Empty for nearly every element, which is why it is an array rather than a dictionary: the
    /// cost that matters is the one paid per element, and an empty array is a shared singleton.
    /// </remarks>
    public (string Name, int Value)[] CounterReset { get; init; } = [];

    /// <summary>The counters this element increments, each by the amount given.</summary>
    public (string Name, int Value)[] CounterIncrement { get; init; } = [];

    /// <summary>
    /// The quotation marks <c>open-quote</c> and <c>close-quote</c> draw, by nesting depth.
    /// </summary>
    /// <remarks>
    /// Inherited, and pairs flattened: index 0 and 1 are the outermost pair, 2 and 3 the next.
    /// A depth past the end reuses the LAST pair, which is CSS's own rule and what keeps a deeply
    /// nested quotation from losing its marks.
    /// </remarks>
    public string[] Quotes { get; init; } = ["“", "”", "‘", "’"];

    /// <summary>
    /// The opacity of <see cref="BackgroundColor"/>, from the colour's fourth component.
    /// </summary>
    /// <remarks>
    /// Separate from the colour because <see cref="Krilla.Color"/> has no alpha — krilla models
    /// opacity as a fill property rather than as a fourth channel. Carrying it alongside is what
    /// makes <c>rgba()</c> work, and <c>rgba()</c> is how nearly every translucent panel, overlay and
    /// tint in modern CSS is written.
    /// </remarks>
    public float BackgroundAlpha { get; init; } = 1;

    /// <summary>The opacity of <see cref="Color"/>, which text and decorations are drawn with.</summary>
    /// <remarks>Inherited with the colour, as CSS inherits the whole value.</remarks>
    public float TextAlpha { get; init; } = 1;

    /// <summary>
    /// The preferred ratio of width to height, or zero when there is none.
    /// </summary>
    /// <remarks>
    /// Held as one number rather than as a pair, because every use of it divides: a width of 200 at
    /// <c>4 / 1</c> is a height of 50. Zero rather than null so the common case costs no allocation
    /// and no unwrapping.
    /// </remarks>
    public float AspectRatio { get; init; }

    /// <summary>
    /// The shadows cast by this box's border box, painted farthest-first.
    /// </summary>
    /// <remarks>
    /// Empty for nearly every box, and empty for any shadow this engine cannot draw exactly — see
    /// <see cref="BoxShadow"/> for which those are and why. A layer that is dropped is reported.
    /// </remarks>
    public BoxShadow[] BoxShadows { get; init; } = [];

    /// <summary>The shadows cast by this element's text, painted farthest-first.</summary>
    public BoxShadow[] TextShadows { get; init; } = [];

    /// <summary>
    /// The thickness of a text decoration's rule, or null to take the font's own.
    /// </summary>
    public float? DecorationThickness { get; init; }

    /// <summary>
    /// How far below the font's own underline position the rule sits, or null for none.
    /// </summary>
    public float? UnderlineOffset { get; init; }

    /// <summary>Which side of the table the caption sits on.</summary>
    public CaptionSideKind CaptionSide { get; init; }

    /// <summary>Where this list item's marker sits.</summary>
    /// <remarks>
    /// Inherited, like the rest of the <c>list-style</c> family, so it is set on the list and read
    /// on the items.
    /// </remarks>
    public ListStylePositionKind ListStylePosition { get; init; }

    /// <summary>How this replaced element's content fills its box.</summary>
    public ObjectFitKind ObjectFit { get; init; }

    /// <summary>Horizontal and vertical radii of the top-left corner.</summary>
    /// <remarks>
    /// Two lengths per corner rather than one, because the slash form of the shorthand asks for an
    /// ellipse quadrant: <c>border-radius: 30px / 12px</c> is 30 across and 12 down. Kept as
    /// declared rather than resolved, since a percentage resolves against the box's own width and
    /// height and neither is known until layout has finished.
    /// </remarks>
    public (CssLength X, CssLength Y) RadiusTopLeft { get; init; }

    /// <summary>Horizontal and vertical radii of the top-right corner.</summary>
    public (CssLength X, CssLength Y) RadiusTopRight { get; init; }

    /// <summary>Horizontal and vertical radii of the bottom-right corner.</summary>
    public (CssLength X, CssLength Y) RadiusBottomRight { get; init; }

    /// <summary>Horizontal and vertical radii of the bottom-left corner.</summary>
    public (CssLength X, CssLength Y) RadiusBottomLeft { get; init; }

    /// <summary>Whether any corner asks to be rounded.</summary>
    public bool HasRadius =>
        IsRounded(RadiusTopLeft) ||
        IsRounded(RadiusTopRight) ||
        IsRounded(RadiusBottomRight) ||
        IsRounded(RadiusBottomLeft);

    /// <summary>
    /// Whether one corner asks for anything.
    /// </summary>
    /// <remarks>
    /// Tested on the VALUE rather than against <c>default</c>: an unset radius is
    /// <see cref="CssLength.Zero"/>, which is an absolute zero and not the same struct as a
    /// default <c>CssLength</c>, whose kind is <c>Auto</c>. Comparing structs answers true for
    /// every box in the document and sends every background down the rounded path.
    /// </remarks>
    static bool IsRounded((CssLength X, CssLength Y) corner) =>
        corner.X.Value != 0 || corner.Y.Value != 0;

    /// <summary>
    /// Whether each border colour came from <c>currentColor</c> rather than from a declaration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a BEVELLED edge reads this, and it reads it because Chromium does something with the
    /// case that cannot be derived from the colour: an <c>inset</c> border whose colour was not
    /// declared is drawn in a fixed pair of greys, whatever the element's own <c>color</c> is.
    /// Measured — a box with <c>color: gray</c> and one with <c>color: black</c> produce the same
    /// two shades, and a box declaring <c>border-color: gray</c> produces the shades derived from
    /// grey instead.
    /// </para>
    /// <para>
    /// Per side rather than one flag for the box, because the flag has to travel with the colour it
    /// qualifies and a border can declare a colour on one edge and not another. Four properties for
    /// what is nearly always one answer, and the alternative is a simplification that would be wrong
    /// exactly where the corpus would find it.
    /// </para>
    /// </remarks>
    public bool BorderTopColorIsCurrent { get; init; }

    /// <inheritdoc cref="BorderTopColorIsCurrent"/>
    public bool BorderRightColorIsCurrent { get; init; }

    /// <inheritdoc cref="BorderTopColorIsCurrent"/>
    public bool BorderBottomColorIsCurrent { get; init; }

    /// <inheritdoc cref="BorderTopColorIsCurrent"/>
    public bool BorderLeftColorIsCurrent { get; init; }

    /// <summary>How the top border edge is drawn.</summary>
    public BorderStyleKind BorderTopStyle { get; init; }

    /// <summary>How the right border edge is drawn.</summary>
    public BorderStyleKind BorderRightStyle { get; init; }

    /// <summary>How the bottom border edge is drawn.</summary>
    public BorderStyleKind BorderBottomStyle { get; init; }

    /// <summary>How the left border edge is drawn.</summary>
    public BorderStyleKind BorderLeftStyle { get; init; }

    /// <summary>
    /// Whether every edge is drawn as one unbroken band.
    /// </summary>
    /// <remarks>
    /// The uniform-colour border is painted as a single ring rather than four mitred trapezia,
    /// because two antialiased edges meeting on a mitre diagonal do not composite to full
    /// coverage. That shortcut needs the STYLE to be uniform as well as the colour, which is what
    /// this answers.
    /// </remarks>
    public bool PaintsBorderAsRing =>
        IsBorderSolid &&
        BorderTopColor is {} colour &&
        BorderRightColor == colour &&
        BorderBottomColor == colour &&
        BorderLeftColor == colour;

    /// <summary>
    /// Whether every edge is drawn as one unbroken band.
    /// </summary>
    public bool IsBorderSolid =>
        BorderTopStyle == BorderStyleKind.Solid &&
        BorderRightStyle == BorderStyleKind.Solid &&
        BorderBottomStyle == BorderStyleKind.Solid &&
        BorderLeftStyle == BorderStyleKind.Solid;

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

    /// <summary>The rules drawn through this text.</summary>
    /// <remarks>
    /// Treated as inherited, which <c>text-decoration</c> strictly is not — CSS says a decoration
    /// is drawn ACROSS descendants by the element that declared it, rather than being inherited by
    /// them. The distinction shows only where a descendant sets its own colour, since a propagated
    /// decoration keeps the colour of the element that declared it while an inherited one does not.
    /// Inheriting is the far simpler model and agrees with propagation everywhere else.
    /// </remarks>
    public TextDecorations Decorations { get; init; }

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

    /// <summary>How a cell's content sits within a taller row, or a box sits on its line.</summary>
    public VerticalAlignKind VerticalAlign { get; init; } = VerticalAlignKind.Baseline;

    /// <summary>
    /// Whether the cascade actually carried <c>vertical-align</c> for this element.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Needed because this engine INHERITS the property, which CSS does not. The inheritance is
    /// deliberate and load-bearing for tables: the user-agent sheet puts <c>middle</c> on the table
    /// and <c>inherit</c> on the cells, so a cell can only read its value by having it handed down.
    /// </para>
    /// <para>
    /// The cost is that every run of text inside a cell also arrives carrying <c>middle</c>, and
    /// line layout must not act on it — a text token shifted half an x-height would move every
    /// cell's text and break every table scenario at once. So inline alignment applies only where
    /// the value was declared rather than inherited, which is true of <c>&lt;sub&gt;</c> and
    /// <c>&lt;sup&gt;</c> (the default stylesheet declares theirs) and false of a bare
    /// <c>&lt;span&gt;</c> in a cell.
    /// </para>
    /// </remarks>
    public bool VerticalAlignDeclared { get; init; }

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

    /// <summary>Whether a page must start at this box's top border edge.</summary>
    /// <remarks>
    /// Not inherited, and deliberately absent from the anonymous box's style: a wrapper generated
    /// around a run of text is not the box the author declared the break on, and giving it the
    /// same request would take the break twice.
    /// </remarks>
    public BreakKind BreakBefore { get; init; } = BreakKind.Auto;

    /// <summary>Whether a page must start after this box.</summary>
    /// <remarks>
    /// After this box, not at its bottom edge — the two differ by whatever margin sits between it
    /// and what follows, and a browser drops that margin. <see cref="Paginator"/> resolves it to
    /// the top of the next in-flow box for that reason.
    /// </remarks>
    public BreakKind BreakAfter { get; init; } = BreakKind.Auto;

    /// <summary>Whether this box may be split across a page break.</summary>
    public BreakKind BreakInside { get; init; } = BreakKind.Auto;

    /// <summary>Whether this box and its descendants are painted.</summary>
    public VisibilityKind Visibility { get; init; } = VisibilityKind.Visible;

    /// <summary>The gradient painted over this box's background colour, if any.</summary>
    /// <remarks>
    /// Parsed during the cascade and resolved against the box at paint time, since a corner
    /// keyword's direction, the gradient line's length and a stop given in pixels all depend on the
    /// box's size. Null for a value that is not a gradient this engine draws — a <c>url()</c>, a
    /// repeating gradient, or a radial one carrying an explicit size — which is what
    /// <see cref="UnsupportedCss"/> reports.
    /// </remarks>
    public CssGradient? BackgroundImage { get; init; }

    /// <summary>The transform applied to this box when it is painted, if any.</summary>
    /// <remarks>
    /// Painting only: the box keeps the space layout gave it and its siblings sit where they would
    /// have, which is what makes a transform as cheap as <c>position: relative</c>. Null for a
    /// value carrying a function this engine does not apply, which is what
    /// <see cref="UnsupportedCss"/> reports.
    /// </remarks>
    public CssTransform? Transform { get; init; }

    /// <summary>How opaque this box and its descendants are, from 0 to 1.</summary>
    /// <remarks>
    /// Not inherited, and not a fill alpha. The box and everything under it are drawn into a group
    /// and the GROUP is made transparent, so two overlapping children of a half-opaque parent show
    /// the same shade in the overlap as anywhere else. Fading each fill on its own would darken it,
    /// which is the whole reason this needs a stacking context rather than a colour with an alpha.
    /// </remarks>
    public float Opacity { get; init; } = 1f;

    /// <summary>The <c>z-index</c> this box declared, or null for <c>auto</c>.</summary>
    /// <remarks>
    /// Null is not the same as zero, and the difference is not only where the box paints: a
    /// positioned box given any integer — <c>0</c> included — establishes a stacking context and
    /// confines its positioned descendants to it, where one left at <c>auto</c> does not and lets
    /// them flatten onto the page. Which is what makes <c>z-index: 0</c> a real declaration rather
    /// than the no-op it reads as, and what <c>position/z_index</c>'s last row measures.
    /// </remarks>
    public int? ZIndex { get; init; }

    /// <summary>Where this box sorts among the contexts its parent paints.</summary>
    /// <remarks>
    /// <c>z-index</c> applies to positioned boxes only, so an integer on a static one is ignored by
    /// CSS itself rather than by omission here — and sorts where <c>auto</c> sorts, which is also
    /// where a box establishing a context through <c>opacity</c> or <c>transform</c> alone belongs.
    /// CSS Color's rule for an unpositioned faded box says exactly that: paint it where a
    /// positioned box with <c>z-index: 0</c> would go.
    /// </remarks>
    public int StackingOrder
    {
        get
        {
            if (IsPositioned)
            {
                return ZIndex ?? 0;
            }

            return 0;
        }
    }

    /// <summary>
    /// Whether this box establishes a stacking context of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>opacity</c>, <c>transform</c>, and a <c>z-index</c> that is not <c>auto</c> on a
    /// positioned box — the three this engine reads that do it. The consequence that matters is
    /// paint ORDER: such a box leaves its parent's phases and paints with the positioned content,
    /// after every in-flow background and line on the page — so a faded box written first covers an
    /// opaque sibling written after it, which is measurable and is what <c>block/opacity</c>'s last
    /// row measures.
    /// </para>
    /// <para>
    /// <c>position: fixed</c> is the fourth, and it is here for a structural reason as well as a
    /// specified one. CSS Position 3 says a fixed box always establishes one; here that is also
    /// what keeps its subtree together, because a fixed box is painted through an extra
    /// per-page translate and anything flattened out of it onto the page would be painted without
    /// that translate — at its page-one position, on every page.
    /// </para>
    /// </remarks>
    public bool CreatesStackingContext =>
        Opacity < 1 ||
        Transform is not null ||
        Position == PositionKind.Fixed ||
        (IsPositioned && ZIndex is not null);

    /// <summary>How this box's text is cased before shaping.</summary>
    public TextTransformKind TextTransform { get; init; } = TextTransformKind.None;

    /// <summary>Extra advance after each character, in CSS pixels.</summary>
    /// <remarks>
    /// After EACH character including the last, which is measurable and was measured: seven
    /// characters at 3px are 21px wider rather than 18px, so a shrink-wrapped box carries the
    /// spacing past its final glyph.
    /// </remarks>
    public float LetterSpacing { get; init; }

    /// <summary>Extra advance added to each space, in CSS pixels.</summary>
    public float WordSpacing { get; init; }

    /// <summary>Whether this box clips what overflows it.</summary>
    /// <remarks>
    /// Not inherited, unlike everything above it here. Anything other than <c>visible</c> also
    /// makes the box establish a block formatting context, which <see cref="EstablishesContext"/>
    /// answers.
    /// </remarks>
    public OverflowKind Overflow { get; init; } = OverflowKind.Visible;

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

    /// <summary>
    /// Whether this box establishes a block formatting context of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only <c>overflow</c> answers here. A float, an inline-block and a table cell establish one
    /// too, and each reaches it structurally instead — by being laid out through a call that
    /// passes no <see cref="FloatContext"/> — because each is already on a path of its own. This
    /// property is for the one case that is an ordinary in-flow block and has to be recognised
    /// from its style.
    /// </para>
    /// <para>
    /// Two consequences, and both are measured by <c>float/overflow_bfc</c>: the box grows to
    /// contain its own floats, and it is placed BESIDE a float outside it rather than overlapping
    /// it. The second is the reason the property is written at all in practice — a float with a
    /// text block beside it is the pre-flexbox way to lay out a media object, and the text block
    /// is given <c>overflow: hidden</c> for exactly this effect.
    /// </para>
    /// </remarks>
    public bool EstablishesContext => Overflow != OverflowKind.Visible;

    /// <summary>Whether this box is taken out of flow by positioning.</summary>
    public bool IsAbsolute => Position is PositionKind.Absolute or PositionKind.Fixed;

    /// <summary>
    /// Whether this box is the containing block for absolutely positioned descendants.
    /// </summary>
    public bool IsPositioned => Position != PositionKind.Static;

    /// <summary>
    /// Whether this box is drawn at the same place on every page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CSS 2.1 §9.6.1 says a fixed box in paged media is repeated on every page, and Chromium's
    /// printer agrees — a fixed banner appears at the same corner of every sheet, which is what
    /// <c>page/fixed_repeat</c> measures.
    /// </para>
    /// <para>
    /// A VERTICAL anchor is required, and that is the one part of this not taken from the
    /// specification. With both <c>top</c> and <c>bottom</c> auto the box sits at its static
    /// position, which is a position in the DOCUMENT — repeating it would add each page's own top
    /// to a coordinate that already includes it, so a box whose flow position is on page three
    /// would fall off the bottom of every page and disappear from a document it currently appears
    /// in. Such a box is painted once, where flow put it.
    /// </para>
    /// </remarks>
    public bool RepeatsOnEveryPage =>
        Position == PositionKind.Fixed &&
        (Top.Kind != LengthKind.Auto || Bottom.Kind != LengthKind.Auto);

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
    public float? ContentSize(float? declared, float surround)
    {
        if (declared is {} value)
        {
            return ContentSize(value, surround);
        }

        return null;
    }

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
    /// <summary>
    /// Whether this element has any padding or border at all, on any side.
    /// </summary>
    /// <remarks>
    /// Read to decide whether an inline element needs the two marker items that carry its edges.
    /// Deliberately tests the DECLARED padding rather than a resolved one: a percentage padding is
    /// not zero however small the containing block turns out to be.
    /// </remarks>
    public bool HasSurround =>
        BorderTop > 0 || BorderRight > 0 || BorderBottom > 0 || BorderLeft > 0 ||
        !IsZeroLength(PaddingTop) || !IsZeroLength(PaddingRight) ||
        !IsZeroLength(PaddingBottom) || !IsZeroLength(PaddingLeft) ||
        // The horizontal margins alone. CSS applies those to an inline element and drops the
        // vertical pair, so a top margin here is not a reason to give the element edges.
        !IsZeroLength(MarginLeft) || !IsZeroLength(MarginRight);

    static bool IsZeroLength(CssLength length) =>
        length is {Value: 0, Percent: 0};

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
    public float ResolveLineHeight(FontFace face)
    {
        if (LineHeightScale is { } scale)
        {
            return scale * FontSize;
        }

        return LineHeight ?? face.NormalLineHeight(FontSize);
    }
}
