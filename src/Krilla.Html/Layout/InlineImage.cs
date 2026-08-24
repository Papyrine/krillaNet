/// <summary>An image positioned on a line.</summary>
/// <param name="Image">The image to draw.</param>
/// <param name="Bounds">Where to draw it.</param>
/// <param name="Selector">
/// The selector path of the <c>&lt;img&gt;</c> it came from. Carried so the box dump can report an
/// inline image's geometry: it is a real box whose position and size are known exactly, and
/// omitting it would leave the one replaced element in the corpus unmeasured.
/// </param>
readonly record struct InlineImage(ImageData Image, Rect Bounds, string? Selector);