namespace Krilla;

/// <summary>
/// An identifier for a span of tagged content or a tagged annotation.
/// </summary>
/// <remarks>
/// Each must be placed in the tag tree exactly once. An identifier that never appears, or
/// appears twice, is reported when the document is finished.
/// </remarks>
public readonly record struct TagIdentifier
{
    internal TagIdentifier(nuint slot) =>
        Slot = slot;

    internal nuint Slot { get; }
}