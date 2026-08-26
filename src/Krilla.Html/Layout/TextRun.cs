/// <summary>A positioned run of text, ready to paint.</summary>
/// <param name="Text">The run's text.</param>
/// <param name="Style">The style to paint it with.</param>
/// <param name="Face">The resolved face, already matched against the style.</param>
/// <param name="X">Left edge of the run.</param>
/// <param name="Y">The run's baseline.</param>
/// <param name="Width">The run's advance width.</param>
/// <param name="Link">
/// The <c>href</c> this run links to, or null. One annotation is emitted per run, which is why runs
/// do not merge across a link boundary: a PDF link is a rectangle, so an anchor spanning three
/// lines needs three of them.
/// </param>
/// <param name="Glyphs">
/// The shaped glyphs, already positioned relative to the run's origin. Carried rather than
/// re-derived at paint time so that what is drawn is exactly what the line was measured with —
/// shaping twice would leave the two free to disagree.
/// </param>
/// <param name="Backdrops">
/// The inline ancestors painting a background behind this run, outermost first, or null.
/// </param>
/// <param name="Generated">Whether this run came from a <c>::before</c> or <c>::after</c>.</param>
/// <param name="Selector">
/// The path of the element this run's text came from, or null for text with no element of its own.
/// Carried so <see cref="Krilla.Html.Diagnostics.BoxDump"/> can report a rectangle for an inline
/// element, which produces no box and would otherwise be geometry the corpus cannot see at all.
/// </param>
readonly record struct TextRun(
    string Text,
    ComputedStyle Style,
    FontFace Face,
    float X,
    float Y,
    float Width,
    AnchorLink? Link = null,
    IReadOnlyList<Glyph>? Glyphs = null,
    string? Selector = null,
    IReadOnlyList<InlineBackdrop>? Backdrops = null,
    bool Generated = false);