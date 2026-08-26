namespace Krilla;

/// <summary>
/// An entry in the document outline, shown in a viewer's bookmark pane.
/// </summary>
/// <remarks>
/// <see cref="PageIndex"/> may name a page that has not been created yet: forward references
/// are resolved when the document is finished.
/// </remarks>
public sealed class OutlineItem(string title, int pageIndex, Point target = default)
{
    /// <summary>
    /// The text shown in the bookmark pane.
    /// </summary>
    public string Title { get; } = title;

    /// <summary>
    /// The zero-based page this entry jumps to.
    /// </summary>
    public int PageIndex { get; } = pageIndex;

    /// <summary>
    /// The position on the page this entry jumps to.
    /// </summary>
    public Point Target { get; } = target;

    /// <summary>
    /// Whether the entry starts expanded.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Nested entries.
    /// </summary>
    public IList<OutlineItem> Children { get; } = [];

    /// <summary>
    /// Adds a nested entry and returns it, so a tree can be built in one expression.
    /// </summary>
    public OutlineItem Add(OutlineItem child)
    {
        Children.Add(child);
        return child;
    }

    /// <inheritdoc cref="Add(OutlineItem)" />
    public OutlineItem Add(string title, int pageIndex, Point target = default) =>
        Add(new(title, pageIndex, target));

    /// <summary>
    /// Builds the native node, recursively.
    /// </summary>
    internal IntPtr Build()
    {
        if (PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PageIndex),
                PageIndex,
                "A page index cannot be negative.");
        }

        var utf8 = Encoding.UTF8.GetBytes(Title);

        Status.Check(
            KrillaNative.krilla_outline_node_new(
                utf8,
                (nuint) utf8.Length,
                (uint) PageIndex,
                Target.ToNative(),
                out var handle),
            "Creating an outline entry");

        try
        {
            if (IsOpen)
            {
                Status.Check(
                    KrillaNative.krilla_outline_node_set_open(handle, true),
                    "Setting an outline entry open");
            }

            foreach (var child in Children)
            {
                var childHandle = child.Build();

                try
                {
                    Status.Check(
                        KrillaNative.krilla_outline_node_push_child(handle, childHandle),
                        "Nesting an outline entry");
                }
                finally
                {
                    // The push consumed the child's contents; the handle itself is still ours.
                    KrillaNative.krilla_outline_node_free(childHandle);
                }
            }

            return handle;
        }
        catch
        {
            KrillaNative.krilla_outline_node_free(handle);
            throw;
        }
    }
}
