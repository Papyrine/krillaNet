namespace Krilla;

/// <summary>
/// Document-level metadata: title, authors, dates and viewer preferences.
/// </summary>
/// <remarks>
/// Apply with <see cref="KrillaDocument.SetMetadata"/>. PDF/UA requires a
/// <see cref="Title"/> and a <see cref="Language"/>; PDF/A requires a
/// <see cref="CreationDate"/>.
/// </remarks>
public sealed class DocumentMetadata
{
    /// <summary>
    /// The document title. Required by PDF/UA.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// A description of the document, written to the PDF <c>Subject</c> field.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The document's natural language as a BCP 47 tag, such as <c>en-GB</c>. Required by
    /// PDF/UA.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// The application that authored the original content.
    /// </summary>
    public string? Creator { get; set; }

    /// <summary>
    /// The application that produced the PDF.
    /// </summary>
    public string? Producer { get; set; }

    /// <summary>
    /// A stable identifier for the document.
    /// </summary>
    /// <remarks>
    /// Setting this together with <see cref="CreationDate"/> makes output byte-reproducible.
    /// Left unset, both are generated afresh on every run and no two documents compare equal.
    /// </remarks>
    public string? DocumentId { get; set; }

    /// <summary>
    /// The document authors.
    /// </summary>
    public IReadOnlyList<string>? Authors { get; set; }

    /// <summary>
    /// Keywords describing the document.
    /// </summary>
    public IReadOnlyList<string>? Keywords { get; set; }

    /// <summary>
    /// When the document was created. Required by PDF/A.
    /// </summary>
    public DateTimeOffset? CreationDate { get; set; }

    /// <summary>
    /// The dominant reading direction.
    /// </summary>
    public TextDirection? TextDirection { get; set; }

    /// <summary>
    /// How a viewer should arrange the pages.
    /// </summary>
    public PageLayout? PageLayout { get; set; }

    /// <summary>
    /// Builds the native metadata object, applying only the properties that were set.
    /// </summary>
    internal IntPtr Build()
    {
        Status.Check(KrillaNative.krilla_metadata_new(out var handle), "Creating metadata");

        try
        {
            Set(handle, Title, KrillaNative.krilla_metadata_set_title, "title");
            Set(handle, Description, KrillaNative.krilla_metadata_set_description, "description");
            Set(handle, Language, KrillaNative.krilla_metadata_set_language, "language");
            Set(handle, Creator, KrillaNative.krilla_metadata_set_creator, "creator");
            Set(handle, Producer, KrillaNative.krilla_metadata_set_producer, "producer");
            Set(handle, DocumentId, KrillaNative.krilla_metadata_set_document_id, "document id");

            SetList(handle, Authors, KrillaNative.krilla_metadata_set_authors, "authors");
            SetList(handle, Keywords, KrillaNative.krilla_metadata_set_keywords, "keywords");

            if (CreationDate is { } created)
            {
                Status.Check(
                    KrillaNative.krilla_metadata_set_creation_date(handle, ToNative(created)),
                    "Setting the creation date");
            }

            if (TextDirection is { } direction)
            {
                // krilla's metadata direction has no Auto: it is a document-level declaration,
                // unlike the per-run hint DrawText takes.
                var value = direction == Krilla.TextDirection.RightToLeft ? 1 : 0;
                Status.Check(
                    KrillaNative.krilla_metadata_set_text_direction(handle, value),
                    "Setting the text direction");
            }

            if (PageLayout is { } layout)
            {
                Status.Check(
                    KrillaNative.krilla_metadata_set_page_layout(handle, (int) layout),
                    "Setting the page layout");
            }

            return handle;
        }
        catch
        {
            KrillaNative.krilla_metadata_free(handle);
            throw;
        }
    }

    delegate int StringSetter(IntPtr handle, ReadOnlySpan<byte> text, nuint length);

    delegate int ListSetter(
        IntPtr handle,
        ReadOnlySpan<IntPtr> pointers,
        ReadOnlySpan<nuint> lengths,
        nuint count);

    static void Set(IntPtr handle, string? value, StringSetter setter, string what)
    {
        if (value is null)
        {
            return;
        }

        var utf8 = Encoding.UTF8.GetBytes(value);
        Status.Check(setter(handle, utf8, (nuint) utf8.Length), $"Setting the {what}");
    }

    static void SetList(
        IntPtr handle,
        IReadOnlyList<string>? values,
        ListSetter setter,
        string what)
    {
        if (values is null || values.Count == 0)
        {
            return;
        }

        // Parallel pointer and length arrays, pinned only for the call. The native side copies
        // each string into an owned String, so nothing outlives this frame.
        var buffers = new byte[values.Count][];
        var handles = new GCHandle[values.Count];
        var pointers = new IntPtr[values.Count];
        var lengths = new nuint[values.Count];

        try
        {
            for (var index = 0; index < values.Count; index++)
            {
                buffers[index] = Encoding.UTF8.GetBytes(values[index]);
                handles[index] = GCHandle.Alloc(buffers[index], GCHandleType.Pinned);
                pointers[index] = handles[index].AddrOfPinnedObject();
                lengths[index] = (nuint) buffers[index].Length;
            }

            Status.Check(
                setter(handle, pointers, lengths, (nuint) values.Count),
                $"Setting the {what}");
        }
        finally
        {
            foreach (var pinned in handles)
            {
                if (pinned.IsAllocated)
                {
                    pinned.Free();
                }
            }
        }
    }

    internal static NativeDateTime ToNative(DateTimeOffset value) =>
        new()
        {
            Year = (ushort) value.Year,
            Month = (byte) value.Month,
            Day = (byte) value.Day,
            Hour = (byte) value.Hour,
            Minute = (byte) value.Minute,
            Second = (byte) value.Second,
            HasTime = 1,
            HasUtcOffset = 1,
            UtcOffsetHour = (sbyte) value.Offset.Hours,
            UtcOffsetMinute = (byte) Math.Abs(value.Offset.Minutes)
        };
}
