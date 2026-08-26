/// <summary>An image positioned on a line.</summary>
/// <param name="Image">The image to draw.</param>
/// <param name="Bounds">
/// The BORDER box, which is what a browser reports for the element and what its background and
/// border are painted into.
/// </param>
/// <param name="Content">
/// Where the picture itself goes. Carried rather than derived from <paramref name="Bounds"/>,
/// because a marker image has no box of its own and must not be deflated by the surround of the
/// list item that generated it.
/// </param>
/// <param name="Selector">
/// The selector path of the <c>&lt;img&gt;</c> it came from. Carried so the box dump can report an
/// inline image's geometry: it is a real box whose position and size are known exactly, and
/// omitting it would leave the one replaced element in the corpus unmeasured.
/// </param>
/// <param name="Decorated">
/// Whether the background and border of <paramref name="Style"/> belong to this image. False for a
/// marker image, whose style is the LIST ITEM’s — so painting it would draw the item’s own
/// background a second time, and its border inside its bullet.
/// </param>
/// <param name="Style">
/// The element's own style, for the surround. An atomic inline is the one inline-level box that
/// takes its whole box model — vertical margins and all — where a run of text takes only the
/// horizontal half of it.
/// </param>
readonly record struct InlineImage(
    ImageData Image,
    Rect Bounds,
    Rect Content,
    string? Selector,
    ComputedStyle Style,
    bool Decorated);