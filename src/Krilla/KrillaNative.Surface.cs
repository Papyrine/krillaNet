static partial class KrillaNative
{
    [LibraryImport(library)]
    internal static partial int krilla_path_builder_new(out IntPtr builder);

    [LibraryImport(library)]
    internal static partial int krilla_path_builder_free(IntPtr builder);

    [LibraryImport(library)]
    internal static partial int krilla_path_builder_move_to(IntPtr builder, float x, float y);

    [LibraryImport(library)]
    internal static partial int krilla_path_builder_line_to(IntPtr builder, float x, float y);

    [LibraryImport(library)]
    internal static partial int krilla_path_builder_quad_to(
        IntPtr builder,
        float x1,
        float y1,
        float x,
        float y);

    [LibraryImport(library)]
    internal static partial int krilla_path_builder_cubic_to(
        IntPtr builder,
        float x1,
        float y1,
        float x2,
        float y2,
        float x,
        float y);

    [LibraryImport(library)]
    internal static partial int krilla_path_builder_close(IntPtr builder);

    [LibraryImport(library)]
    internal static partial int krilla_path_builder_push_rect(IntPtr builder, NativeRect rect);

    [LibraryImport(library)]
    internal static partial int krilla_path_builder_finish(IntPtr builder, out IntPtr path);

    [LibraryImport(library)]
    internal static partial int krilla_path_free(IntPtr path);

    [LibraryImport(library)]
    internal static partial int krilla_paint_new_color(NativeColor color, out IntPtr paint);

    [LibraryImport(library)]
    internal static partial int krilla_paint_new_linear_gradient(
        float x1,
        float y1,
        float x2,
        float y2,
        NativeTransform transform,
        int spread,
        [MarshalAs(UnmanagedType.U1)] bool antiAlias,
        ReadOnlySpan<NativeStop> stops,
        nuint stopCount,
        out IntPtr paint);

    [LibraryImport(library)]
    internal static partial int krilla_paint_new_radial_gradient(
        float fx,
        float fy,
        float fr,
        float cx,
        float cy,
        float cr,
        NativeTransform transform,
        int spread,
        [MarshalAs(UnmanagedType.U1)] bool antiAlias,
        ReadOnlySpan<NativeStop> stops,
        nuint stopCount,
        out IntPtr paint);

    [LibraryImport(library)]
    internal static partial int krilla_paint_new_sweep_gradient(
        float cx,
        float cy,
        float startAngle,
        float endAngle,
        NativeTransform transform,
        int spread,
        [MarshalAs(UnmanagedType.U1)] bool antiAlias,
        ReadOnlySpan<NativeStop> stops,
        nuint stopCount,
        out IntPtr paint);

    [LibraryImport(library)]
    internal static partial int krilla_paint_free(IntPtr paint);

    [LibraryImport(library)]
    internal static partial int krilla_surface_set_fill(
        IntPtr document,
        ulong token,
        IntPtr paint,
        NativeFill fill);

    [LibraryImport(library)]
    internal static partial int krilla_surface_set_stroke(
        IntPtr document,
        ulong token,
        IntPtr paint,
        NativeStroke stroke);

    [LibraryImport(library)]
    internal static partial int krilla_surface_draw_path(
        IntPtr document,
        ulong token,
        IntPtr path);

    [LibraryImport(library)]
    internal static partial int krilla_surface_push_transform(
        IntPtr document,
        ulong token,
        NativeTransform transform);

    [LibraryImport(library)]
    internal static partial int krilla_surface_push_clip_path(
        IntPtr document,
        ulong token,
        IntPtr path,
        int rule);

    [LibraryImport(library)]
    internal static partial int krilla_surface_push_opacity(
        IntPtr document,
        ulong token,
        float opacity);

    [LibraryImport(library)]
    internal static partial int krilla_surface_push_isolated(IntPtr document, ulong token);

    [LibraryImport(library)]
    internal static partial int krilla_surface_pop(IntPtr document, ulong token);

    [LibraryImport(library)]
    internal static partial int krilla_surface_current_transform(
        IntPtr document,
        ulong token,
        out NativeTransform transform);

    [LibraryImport(library)]
    internal static partial int krilla_surface_set_location(
        IntPtr document,
        ulong token,
        ulong location);
}
