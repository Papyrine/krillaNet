static partial class KrillaNative
{
    /// <summary>
    /// Whether the loaded native library was built with SVG support.
    /// </summary>
    /// <remarks>
    /// The one optional part of the ABI, so the one place a managed call can find its entry
    /// point missing. A published package never can — managed and native ship together — but a
    /// native built by hand with <c>--no-default-features</c> does, and probing turns an
    /// <see cref="EntryPointNotFoundException"/> from somewhere inside the P/Invoke layer into
    /// a sentence naming the cause. Cached, since the answer cannot change while the library is
    /// loaded.
    /// </remarks>
    internal static bool SvgSupported => svgSupported ??= QuerySvgSupported();

    static bool? svgSupported;

    static bool QuerySvgSupported()
    {
        try
        {
            return krilla_svg_supported(out var supported) == Status.Ok && supported != 0;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [LibraryImport(library)]
    internal static partial int krilla_svg_supported(out uint supported);

    [LibraryImport(library)]
    internal static partial int krilla_svg_options_new(out IntPtr options);

    [LibraryImport(library)]
    internal static partial int krilla_svg_options_add_font(
        IntPtr options,
        ReadOnlySpan<byte> data,
        nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_svg_options_set_default_family(
        IntPtr options,
        ReadOnlySpan<byte> family,
        nuint length);

    [LibraryImport(library)]
    internal static partial int krilla_svg_options_free(IntPtr options);

    [LibraryImport(library)]
    internal static partial int krilla_svg_new(
        ReadOnlySpan<byte> data,
        nuint length,
        IntPtr options,
        out IntPtr svg);

    [LibraryImport(library)]
    internal static partial int krilla_svg_size(
        IntPtr svg,
        out float width,
        out float height);

    [LibraryImport(library)]
    internal static partial int krilla_svg_free(IntPtr svg);

    [LibraryImport(library)]
    internal static partial int krilla_surface_draw_svg(
        IntPtr document,
        ulong token,
        IntPtr svg,
        NativeSize size,
        [MarshalAs(UnmanagedType.U1)] bool embedText,
        float filterScale);
}
