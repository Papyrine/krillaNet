namespace Krilla;

/// <summary>
/// Raised when krilla rejects an operation, or when the native library cannot be loaded.
/// </summary>
public class KrillaException :
    Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KrillaException"/> class.
    /// </summary>
    public KrillaException(string message) :
        base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KrillaException"/> class.
    /// </summary>
    public KrillaException(string message, Exception innerException) :
        base(message, innerException)
    {
    }
}
