namespace Krilla;

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