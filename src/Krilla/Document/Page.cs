namespace Krilla;

/// <summary>
/// An open page. Closing it flushes its content into the document.
/// </summary>
public sealed class Page :
    IDisposable
{
    readonly KrillaDocument document;
    readonly ulong token;
    bool closed;

    internal Page(KrillaDocument document, ulong token)
    {
        this.document = document;
        this.token = token;
        Surface = new(document, token);
    }

    /// <summary>
    /// The drawing area of this page.
    /// </summary>
    public Surface Surface { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (closed)
        {
            return;
        }

        closed = true;
        document.ClosePage(this, token);
    }
}