/// <summary>
/// Status codes returned by every native entry point. Mirrors <c>src/status.rs</c>.
/// </summary>
static class Status
{
    public const int Ok = 0;

    public const int NullArgument = 1;
    public const int InvalidArgument = 2;
    public const int InvalidUtf8 = 3;
    public const int InvalidGeometry = 4;

    public const int NoOpenPage = 10;
    public const int PageAlreadyOpen = 11;
    public const int StalePage = 12;
    public const int Finished = 13;
    public const int Poisoned = 14;

    public const int PopUnderflow = 20;
    public const int DepthLimit = 21;
    public const int TagAlreadyOpen = 22;
    public const int NoOpenTag = 23;

    public const int InvalidFont = 30;
    public const int InvalidImage = 31;
    public const int WrongDocument = 32;
    public const int Consumed = 33;

    public const int KrillaError = 40;

    public const int Panic = 90;

    /// <summary>
    /// Throws unless <paramref name="status"/> is <see cref="Ok"/>.
    /// </summary>
    public static void Check(int status, string operation)
    {
        if (status == Ok)
        {
            return;
        }

        throw new KrillaException($"{operation} failed: {Describe(status)}{Detail()}");
    }

    static string Detail()
    {
        var message = KrillaNative.LastErrorMessage();

        if (string.IsNullOrEmpty(message))
        {
            return "";
        }

        return $" ({message})";
    }

    static string Describe(int status) =>
        status switch
        {
            NullArgument => "a required argument was null",
            InvalidArgument => "an argument was outside its permitted range",
            InvalidUtf8 => "a string argument was not valid UTF-8",
            InvalidGeometry => "the geometry was rejected: a non-finite value, or a size or rectangle that is not strictly positive",
            NoOpenPage => "no page is open",
            PageAlreadyOpen => "a page is already open; only one page can be open at a time",
            StalePage => "the page this operation targets has already been closed",
            Finished => "the document has already been finished",
            Poisoned => "an earlier call faulted and the document is no longer usable",
            PopUnderflow => "there was no matching push for this pop",
            DepthLimit => "the push nesting limit was exceeded",
            TagAlreadyOpen => "a tagged section is already open",
            NoOpenTag => "no tagged section is open",
            InvalidFont => "the font data could not be parsed",
            InvalidImage => "the image data could not be decoded",
            WrongDocument => "the handle belongs to a different document",
            Consumed => "the builder has already been finished",
            Panic => "the native library faulted",
            _ => $"unexpected status {status}"
        };
}
