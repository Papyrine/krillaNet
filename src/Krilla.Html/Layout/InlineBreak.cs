/// <summary>A line break element, and the empty rectangle a browser reports for it.</summary>
/// <param name="Selector">The selector path of the <c>&lt;br&gt;</c>.</param>
/// <param name="Bounds">
/// Zero-width, at the end of the line it ended, as tall as the font's ascent plus descent.
/// </param>
readonly record struct InlineBreak(string Selector, Rect Bounds);