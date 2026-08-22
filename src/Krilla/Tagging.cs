namespace Krilla;

/// <summary>
/// The structural role of a tagged element.
/// </summary>
/// <remarks>
/// Headings, lists, table headers, figures and formulas carry a value krilla only accepts at
/// construction, so they are created through <see cref="Tag.Heading"/>,
/// <see cref="Tag.List"/>, <see cref="Tag.TableHeader"/>, <see cref="Tag.Figure"/> and
/// <see cref="Tag.Formula"/> rather than named here.
/// </remarks>
public enum TagKind
{
    /// <summary>A part of a document containing multiple articles or sections.</summary>
    Part = 0,

    /// <summary>A largely self-contained article.</summary>
    Article = 1,

    /// <summary>A section.</summary>
    Section = 2,

    /// <summary>A generic grouping with no stronger meaning.</summary>
    Div = 3,

    /// <summary>A paragraph-level quotation.</summary>
    BlockQuote = 4,

    /// <summary>A caption for a figure or table.</summary>
    Caption = 5,

    /// <summary>A table of contents.</summary>
    TableOfContents = 6,

    /// <summary>An entry in a table of contents.</summary>
    TableOfContentsItem = 7,

    /// <summary>An index.</summary>
    Index = 8,

    /// <summary>A paragraph.</summary>
    Paragraph = 9,

    /// <summary>A list item.</summary>
    ListItem = 10,

    /// <summary>A list item's label — its bullet or number.</summary>
    ListLabel = 11,

    /// <summary>A list item's body.</summary>
    ListBody = 12,

    /// <summary>A table.</summary>
    Table = 13,

    /// <summary>A table row.</summary>
    TableRow = 14,

    /// <summary>A table data cell.</summary>
    TableCell = 15,

    /// <summary>A table header row group.</summary>
    TableHead = 16,

    /// <summary>A table body row group.</summary>
    TableBody = 17,

    /// <summary>A table footer row group.</summary>
    TableFoot = 18,

    /// <summary>An inline span of text.</summary>
    Span = 19,

    /// <summary>An inline quotation.</summary>
    InlineQuote = 20,

    /// <summary>A footnote or endnote.</summary>
    Note = 21,

    /// <summary>A reference to elsewhere in the document.</summary>
    Reference = 22,

    /// <summary>A bibliography entry.</summary>
    BibliographyEntry = 23,

    /// <summary>A fragment of computer code.</summary>
    Code = 24,

    /// <summary>A hyperlink.</summary>
    Link = 25,

    /// <summary>An annotation.</summary>
    Annotation = 26,

    /// <summary>A form field.</summary>
    Form = 27,

    /// <summary>Content with no structural role of its own.</summary>
    NonStructural = 28,

    /// <summary>A date or time.</summary>
    DateTime = 29,

    /// <summary>A list of terms and definitions.</summary>
    Terms = 30,

    /// <summary>A title.</summary>
    Title = 31,

    /// <summary>Strongly emphasised text.</summary>
    Strong = 32,

    /// <summary>Emphasised text.</summary>
    Emphasis = 33
}

/// <summary>
/// How a list's items are numbered or bulleted.
/// </summary>
public enum ListNumbering
{
    /// <summary>No marker.</summary>
    None = 0,

    /// <summary>A filled circle.</summary>
    Disc = 1,

    /// <summary>An open circle.</summary>
    Circle = 2,

    /// <summary>A filled square.</summary>
    Square = 3,

    /// <summary>Arabic numerals.</summary>
    Decimal = 4,

    /// <summary>Lower-case Roman numerals.</summary>
    LowerRoman = 5,

    /// <summary>Upper-case Roman numerals.</summary>
    UpperRoman = 6,

    /// <summary>Lower-case letters.</summary>
    LowerAlpha = 7,

    /// <summary>Upper-case letters.</summary>
    UpperAlpha = 8
}

/// <summary>
/// What a table header cell describes.
/// </summary>
public enum TableHeaderScope
{
    /// <summary>The cells in its row.</summary>
    Row = 0,

    /// <summary>The cells in its column.</summary>
    Column = 1,

    /// <summary>Both.</summary>
    Both = 2
}

/// <summary>
/// Content excluded from the logical structure: running heads, page numbers, decorative rules.
/// </summary>
public enum ArtifactKind
{
    /// <summary>A running header.</summary>
    Header = 0,

    /// <summary>A running footer.</summary>
    Footer = 1,

    /// <summary>Background content. Requires a bounding box.</summary>
    Page = 2,

    /// <summary>Anything else.</summary>
    Other = 3
}

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
    readonly List<Tag> children = [];

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
    public Tag Add(TagIdentifier identifier)
    {
        Status.Check(
            KrillaNative.krilla_tag_push_identifier(Handle, identifier.Slot),
            "Adding content to a tag");
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
        children.Add(child);
        return child;
    }

    /// <summary>
    /// Applies the recorded nesting to the native handles, depth-first.
    /// </summary>
    internal void Flatten()
    {
        foreach (var child in children)
        {
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
        foreach (var child in children)
        {
            child.Dispose();
        }

        children.Clear();

        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_tag_free(handle);
        handle = IntPtr.Zero;
    }
}

/// <summary>
/// The document's logical structure tree.
/// </summary>
public sealed class TagTree :
    IDisposable
{
    IntPtr handle;
    readonly List<Tag> roots = [];

    /// <summary>
    /// Creates an empty tree.
    /// </summary>
    public TagTree()
    {
        KrillaNative.EnsureLoaded();
        Status.Check(KrillaNative.krilla_tag_tree_new(out handle), "Creating a tag tree");
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
    /// The document's default natural language, as a BCP 47 tag. Required by PDF/UA.
    /// </summary>
    public TagTree WithLanguage(string? language)
    {
        var utf8 = Tag.Utf8(language);
        Status.Check(
            KrillaNative.krilla_tag_tree_set_lang(Handle, utf8, Tag.Length(language, utf8)),
            "Setting the tree language");
        return this;
    }

    /// <summary>
    /// Adds a top-level tag and returns it.
    /// </summary>
    /// <remarks>
    /// Recorded now and applied when the tree is attached to a document, so tags stay
    /// mutable until then.
    /// </remarks>
    public Tag Add(Tag root)
    {
        roots.Add(root);
        return root;
    }

    /// <summary>
    /// Pushes the recorded structure into the native tree, depth-first.
    /// </summary>
    internal void Flatten()
    {
        foreach (var root in roots)
        {
            root.Flatten();

            Status.Check(
                KrillaNative.krilla_tag_tree_push(Handle, root.Handle),
                "Adding a root tag");
        }
    }

    /// <inheritdoc cref="Add(Tag)" />
    public Tag Add(TagKind kind) =>
        Add(Tag.Create(kind));

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var root in roots)
        {
            root.Dispose();
        }

        roots.Clear();

        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_tag_tree_free(handle);
        handle = IntPtr.Zero;
    }
}
