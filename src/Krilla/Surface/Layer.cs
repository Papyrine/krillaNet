namespace Krilla;

/// <summary>
/// A pushed surface state, reverted when disposed.
/// </summary>
/// <remarks>
/// krilla requires every push to be matched by a pop and asserts this when the page closes.
/// Tying the pop to <see cref="IDisposable"/> means a <c>using</c> statement makes the pairing
/// structural rather than something the caller has to remember.
/// </remarks>
public readonly struct Layer :
    IDisposable
{
    readonly Surface surface;

    internal Layer(Surface surface) =>
        this.surface = surface;

    /// <inheritdoc />
    public void Dispose() =>
        surface.Pop();
}