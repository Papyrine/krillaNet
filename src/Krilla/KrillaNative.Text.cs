static partial class KrillaNative
{
    [LibraryImport(library)]
    internal static partial int krilla_font_new(
        ReadOnlySpan<byte> data,
        nuint length,
        uint index,
        out IntPtr font);

    [LibraryImport(library)]
    internal static partial int krilla_font_free(IntPtr font);

    [LibraryImport(library)]
    internal static partial int krilla_font_units_per_em(IntPtr font, out float unitsPerEm);

    [LibraryImport(library)]
    internal static partial int krilla_font_shape(
        IntPtr font,
        ReadOnlySpan<byte> text,
        nuint textLength,
        int textDirection,
        out IntPtr glyphs,
        out nuint glyphCount);

    [LibraryImport(library)]
    internal static partial int krilla_glyphs_free(IntPtr glyphs, nuint glyphCount);

    [LibraryImport(library)]
    internal static partial int krilla_surface_draw_text(
        IntPtr document,
        ulong token,
        NativePoint start,
        IntPtr font,
        float fontSize,
        ReadOnlySpan<byte> text,
        nuint textLength,
        [MarshalAs(UnmanagedType.U1)] bool outlined,
        int textDirection);

    [LibraryImport(library)]
    internal static partial int krilla_surface_draw_glyphs(
        IntPtr document,
        ulong token,
        NativePoint start,
        IntPtr font,
        float fontSize,
        ReadOnlySpan<byte> text,
        nuint textLength,
        ReadOnlySpan<NativeGlyph> glyphs,
        nuint glyphCount,
        [MarshalAs(UnmanagedType.U1)] bool outlined);

    [LibraryImport(library)]
    internal static partial int krilla_image_new_encoded(
        int format,
        ReadOnlySpan<byte> data,
        nuint length,
        [MarshalAs(UnmanagedType.U1)] bool interpolate,
        out IntPtr image);

    [LibraryImport(library)]
    internal static partial int krilla_image_new_rgba8(
        ReadOnlySpan<byte> data,
        nuint length,
        uint width,
        uint height,
        out IntPtr image);

    [LibraryImport(library)]
    internal static partial int krilla_image_size(
        IntPtr image,
        out uint width,
        out uint height);

    [LibraryImport(library)]
    internal static partial int krilla_image_free(IntPtr image);

    [LibraryImport(library)]
    internal static partial int krilla_surface_draw_image(
        IntPtr document,
        ulong token,
        IntPtr image,
        NativeSize size);
}
