namespace Krilla;

/// <summary>
/// A node in the document's logical structure tree.
/// </summary>
/// <remarks>
/// Build a tree of these, attach content with <see cref="Add(TagIdentifier)"/>, then apply it
/// with <see cref="KrillaDocument.SetTagTree"/>. A tagged structure is what makes a PDF
/// navigable by a screen reader, and is required by PDF/UA and by PDF/A level A.
/// </remarks>
public sealed class Tag :
    IDisposable
{
    IntPtr handle;

    // Children and content in ONE list, because their ORDER relative to each other is the reading
    // order: a paragraph holding a word in bold is text, then the bold, then more text. Keeping
    // two lists — or pushing content eagerly and children at the end — puts every child after
    // everything its parent said itself, which is right only for a parent that says nothing.
    readonly List<Node> nodes = [];

    /// <summary>A child tag, or a span of content, in the order it was added.</summary>
    readonly record struct Node(Tag? Child, TagIdentifier Identifier);

    Tag(IntPtr handle) =>
        this.handle = handle;

    internal IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
            return handle;
        }
    }

    /// <summary>
    /// Creates a tag of the given structural role.
    /// </summary>
    public static Tag Create(TagKind kind)
    {
        KrillaNative.EnsureLoaded();
        Status.Check(KrillaNative.krilla_tag_new((int) kind, out var handle), "Creating a tag");
        return new(handle);
    }

    /// <summary>
    /// Creates a heading at <paramref name="level"/>, 1 being the most significant.
    /// </summary>
    /// <remarks>
    /// The title is required by PDF/UA.
    /// </remarks>
    public static Tag Heading(int level, string? title = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, ushort.MaxValue);

        KrillaNative.EnsureLoaded();
        var utf8 = Utf8(title);

        Status.Check(
            KrillaNative.krilla_tag_new_heading((ushort) level, utf8, Length(title, utf8), out var handle),
            "Creating a heading tag");
        return new(handle);
    }

    /// <summary>
    /// Creates a list with the given item markers.
    /// </summary>
    public static Tag List(ListNumbering numbering)
    {
        KrillaNative.EnsureLoaded();
        Status.Check(
            KrillaNative.krilla_tag_new_list((int) numbering, out var handle),
            "Creating a list tag");
        return new(handle);
    }

    /// <summary>
    /// Creates a table header cell describing its row, column, or both.
    /// </summary>
    public static Tag TableHeader(TableHeaderScope scope)
    {
        KrillaNative.EnsureLoaded();
        Status.Check(
            KrillaNative.krilla_tag_new_table_header((int) scope, out var handle),
            "Creating a table header tag");
        return new(handle);
    }

    /// <summary>
    /// Creates a figure with alternative text.
    /// </summary>
    /// <remarks>
    /// The alt text is what a screen reader announces in place of the image, and PDF/UA
    /// requires it.
    /// </remarks>
    public static Tag Figure(string? altText)
    {
        KrillaNative.EnsureLoaded();
        var utf8 = Utf8(altText);

        Status.Check(
            KrillaNative.krilla_tag_new_figure(utf8, Length(altText, utf8), out var handle),
            "Creating a figure tag");
        return new(handle);
    }

    /// <summary>
    /// Creates a formula with alternative text.
    /// </summary>
    public static Tag Formula(string? altText)
    {
        KrillaNative.EnsureLoaded();
        var utf8 = Utf8(altText);

        Status.Check(
            KrillaNative.krilla_tag_new_formula(utf8, Length(altText, utf8), out var handle),
            "Creating a formula tag");
        return new(handle);
    }

    /// <summary>
    /// The natural language of this subtree, as a BCP 47 tag.
    /// </summary>
    public Tag WithLanguage(string? language) =>
        SetString(KrillaNative.krilla_tag_set_lang, language, "language");

    /// <summary>
    /// What a screen reader announces in place of the content.
    /// </summary>
    public Tag WithAltText(string? altText) =>
        SetString(KrillaNative.krilla_tag_set_alt_text, altText, "alt text");

    /// <summary>
    /// What the content actually says, when its glyphs do not spell it — a ligature, or a
    /// dropped capital.
    /// </summary>
    public Tag WithActualText(string? actualText) =>
        SetString(KrillaNative.krilla_tag_set_actual_text, actualText, "actual text");

    /// <summary>
    /// The expansion of an abbreviation or acronym.
    /// </summary>
    public Tag WithExpanded(string? expanded) =>
        SetString(KrillaNative.krilla_tag_set_expanded, expanded, "expansion");

    /// <summary>
    /// An identifier other tags can reference, used by <see cref="WithHeaders"/>.
    /// </summary>
    public Tag WithId(string? id) =>
        SetString(KrillaNative.krilla_tag_set_id, id, "id");

    /// <summary>
    /// A prose summary of a table's structure.
    /// </summary>
    /// <exception cref="KrillaException">This tag is not a table.</exception>
    public Tag WithSummary(string? summary) =>
        SetString(KrillaNative.krilla_tag_set_summary, summary, "summary");

    /// <summary>
    /// How many rows a table cell spans.
    /// </summary>
    /// <exception cref="KrillaException">This tag is not a table cell.</exception>
    public Tag WithRowSpan(int span)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(span, 1);
        Status.Check(KrillaNative.krilla_tag_set_row_span(Handle, (uint) span), "Setting a row span");
        return this;
    }

    /// <summary>
    /// How many columns a table cell spans.
    /// </summary>
    /// <exception cref="KrillaException">This tag is not a table cell.</exception>
    public Tag WithColumnSpan(int span)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(span, 1);
        Status.Check(KrillaNative.krilla_tag_set_col_span(Handle, (uint) span), "Setting a column span");
        return this;
    }

    /// <summary>
    /// The ids of the header cells that describe this cell.
    /// </summary>
    /// <exception cref="KrillaException">This tag is not a table cell.</exception>
    public Tag WithHeaders(params IReadOnlyList<string> headers)
    {
        if (headers.Count == 0)
        {
            return this;
        }

        var buffers = new byte[headers.Count][];
        var pinned = new GCHandle[headers.Count];
        var pointers = new IntPtr[headers.Count];
        var lengths = new nuint[headers.Count];

        try
        {
            for (var index = 0; index < headers.Count; index++)
            {
                buffers[index] = Encoding.UTF8.GetBytes(headers[index]);
                pinned[index] = GCHandle.Alloc(buffers[index], GCHandleType.Pinned);
                pointers[index] = pinned[index].AddrOfPinnedObject();
                lengths[index] = (nuint) buffers[index].Length;
            }

            Status.Check(
                KrillaNative.krilla_tag_set_headers(Handle, pointers, lengths, (nuint) headers.Count),
                "Setting cell headers");
        }
        finally
        {
            foreach (var entry in pinned)
            {
                if (entry.IsAllocated)
                {
                    entry.Free();
                }
            }
        }

        return this;
    }

    /// <summary>
    /// Places a span of tagged content, or a tagged annotation, under this tag.
    /// </summary>
    /// <remarks>
    /// Recorded here and applied when the tree is attached to a document, so that it keeps its
    /// place among the children added around it.
    /// </remarks>
    public Tag Add(TagIdentifier identifier)
    {
        nodes.Add(new(null, identifier));
        return this;
    }

    /// <summary>
    /// Nests a tag under this one and returns the child, so a tree can be built in one
    /// expression.
    /// </summary>
    /// <remarks>
    /// Nesting is recorded here and applied when the tree is attached to a document. It has to
    /// be: the native push consumes the child, so pushing eagerly would freeze each tag the
    /// moment it was placed, and a tree could only ever be built leaves-first.
    /// </remarks>
    public Tag Add(Tag child)
    {
        nodes.Add(new(child, default));
        return child;
    }

    /// <summary>
    /// Applies the recorded content and nesting to the native handles, depth-first and in the
    /// order they were added.
    /// </summary>
    internal void Flatten()
    {
        foreach (var (child, identifier) in nodes)
        {
            if (child is null)
            {
                Status.Check(
                    KrillaNative.krilla_tag_push_identifier(Handle, identifier.Slot),
                    "Adding content to a tag");
                continue;
            }

            child.Flatten();

            Status.Check(
                KrillaNative.krilla_tag_push_child(Handle, child.Handle),
                "Nesting a tag");
        }
    }

    /// <inheritdoc cref="Add(Tag)" />
    public Tag Add(TagKind kind) =>
        Add(Create(kind));

    // A named delegate rather than Func<>, because the P/Invoke takes a ReadOnlySpan and a
    // ref struct cannot appear in a generic type argument.
    delegate int StringSetter(IntPtr handle, ReadOnlySpan<byte> text, nuint length);

    Tag SetString(
        StringSetter setter,
        string? value,
        string what)
    {
        var utf8 = Utf8(value);
        Status.Check(setter(Handle, utf8, Length(value, utf8)), $"Setting the {what}");
        return this;
    }

    internal static byte[] Utf8(string? value)
    {
        if (value is null)
        {
            return [];
        }

        return Encoding.UTF8.GetBytes(value);
    }

    internal static nuint Length(string? value, byte[] utf8)
    {
        if (value is null)
        {
            return 0;
        }

        return (nuint) utf8.Length;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (child, _) in nodes)
        {
            child?.Dispose();
        }

        nodes.Clear();

        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_tag_free(handle);
        handle = IntPtr.Zero;
    }
}