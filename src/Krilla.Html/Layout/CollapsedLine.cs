/// <summary>
/// One resolved grid line, ready to paint.
/// </summary>
/// <param name="Bounds">The rectangle the line fills.</param>
/// <param name="Color">Its colour.</param>
sealed record CollapsedLine(Rect Bounds, Color Color);