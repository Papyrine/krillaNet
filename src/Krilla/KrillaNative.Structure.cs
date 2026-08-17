static partial class KrillaNative
{
    // -- Metadata and configuration -------------------------------------------------------

    [LibraryImport(library)]
    internal static partial int krilla_document_new_with(
        NativeDocumentOptions options,
        out IntPtr document);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_new(out IntPtr metadata);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_free(IntPtr metadata);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_set_title(IntPtr metadata, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_set_description(IntPtr metadata, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_set_language(IntPtr metadata, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_set_creator(IntPtr metadata, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_set_producer(IntPtr metadata, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_set_document_id(IntPtr metadata, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_set_authors(
        IntPtr metadata,
        ReadOnlySpan<IntPtr> pointers,
        ReadOnlySpan<nuint> lengths,
        nuint count);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_set_keywords(
        IntPtr metadata,
        ReadOnlySpan<IntPtr> pointers,
        ReadOnlySpan<nuint> lengths,
        nuint count);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_set_creation_date(IntPtr metadata, NativeDateTime date);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_set_text_direction(IntPtr metadata, int direction);

    [LibraryImport(library)]
    internal static partial int krilla_metadata_set_page_layout(IntPtr metadata, int layout);

    [LibraryImport(library)]
    internal static partial int krilla_document_set_metadata(IntPtr document, IntPtr metadata);

    // -- Outline ---------------------------------------------------------------------------

    [LibraryImport(library)]
    internal static partial int krilla_outline_new(out IntPtr outline);

    [LibraryImport(library)]
    internal static partial int krilla_outline_free(IntPtr outline);

    [LibraryImport(library)]
    internal static partial int krilla_outline_node_new(
        ReadOnlySpan<byte> text,
        nuint textLength,
        uint pageIndex,
        NativePoint point,
        out IntPtr node);

    [LibraryImport(library)]
    internal static partial int krilla_outline_node_free(IntPtr node);

    [LibraryImport(library)]
    internal static partial int krilla_outline_node_set_open(
        IntPtr node,
        [MarshalAs(UnmanagedType.U1)] bool open);

    [LibraryImport(library)]
    internal static partial int krilla_outline_node_push_child(IntPtr parent, IntPtr child);

    [LibraryImport(library)]
    internal static partial int krilla_outline_push(IntPtr outline, IntPtr node);

    [LibraryImport(library)]
    internal static partial int krilla_document_set_outline(IntPtr document, IntPtr outline);

    // -- Links and destinations -------------------------------------------------------------

    [LibraryImport(library)]
    internal static partial int krilla_page_add_link(
        IntPtr document,
        ulong token,
        NativeRect rect,
        ReadOnlySpan<byte> uri,
        nuint uriLength,
        uint pageIndex,
        NativePoint point);

    [LibraryImport(library)]
    internal static partial int krilla_page_add_tagged_link(
        IntPtr document,
        ulong token,
        NativeRect rect,
        ReadOnlySpan<byte> uri,
        nuint uriLength,
        uint pageIndex,
        NativePoint point,
        out nuint identifier);

    [LibraryImport(library)]
    internal static partial int krilla_document_register_named_destination(
        IntPtr document,
        ReadOnlySpan<byte> name,
        nuint nameLength,
        uint pageIndex,
        NativePoint point);

    // -- Streams, graphics, masks, patterns ---------------------------------------------------

    [LibraryImport(library)]
    internal static partial int krilla_stream_begin(IntPtr document, ulong token, out ulong streamToken);

    [LibraryImport(library)]
    internal static partial int krilla_stream_finish(IntPtr document, ulong token, out IntPtr stream);

    [LibraryImport(library)]
    internal static partial int krilla_stream_free(IntPtr stream);

    [LibraryImport(library)]
    internal static partial int krilla_graphic_new(
        IntPtr document,
        IntPtr stream,
        [MarshalAs(UnmanagedType.U1)] bool isolated,
        out IntPtr graphic);

    [LibraryImport(library)]
    internal static partial int krilla_graphic_free(IntPtr graphic);

    [LibraryImport(library)]
    internal static partial int krilla_surface_draw_graphic(IntPtr document, ulong token, IntPtr graphic);

    [LibraryImport(library)]
    internal static partial int krilla_surface_push_mask(
        IntPtr document,
        ulong token,
        int kind,
        IntPtr stream);

    [LibraryImport(library)]
    internal static partial int krilla_paint_new_pattern(
        IntPtr document,
        IntPtr stream,
        NativeTransform transform,
        float width,
        float height,
        out IntPtr paint);

    // -- Embedded files and PDF embedding -----------------------------------------------------

    [LibraryImport(library)]
    internal static partial int krilla_document_embed_file(
        IntPtr document,
        ReadOnlySpan<byte> path,
        nuint pathLength,
        ReadOnlySpan<byte> mime,
        nuint mimeLength,
        ReadOnlySpan<byte> description,
        nuint descriptionLength,
        ReadOnlySpan<byte> data,
        nuint dataLength,
        int associationKind,
        NativeDateTime modificationDate,
        [MarshalAs(UnmanagedType.U1)] bool hasModificationDate,
        int compress);

    [LibraryImport(library)]
    internal static partial int krilla_pdf_new(ReadOnlySpan<byte> data, nuint length, out IntPtr pdf);

    [LibraryImport(library)]
    internal static partial int krilla_pdf_free(IntPtr pdf);

    [LibraryImport(library)]
    internal static partial int krilla_pdf_page_count(IntPtr pdf, out nuint count);

    [LibraryImport(library)]
    internal static partial int krilla_surface_draw_pdf_page(
        IntPtr document,
        ulong token,
        IntPtr pdf,
        NativeSize size,
        nuint pageIndex);

    [LibraryImport(library)]
    internal static partial int krilla_document_embed_pdf_pages(
        IntPtr document,
        IntPtr pdf,
        ReadOnlySpan<nuint> indices,
        nuint count);

    // -- Tagged PDF ---------------------------------------------------------------------------

    [LibraryImport(library)]
    internal static partial int krilla_tag_new(int kind, out IntPtr tag);

    [LibraryImport(library)]
    internal static partial int krilla_tag_new_heading(
        ushort level,
        ReadOnlySpan<byte> title,
        nuint titleLength,
        out IntPtr tag);

    [LibraryImport(library)]
    internal static partial int krilla_tag_new_list(int numbering, out IntPtr tag);

    [LibraryImport(library)]
    internal static partial int krilla_tag_new_table_header(int scope, out IntPtr tag);

    [LibraryImport(library)]
    internal static partial int krilla_tag_new_figure(ReadOnlySpan<byte> alt, nuint altLength, out IntPtr tag);

    [LibraryImport(library)]
    internal static partial int krilla_tag_new_formula(ReadOnlySpan<byte> alt, nuint altLength, out IntPtr tag);

    [LibraryImport(library)]
    internal static partial int krilla_tag_free(IntPtr tag);

    [LibraryImport(library)]
    internal static partial int krilla_tag_set_lang(IntPtr tag, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_tag_set_alt_text(IntPtr tag, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_tag_set_actual_text(IntPtr tag, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_tag_set_expanded(IntPtr tag, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_tag_set_id(IntPtr tag, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_tag_set_row_span(IntPtr tag, uint span);

    [LibraryImport(library)]
    internal static partial int krilla_tag_set_col_span(IntPtr tag, uint span);

    [LibraryImport(library)]
    internal static partial int krilla_tag_set_headers(
        IntPtr tag,
        ReadOnlySpan<IntPtr> pointers,
        ReadOnlySpan<nuint> lengths,
        nuint count);

    [LibraryImport(library)]
    internal static partial int krilla_tag_set_summary(IntPtr tag, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_tag_push_identifier(IntPtr tag, nuint identifier);

    [LibraryImport(library)]
    internal static partial int krilla_tag_push_child(IntPtr parent, IntPtr child);

    [LibraryImport(library)]
    internal static partial int krilla_tag_tree_new(out IntPtr tree);

    [LibraryImport(library)]
    internal static partial int krilla_tag_tree_free(IntPtr tree);

    [LibraryImport(library)]
    internal static partial int krilla_tag_tree_set_lang(IntPtr tree, ReadOnlySpan<byte> text, nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_tag_tree_push(IntPtr tree, IntPtr tag);

    [LibraryImport(library)]
    internal static partial int krilla_document_set_tag_tree(IntPtr document, IntPtr tree);

    [LibraryImport(library)]
    internal static partial int krilla_surface_start_tagged(
        IntPtr document,
        ulong token,
        int kind,
        int artifactType,
        NativeRect bbox,
        [MarshalAs(UnmanagedType.U1)] bool hasBbox,
        ReadOnlySpan<byte> lang,
        nuint langLength,
        ReadOnlySpan<byte> alt,
        nuint altLength,
        ReadOnlySpan<byte> expanded,
        nuint expandedLength,
        ReadOnlySpan<byte> actual,
        nuint actualLength,
        out nuint identifier);

    [LibraryImport(library)]
    internal static partial int krilla_surface_end_tagged(IntPtr document, ulong token);
}
