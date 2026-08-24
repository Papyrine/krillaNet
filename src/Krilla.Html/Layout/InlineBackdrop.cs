/// <summary>
/// An inline element enclosing a run that is not its own text.
/// </summary>
/// <remarks>
/// An inline element generates no box, so both its painting and its geometry are carried by
/// whatever text sits inside it — including text belonging to a nested inline element, which is
/// the case this exists for. The face is resolved alongside the style because the rectangle is the
/// ANCESTOR's font box rather than the run's: a small nested element inside a large one is backed
/// to the large one's height, and a browser reports it at that height too.
/// </remarks>
readonly record struct InlineBackdrop(ComputedStyle Style, FontFace Face);