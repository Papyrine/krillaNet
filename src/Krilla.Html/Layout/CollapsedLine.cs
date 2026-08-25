/// <summary>
/// One resolved grid line, ready to paint.
/// </summary>
/// <param name="Bounds">The rectangle the line fills.</param>
/// <param name="Color">Its colour.</param>
/// <param name="Alpha">
/// How opaque it is, from <c>rgba()</c>. Beside the colour rather than in it, because
/// <see cref="Krilla.Color"/> has no fourth channel.
/// </param>
sealed record CollapsedLine(Rect Bounds, Color Color, float Alpha = 1);