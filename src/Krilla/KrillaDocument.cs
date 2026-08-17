namespace Krilla;

/// <summary>
/// A PDF document under construction. The entry point to the library.
/// </summary>
/// <remarks>
/// <para>
/// Pages are added one at a time: krilla keeps global serialization state, so only one page
/// can be open at a time and a second <see cref="StartPage(Size)"/> throws until the first is
/// closed. <see cref="Page"/> implements <see cref="IDisposable"/>, so a <c>using</c>
/// statement handles that.
/// </para>
/// <para>
/// <see cref="Finish"/> serializes and consumes the document; nothing can be added afterwards.
/// </para>
/// <para>
/// Not thread safe. A document and everything reachable from it must be used from one thread
/// at a time. Separate documents are genuinely independent and can be built in parallel.
/// </para>
/// </remarks>
public sealed class KrillaDocument :
    IDisposable
{
    IntPtr handle;
    Page? openPage;
    readonly List<IDisposable> tracked = [];

    /// <summary>
    /// Creates an empty document with default settings: PDF 1.7, compressed, no conformance
    /// profile.
    /// </summary>
    public KrillaDocument()
    {
        KrillaNative.EnsureLoaded();
        Status.Check(KrillaNative.krilla_document_new(out handle), "Creating a document");
    }

    /// <summary>
    /// Creates an empty document with explicit settings.
    /// </summary>
    /// <exception cref="KrillaException">
    /// The combination is invalid — for instance a conformance level needing a newer PDF
    /// version than the one requested.
    /// </exception>
    public KrillaDocument(DocumentOptions options)
    {
        KrillaNative.EnsureLoaded();
        Status.Check(
            KrillaNative.krilla_document_new_with(options.ToNative(), out handle),
            "Creating a document");
    }

    internal IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
            return handle;
        }
    }

    /// <summary>
    /// Keeps a resource alive for the document's lifetime.
    /// </summary>
    /// <remarks>
    /// Used for paints created implicitly by convenience overloads, which the caller never
    /// sees and therefore cannot dispose.
    /// </remarks>
    internal void Track(IDisposable resource) =>
        tracked.Add(resource);

    /// <summary>
    /// Opens a page of the given size, in points.
    /// </summary>
    /// <exception cref="KrillaException">
    /// Another page is already open, the document has been finished, or the size is not
    /// strictly positive.
    /// </exception>
    public Page StartPage(Size size) =>
        StartPage(new PageSettings(size));

    /// <inheritdoc cref="StartPage(Size)" />
    public Page StartPage(float width, float height) =>
        StartPage(new Size(width, height));

    /// <inheritdoc cref="StartPage(Size)" />
    public Page StartPage(PageSettings settings)
    {
        Status.Check(
            KrillaNative.krilla_document_start_page(Handle, settings.ToNative(), out var token),
            "Starting a page");

        var page = new Page(this, token);
        openPage = page;
        return page;
    }

    internal void ClosePage(Page page, ulong token)
    {
        if (!ReferenceEquals(openPage, page))
        {
            return;
        }

        openPage = null;
        Status.Check(KrillaNative.krilla_document_close_page(Handle, token), "Closing a page");
    }

    /// <summary>
    /// Applies document metadata.
    /// </summary>
    /// <remarks>
    /// Cannot be called while a page is open.
    /// </remarks>
    public KrillaDocument SetMetadata(DocumentMetadata metadata)
    {
        var built = metadata.Build();

        try
        {
            Status.Check(
                KrillaNative.krilla_document_set_metadata(Handle, built),
                "Setting the metadata");
        }
        finally
        {
            KrillaNative.krilla_metadata_free(built);
        }

        return this;
    }

    /// <summary>
    /// Sets the document outline, shown in a viewer's bookmark pane.
    /// </summary>
    /// <remarks>
    /// Cannot be called while a page is open. Entries may reference pages that do not exist
    /// yet.
    /// </remarks>
    public KrillaDocument SetOutline(params IReadOnlyList<OutlineItem> items)
    {
        Status.Check(KrillaNative.krilla_outline_new(out var outline), "Creating an outline");

        try
        {
            foreach (var item in items)
            {
                var node = item.Build();

                try
                {
                    Status.Check(
                        KrillaNative.krilla_outline_push(outline, node),
                        "Adding an outline entry");
                }
                finally
                {
                    KrillaNative.krilla_outline_node_free(node);
                }
            }

            Status.Check(
                KrillaNative.krilla_document_set_outline(Handle, outline),
                "Setting the outline");
        }
        finally
        {
            KrillaNative.krilla_outline_free(outline);
        }

        return this;
    }

    /// <summary>
    /// Applies the logical structure tree.
    /// </summary>
    /// <remarks>
    /// Cannot be called while a page is open — annotation identifiers do not resolve until
    /// their page closes. Requires <see cref="DocumentOptions.EnableTagging"/>.
    /// </remarks>
    public KrillaDocument SetTagTree(TagTree tree)
    {
        // The structure was recorded on the managed side so tags stayed mutable while it was
        // being built; this is where it is actually pushed down.
        tree.Flatten();

        Status.Check(
            KrillaNative.krilla_document_set_tag_tree(Handle, tree.Handle),
            "Setting the tag tree");
        return this;
    }

    /// <summary>
    /// Registers a named destination other documents can link to.
    /// </summary>
    public KrillaDocument RegisterDestination(string name, int pageIndex, Point target = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        var utf8 = Encoding.UTF8.GetBytes(name);

        Status.Check(
            KrillaNative.krilla_document_register_named_destination(
                Handle,
                utf8,
                (nuint) utf8.Length,
                (uint) pageIndex,
                target.ToNative()),
            "Registering a named destination");
        return this;
    }

    /// <summary>
    /// Attaches a file to the document.
    /// </summary>
    /// <param name="path">The name the attachment appears under. Must be unique.</param>
    /// <param name="data">The file contents.</param>
    /// <param name="mimeType">The media type, such as <c>text/plain</c>.</param>
    /// <param name="description">A human-readable description.</param>
    /// <param name="association">How the attachment relates to the document.</param>
    /// <param name="modified">When the attachment was last changed.</param>
    /// <remarks>
    /// PDF/A-3 and PDF/A-4f require <paramref name="association"/> to be meaningful rather
    /// than left at its default.
    /// </remarks>
    public KrillaDocument EmbedFile(
        string path,
        ReadOnlySpan<byte> data,
        string? mimeType = null,
        string? description = null,
        FileAssociation association = FileAssociation.Unspecified,
        DateTimeOffset? modified = null)
    {
        var pathUtf8 = Encoding.UTF8.GetBytes(path);
        var mimeUtf8 = Tag.Utf8(mimeType);
        var descriptionUtf8 = Tag.Utf8(description);

        Status.Check(
            KrillaNative.krilla_document_embed_file(
                Handle,
                pathUtf8,
                (nuint) pathUtf8.Length,
                mimeUtf8,
                Tag.Length(mimeType, mimeUtf8),
                descriptionUtf8,
                Tag.Length(description, descriptionUtf8),
                data,
                (nuint) data.Length,
                (int) association,
                modified is { } value ? DocumentMetadata.ToNative(value) : default,
                modified is not null,
                -1),
            "Embedding a file");
        return this;
    }

    /// <summary>
    /// Appends pages from an existing PDF to this document.
    /// </summary>
    /// <remarks>
    /// To paint a page into an area of a page being composed instead, use
    /// <see cref="Surface.DrawPdfPage"/>.
    /// </remarks>
    public KrillaDocument EmbedPdfPages(PdfSource source, params IReadOnlyList<int> pageIndices)
    {
        var indices = new nuint[pageIndices.Count];

        for (var index = 0; index < pageIndices.Count; index++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndices[index]);
            indices[index] = (nuint) pageIndices[index];
        }

        Status.Check(
            KrillaNative.krilla_document_embed_pdf_pages(
                Handle,
                source.Handle,
                indices,
                (nuint) indices.Length),
            "Embedding PDF pages");
        return this;
    }

    /// <summary>
    /// Serializes the document to PDF bytes.
    /// </summary>
    /// <remarks>
    /// Consumes the document: nothing can be added afterwards, though
    /// <see cref="Dispose"/> must still be called.
    /// </remarks>
    /// <exception cref="KrillaException">
    /// A page is still open, the document has already been finished, or krilla rejected the
    /// document — most often a validation failure against a PDF/A or PDF/UA profile.
    /// </exception>
    public byte[] Finish()
    {
        var status = KrillaNative.krilla_document_finish(
            Handle,
            out var ptr,
            out var len,
            out var error);

        if (status == Status.KrillaError)
        {
            throw BuildError(error);
        }

        Status.Check(status, "Finishing the document");
        return KrillaNative.TakeBuffer(ptr, len);
    }

    /// <summary>
    /// Serializes the document and writes it to a file.
    /// </summary>
    public void Save(string path) =>
        File.WriteAllBytes(path, Finish());

    static KrillaException BuildError(IntPtr error)
    {
        if (error == IntPtr.Zero)
        {
            return new("The document could not be finished.");
        }

        try
        {
            if (KrillaNative.krilla_error_message(error, out var ptr, out var len) == Status.Ok &&
                ptr != IntPtr.Zero)
            {
                var message = Encoding.UTF8.GetString(KrillaNative.TakeBuffer(ptr, len));
                return new($"The document could not be finished: {message}");
            }

            return new("The document could not be finished.");
        }
        finally
        {
            KrillaNative.krilla_error_free(error);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var resource in tracked)
        {
            resource.Dispose();
        }

        tracked.Clear();

        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_document_free(handle);
        handle = IntPtr.Zero;
    }
}

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

/// <summary>
/// Page geometry and the optional PDF boundary boxes.
/// </summary>
/// <remarks>
/// Sizes are in points: 72 to the inch, so A4 is 595 x 842 and US Letter is 612 x 792.
/// </remarks>
public sealed class PageSettings
{
    /// <summary>
    /// Creates settings for a page of the given size.
    /// </summary>
    public PageSettings(Size size) =>
        Size = size;

    /// <summary>
    /// A4, 595 x 842 points.
    /// </summary>
    public static PageSettings A4 => new(new(595f, 842f));

    /// <summary>
    /// US Letter, 612 x 792 points.
    /// </summary>
    public static PageSettings Letter => new(new(612f, 792f));

    /// <summary>
    /// The size of the drawing surface.
    /// </summary>
    public Size Size { get; }

    /// <summary>
    /// The visible area. Defaults to the whole surface.
    /// </summary>
    public Rectangle? MediaBox { get; set; }

    /// <summary>
    /// The area to which the page is clipped when displayed or printed.
    /// </summary>
    public Rectangle? CropBox { get; set; }

    /// <summary>
    /// The area including bleed, for production printing.
    /// </summary>
    public Rectangle? BleedBox { get; set; }

    /// <summary>
    /// The intended finished size after trimming.
    /// </summary>
    public Rectangle? TrimBox { get; set; }

    /// <summary>
    /// The extent of meaningful content.
    /// </summary>
    public Rectangle? ArtBox { get; set; }

    internal NativePageSettings ToNative()
    {
        uint present = 0;

        if (MediaBox is not null)
        {
            present |= 1 << 0;
        }

        if (CropBox is not null)
        {
            present |= 1 << 1;
        }

        if (BleedBox is not null)
        {
            present |= 1 << 2;
        }

        if (TrimBox is not null)
        {
            present |= 1 << 3;
        }

        if (ArtBox is not null)
        {
            present |= 1 << 4;
        }

        return new()
        {
            Width = Size.Width,
            Height = Size.Height,
            MediaBox = (MediaBox ?? default).ToNative(),
            CropBox = (CropBox ?? default).ToNative(),
            BleedBox = (BleedBox ?? default).ToNative(),
            TrimBox = (TrimBox ?? default).ToNative(),
            ArtBox = (ArtBox ?? default).ToNative(),
            Present = present,
            Reserved = 0
        };
    }
}
