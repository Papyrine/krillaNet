namespace Krilla;

/// <summary>
/// The PDF specification version to target.
/// </summary>
public enum PdfVersion
{
    Pdf14 = 0,
    Pdf15 = 1,
    Pdf16 = 2,
    Pdf17 = 3,
    Pdf20 = 4
}

/// <summary>
/// PDF/A conformance, for documents intended for long-term archiving.
/// </summary>
/// <remarks>
/// Level <c>A</c> additionally requires a tagged structure tree, so it implies
/// <see cref="DocumentOptions.EnableTagging"/>. Level <c>B</c> requires only that the document
/// renders identically in future; <c>U</c> adds a requirement that all text be extractable as
/// Unicode.
/// </remarks>
public enum PdfArchival
{
    /// <summary>No archival conformance.</summary>
    None = -1,

    /// <summary>PDF/A-1a.</summary>
    A1A = 0,

    /// <summary>PDF/A-1b.</summary>
    A1B = 1,

    /// <summary>PDF/A-2a.</summary>
    A2A = 2,

    /// <summary>PDF/A-2b.</summary>
    A2B = 3,

    /// <summary>PDF/A-2u.</summary>
    A2U = 4,

    /// <summary>PDF/A-3a.</summary>
    A3A = 5,

    /// <summary>PDF/A-3b.</summary>
    A3B = 6,

    /// <summary>PDF/A-3u.</summary>
    A3U = 7,

    /// <summary>PDF/A-4.</summary>
    A4 = 8,

    /// <summary>PDF/A-4f, which permits embedded files.</summary>
    A4F = 9,

    /// <summary>PDF/A-4e, for engineering documents.</summary>
    A4E = 10
}

/// <summary>
/// PDF/UA conformance, for accessible documents.
/// </summary>
/// <remarks>
/// Requires a tagged structure tree, a document title, and a language — and alt text on every
/// figure. Violations are reported when the document is finished.
/// </remarks>
public enum PdfAccessibility
{
    /// <summary>No accessibility conformance.</summary>
    None = -1,

    /// <summary>PDF/UA-1.</summary>
    Ua1 = 0
}

/// <summary>
/// How a viewer should arrange pages.
/// </summary>
public enum PageLayout
{
    /// <summary>One page at a time.</summary>
    SinglePage = 0,

    /// <summary>One continuous column.</summary>
    OneColumn = 1,

    /// <summary>Two columns, odd-numbered pages on the left.</summary>
    TwoColumnLeft = 2,

    /// <summary>Two columns, odd-numbered pages on the right.</summary>
    TwoColumnRight = 3,

    /// <summary>Two pages at a time, odd-numbered pages on the left.</summary>
    TwoPageLeft = 4,

    /// <summary>Two pages at a time, odd-numbered pages on the right.</summary>
    TwoPageRight = 5
}

/// <summary>
/// Settings applied when a document is created.
/// </summary>
/// <remarks>
/// Passed to the <see cref="KrillaDocument(DocumentOptions)"/> constructor. Conformance is
/// checked as the document is built, and violations are reported when it is finished — not
/// when the offending content is added.
/// </remarks>
public sealed class DocumentOptions
{
    /// <summary>
    /// The PDF version to target.
    /// </summary>
    public PdfVersion Version { get; set; } = PdfVersion.Pdf17;

    /// <summary>
    /// PDF/A conformance level.
    /// </summary>
    public PdfArchival Archival { get; set; } = PdfArchival.None;

    /// <summary>
    /// PDF/UA conformance level.
    /// </summary>
    public PdfAccessibility Accessibility { get; set; } = PdfAccessibility.None;

    /// <summary>
    /// Compress content streams. Strongly recommended, and on by default.
    /// </summary>
    public bool CompressStreams { get; set; } = true;

    /// <summary>
    /// Restrict output to 7-bit ASCII, at a cost in size.
    /// </summary>
    public bool AsciiCompatible { get; set; }

    /// <summary>
    /// Embed XMP metadata. Required by PDF/A.
    /// </summary>
    /// <remarks>
    /// XMP packets embed a timestamp, so this defeats byte-reproducible output unless a fixed
    /// creation date is also set.
    /// </remarks>
    public bool XmpMetadata { get; set; }

    /// <summary>
    /// Build the tagged structure tree. Required by PDF/UA and by PDF/A level A.
    /// </summary>
    public bool EnableTagging { get; set; }

    /// <summary>
    /// Write the PDF with readable formatting, for debugging.
    /// </summary>
    public bool Pretty { get; set; }

    /// <summary>
    /// Avoid device colour spaces, writing everything in an explicitly defined one.
    /// </summary>
    public bool NoDeviceColorSpace { get; set; }

    internal NativeDocumentOptions ToNative() =>
        new()
        {
            PdfVersion = (int) Version,
            Archival = (int) Archival,
            Accessibility = (int) Accessibility,
            CompressStreams = CompressStreams ? (byte) 1 : (byte) 0,
            AsciiCompatible = AsciiCompatible ? (byte) 1 : (byte) 0,
            XmpMetadata = XmpMetadata ? (byte) 1 : (byte) 0,
            EnableTagging = EnableTagging ? (byte) 1 : (byte) 0,
            Pretty = Pretty ? (byte) 1 : (byte) 0,
            NoDeviceColorspace = NoDeviceColorSpace ? (byte) 1 : (byte) 0,
            Reserved = 0
        };
}
