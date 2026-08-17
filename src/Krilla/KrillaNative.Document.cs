static partial class KrillaNative
{
    [LibraryImport(library)]
    internal static partial int krilla_document_new(out IntPtr document);

    [LibraryImport(library)]
    internal static partial int krilla_document_free(IntPtr document);

    [LibraryImport(library)]
    internal static partial int krilla_document_start_page(
        IntPtr document,
        NativePageSettings settings,
        out ulong token);

    [LibraryImport(library)]
    internal static partial int krilla_document_close_page(IntPtr document, ulong token);

    [LibraryImport(library)]
    internal static partial int krilla_document_open_page(IntPtr document, out ulong token);

    [LibraryImport(library)]
    internal static partial int krilla_document_finish(
        IntPtr document,
        out IntPtr ptr,
        out nuint len,
        out IntPtr error);

    [LibraryImport(library)]
    internal static partial int krilla_error_free(IntPtr error);

    [LibraryImport(library)]
    internal static partial int krilla_error_message(
        IntPtr error,
        out IntPtr ptr,
        out nuint len);
}
