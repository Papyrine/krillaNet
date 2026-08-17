// Blittable mirrors of the #[repr(C)] structs in rust/crates/krilla-capi/src/types.rs.
//
// Layout is ABI. The Rust side pins every size and offset with a const assertion, and
// AbiTests loops krilla_abi_sizeof over each kind to prove the two sides still agree — which
// is the cheapest available defence against the one bug class here that would otherwise be
// silent memory corruption rather than an exception.
//
// Kept internal: the public API exposes ordinary types and converts at the boundary, so the
// package never leaks its ABI into consumers' code.

/// <summary>Kind discriminants for <c>krilla_abi_sizeof</c>.</summary>
static class AbiKind
{
    public const int Point = 0;
    public const int Size = 1;
    public const int Rect = 2;
    public const int Transform = 3;
    public const int Color = 4;
    public const int Fill = 5;
    public const int Stroke = 6;
    public const int Glyph = 7;
    public const int PageSettings = 8;
    public const int Stop = 9;
    public const int DateTime = 10;
    public const int DocumentOptions = 11;
}

/// <summary>
/// A date as PDF records them. Only the year is required; the flags say which of the rest
/// carry meaning, since 0 is a legal hour and month.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
struct NativeDateTime
{
    public ushort Year;
    public byte Month;
    public byte Day;
    public byte Hour;
    public byte Minute;
    public byte Second;
    public byte HasTime;
    public byte HasUtcOffset;
    public sbyte UtcOffsetHour;
    public byte UtcOffsetMinute;
}

/// <summary>
/// Serialize settings and conformance configuration for a new document.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
struct NativeDocumentOptions
{
    public int PdfVersion;
    public int Archival;
    public int Accessibility;
    public byte CompressStreams;
    public byte AsciiCompatible;
    public byte XmpMetadata;
    public byte EnableTagging;
    public byte Pretty;
    public byte NoDeviceColorspace;
    public byte Reserved;
}

[StructLayout(LayoutKind.Sequential)]
struct NativePoint
{
    public float X;
    public float Y;
}

[StructLayout(LayoutKind.Sequential)]
struct NativeSize
{
    public float Width;
    public float Height;
}

[StructLayout(LayoutKind.Sequential)]
struct NativeRect
{
    public float Left;
    public float Top;
    public float Right;
    public float Bottom;
}

[StructLayout(LayoutKind.Sequential)]
struct NativeTransform
{
    public float ScaleX;
    public float SkewY;
    public float SkewX;
    public float ScaleY;
    public float TranslateX;
    public float TranslateY;
}

/// <summary>
/// A device colour. Components are 8 bit because that is krilla's own constructor signature
/// for every space. Unused components are ignored rather than validated.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
struct NativeColor
{
    public int Space;
    public byte C0;
    public byte C1;
    public byte C2;
    public byte C3;
}

[StructLayout(LayoutKind.Sequential)]
struct NativeFill
{
    public float Opacity;
    public int Rule;
}

[StructLayout(LayoutKind.Sequential)]
struct NativeStroke
{
    public float Width;
    public float MiterLimit;
    public int LineCap;
    public int LineJoin;
    public float Opacity;
    public float DashOffset;
    public IntPtr DashArray;
    public nuint DashLength;
}

/// <summary>
/// One positioned glyph. Advances and offsets must already be divided by the font's
/// units-per-em; <see cref="Surface.DrawGlyphs"/> does that on the caller's behalf.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
struct NativeGlyph
{
    public uint GlyphId;
    public uint TextStart;
    public uint TextEnd;
    public float XAdvance;
    public float XOffset;
    public float YOffset;
    public float YAdvance;
    public ulong Location;
}

/// <summary>
/// A colour stop in a gradient.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
struct NativeStop
{
    public float Offset;
    public NativeColor Color;
    public float Opacity;
}

/// <summary>
/// Page geometry. The optional boxes are always present in the struct; <see cref="Present"/>
/// says which carry meaning, because any float is a legal box edge and no sentinel exists.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
struct NativePageSettings
{
    public float Width;
    public float Height;
    public NativeRect MediaBox;
    public NativeRect CropBox;
    public NativeRect BleedBox;
    public NativeRect TrimBox;
    public NativeRect ArtBox;
    public uint Present;
    public uint Reserved;
}
