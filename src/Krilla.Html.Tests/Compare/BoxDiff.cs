/// <summary>
/// One element's disagreement, as our value minus the browser's. Positive <see cref="Dy"/> means
/// we placed it lower than the browser did.
/// </summary>
public record BoxDiff(string Selector, float Dx, float Dy, float Dw, float Dh);