using System.Reflection;

/// <summary>
/// Guards the boundary between the managed <c>Interop/</c> mirrors and the Rust
/// <c>#[repr(C)]</c> structs.
/// </summary>
/// <remarks>
/// A layout disagreement between the two sides is the one bug class here that produces silent
/// memory corruption rather than an exception. The native library reports its own sizes, so
/// the check is a loop rather than a hand-transcribed table that could drift on its own.
/// </remarks>
public class AbiTests
{
    static readonly Dictionary<int, Type> mirrors = new()
    {
        [AbiKind.Point] = typeof(NativePoint),
        [AbiKind.Size] = typeof(NativeSize),
        [AbiKind.Rect] = typeof(NativeRect),
        [AbiKind.Transform] = typeof(NativeTransform),
        [AbiKind.Color] = typeof(NativeColor),
        [AbiKind.Fill] = typeof(NativeFill),
        [AbiKind.Stroke] = typeof(NativeStroke),
        [AbiKind.Glyph] = typeof(NativeGlyph),
        [AbiKind.PageSettings] = typeof(NativePageSettings),
        [AbiKind.Stop] = typeof(NativeStop),
        [AbiKind.DateTime] = typeof(NativeDateTime),
        [AbiKind.DocumentOptions] = typeof(NativeDocumentOptions)
    };

    [Test]
    public void NativeLibraryLoadsAndReportsTheExpectedAbiVersion()
    {
        KrillaNative.EnsureLoaded();

        var actual = KrillaNative.krilla_abi_version();

        if (actual != KrillaNative.ExpectedAbiVersion)
        {
            throw new($"Expected ABI version {KrillaNative.ExpectedAbiVersion}, got {actual}.");
        }
    }

    [Test]
    public void EveryMirroredStructMatchesItsNativeSize()
    {
        KrillaNative.EnsureLoaded();

        var count = KrillaNative.krilla_abi_kind_count();

        if (count != mirrors.Count)
        {
            throw new(
                $"The native library reports {count} ABI kinds but this test covers {mirrors.Count}. A struct was added without a managed mirror.");
        }

        foreach (var (kind, type) in mirrors)
        {
            var native = (int) KrillaNative.krilla_abi_sizeof(kind);
            var managed = Marshal.SizeOf(type);

            if (native != managed)
            {
                throw new(
                    $"{type.Name} is {managed} bytes managed but {native} bytes native (kind {kind}).");
            }
        }
    }

    /// <summary>
    /// Every mirrored struct must be blittable, since the assembly opts out of runtime
    /// marshalling.
    /// </summary>
    [Test]
    public void EveryMirroredStructIsBlittable()
    {
        foreach (var type in mirrors.Values)
        {
            // GCHandle rejects a non-blittable type outright, which is exactly the question.
            var instance = Activator.CreateInstance(type)!;
            var handle = GCHandle.Alloc(instance, GCHandleType.Pinned);
            handle.Free();
        }
    }

    /// <summary>
    /// The public API must not leak the ABI mirrors into consumers' code.
    /// </summary>
    [Test]
    public void InteropTypesAreNotPublic()
    {
        var leaked = typeof(KrillaDocument).Assembly
            .GetTypes()
            .Where(_ => _.IsPublic && _.Name.StartsWith("Native", StringComparison.Ordinal))
            .Select(_ => _.FullName)
            .ToList();

        if (leaked.Count > 0)
        {
            throw new($"Interop mirrors must stay internal, but these are public: {string.Join(", ", leaked)}");
        }
    }
}
