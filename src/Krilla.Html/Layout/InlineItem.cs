/// <summary>A run of text waiting to be flowed into lines.</summary>
/// <param name="Text">The text, after white-space processing.</param>
/// <param name="Style">The style it is measured and painted with.</param>
/// <param name="Selector">
/// The selector path of the inline element this run came from, or null when it came straight from
/// a block's own text.
/// </param>
/// <param name="ForcedBreak">
/// Whether this item is a <c>&lt;br&gt;</c> rather than text. Carried as a flag rather than as a
/// newline character because white-space processing would collapse a newline into a space under
/// the default <c>white-space: normal</c>, which is exactly what a forced break must survive.
/// </param>
/// <param name="SoftBreak">
/// Set on a <c>&lt;wbr&gt;</c>: a break OPPORTUNITY rather than a break. It carries no text and
/// generates no box (a browser returns an empty rectangle for one), so the only trace it leaves
/// is that the line may break at the point it sits at.
/// </param>
/// <param name="Image">
/// An image, when this item is an inline-level replaced element rather than text. It occupies a
/// box on the line instead of a run of glyphs, which is what CSS calls an atomic inline.
/// </param>
/// <param name="Link">
/// The <c>href</c> of the enclosing <c>&lt;a&gt;</c>, or null. Carried on the item rather than
/// looked up later because an anchor's text is flattened into the line's runs, so by painting time
/// there is nothing left to ask which element it came from.
/// </param>
/// <param name="Box">
/// A whole box tree, when this item is an <c>inline-block</c>. The other atomic inline: it takes a
/// box on the line the way an image does, and differs in having contents of its own that were laid
/// out in their own formatting context before the line was filled.
/// </param>
/// <param name="Backdrops">
/// The styles of the inline ANCESTORS that paint a background over this item, outermost first.
/// Null when there are none, which is nearly always.
/// </param>
/// <param name="Generated">
/// Set on content produced by a <c>::before</c> or <c>::after</c>. It has a style of its own and no
/// element, so this is what tells the painter to draw its background — a selector cannot, there
/// being no element to name.
/// </param>
/// <param name="Marker">
/// Set on the image standing in for a list marker. Either way it contributes its height, which is
/// measured: a 32px image marker grows a 24px item to 39px, exactly as an atomic inline of that
/// height would. The two placements differ in the advance they take.
/// </param>
/// <param name="Edge">
/// Set when this item is not content at all but the opening or closing edge of an inline element
/// carrying padding or a border. Emitted only for an element that has one, so a document full of
/// plain <c>&lt;span&gt;</c>s produces none.
/// </param>
sealed record InlineItem(
    string Text,
    ComputedStyle Style,
    string? Selector,
    bool ForcedBreak = false,
    bool SoftBreak = false,
    ImageData? Image = null,
    string? Link = null,
    LayoutBox? Box = null,
    IReadOnlyList<ComputedStyle>? Backdrops = null,
    InlineEdgeKind Edge = InlineEdgeKind.None,
    MarkerPlacement Marker = MarkerPlacement.None,
    bool Generated = false);