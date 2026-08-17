/// <summary>
/// P/Invoke surface over the krilla C ABI. The native binaries are built from
/// <c>rust/crates/krilla-capi</c> in this repository and ship inside the package under
/// <c>runtimes/&lt;rid&gt;/native/</c>.
/// </summary>
/// <remarks>
/// <para>
/// Handles are not thread safe and carry no internal locking. A <see cref="KrillaDocument"/>
/// and everything reachable from it must be used from one thread at a time. Unlike PDFium
/// there is no process-wide lock, because there is no global state to protect: separate
/// documents are genuinely independent.
/// </para>
/// <para>
/// Every allocation crossing the boundary is released by the side that made it. The Windows
/// natives link the CRT statically, so the library has its own heap and freeing a Rust
/// allocation from managed code would corrupt it.
/// </para>
/// </remarks>
static partial class KrillaNative
{
    // Bare name, no lib prefix and no extension. .NET probing expands it to krilla_capi.dll,
    // libkrilla_capi.so and libkrilla_capi.dylib, so one constant covers every RID.
    //
    // Kept a compile-time literal, and deliberately paired with no DllImportResolver, so a
    // future NativeAOT DirectPInvoke build stays possible at zero cost.
    const string library = "krilla_capi";

    /// <summary>
    /// ABI revision this assembly was built against.
    /// </summary>
    /// <remarks>
    /// A published package can never mismatch, since managed and native ship together. A stale
    /// <c>src/Krilla/runtimes/</c> folder on a developer machine can, and does — which without
    /// this check surfaces as an <see cref="AccessViolationException"/> carrying no useful
    /// stack.
    /// </remarks>
    internal const uint ExpectedAbiVersion = 1;

    static KrillaNative()
    {
        uint actual;

        try
        {
            actual = krilla_abi_version();
        }
        catch (DllNotFoundException exception)
        {
            throw new KrillaException(
                $"The native krilla library ('{library}') could not be loaded. It ships inside the Krilla package under runtimes/<rid>/native/; if this is a source build, run a cargo build first.",
                exception);
        }

        if (actual != ExpectedAbiVersion)
        {
            throw new KrillaException(
                $"The native krilla library reports ABI version {actual}, but this assembly was built against {ExpectedAbiVersion}. A stale native binary is being loaded; delete src/Krilla/runtimes and rebuild.");
        }
    }

    /// <summary>
    /// Forces the static constructor to run, surfacing a load or version failure at a point
    /// the caller controls.
    /// </summary>
    internal static void EnsureLoaded() =>
        RuntimeHelpers.RunClassConstructor(typeof(KrillaNative).TypeHandle);

    [LibraryImport(library)]
    internal static partial uint krilla_abi_version();

    [LibraryImport(library)]
    internal static partial nuint krilla_abi_sizeof(int kind);

    [LibraryImport(library)]
    internal static partial int krilla_abi_kind_count();

    [LibraryImport(library)]
    internal static partial int krilla_buffer_free(IntPtr ptr, nuint len);

    [LibraryImport(library)]
    internal static partial int krilla_string_free(IntPtr ptr, nuint len);

    [LibraryImport(library)]
    internal static partial int krilla_last_error_message(out IntPtr ptr, out nuint len);

    /// <summary>
    /// Reads and releases the thread-local diagnostic message left by the last failing call.
    /// </summary>
    /// <remarks>
    /// Supplementary detail for a status code, never the primary error channel, and only
    /// meaningful on the thread that saw the failure.
    /// </remarks>
    internal static string? LastErrorMessage()
    {
        if (krilla_last_error_message(out var ptr, out var len) != Status.Ok ||
            ptr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            if (len == 0)
            {
                return null;
            }

            return Marshal.PtrToStringUTF8(ptr, (int) len);
        }
        finally
        {
            krilla_string_free(ptr, len);
        }
    }

    /// <summary>
    /// Copies a native buffer into a managed array and releases the native allocation.
    /// </summary>
    internal static byte[] TakeBuffer(IntPtr ptr, nuint len)
    {
        if (ptr == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var bytes = new byte[(int) len];
            Marshal.Copy(ptr, bytes, 0, (int) len);
            return bytes;
        }
        finally
        {
            krilla_buffer_free(ptr, len);
        }
    }
}
