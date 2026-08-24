/// <summary>
/// An encoded image: its bytes, its format, and the intrinsic size read from its header.
/// </summary>
/// <remarks>
/// <para>
/// The size is parsed here rather than taken from <see cref="PdfImage"/>, which also reports it.
/// Layout needs the intrinsic size to resolve an <c>auto</c> width, and going through
/// <see cref="PdfImage"/> for it would drag the native library into layout — undoing the same
/// separation <see cref="FontFace"/> keeps for fonts, and taking the box comparison with it. Only
/// a header is read, so the cost is a few dozen bytes rather than a decode.
/// </para>
/// <para>
/// The krilla image is created on first paint, and krilla does the real decoding.
/// </para>
/// <para>
/// An SVG is carried here too, and the same rule applies to it: its intrinsic size is read out
/// of the root element's own attributes in managed code, not by parsing the document through
/// <see cref="PdfSvg"/>. The resolution mirrors usvg's — the <c>width</c> and <c>height</c>
/// attributes when both are absolute lengths, the <c>viewBox</c>'s extent otherwise — because
/// layout computes the destination rectangle from these numbers while krilla-svg scales the tree
/// from the size IT resolved. Two different answers would leave the drawing correctly filling a
/// rectangle that was the wrong size.
/// </para>
/// </remarks>
sealed class ImageData :
    IDisposable
{
    readonly byte[] data;
    readonly ImageFormat? format;
    PdfImage? image;
    PdfSvg? svg;

    ImageData(byte[] data, ImageFormat? format, int width, int height, bool intrinsic = true)
    {
        this.data = data;
        this.format = format;
        Width = width;
        Height = height;
        HasIntrinsicSize = intrinsic;
    }

    /// <summary>
    /// Whether <see cref="Width"/> and <see cref="Height"/> are an intrinsic SIZE, or only carry
    /// the aspect ratio.
    /// </summary>
    /// <remarks>
    /// Always true for a raster image, which cannot be anything but a fixed grid of pixels. False
    /// for an SVG declaring only a <c>viewBox</c>, whose root <c>width</c> and <c>height</c>
    /// default to <c>100%</c> — a share of a containing block rather than a size. Such an image
    /// still has a ratio, so <see cref="Ratio"/> means the same thing either way, and only the
    /// case where NEITHER dimension is declared in CSS has to tell them apart.
    /// </remarks>
    public bool HasIntrinsicSize { get; }

    /// <summary>
    /// Whether this is an SVG rather than a raster image.
    /// </summary>
    /// <remarks>
    /// The only thing outside this class that has to know: the painter draws through
    /// <see cref="Krilla.Surface.DrawSvg"/> instead of <see cref="Krilla.Surface.DrawImage"/>.
    /// Everything else — <c>object-fit</c>, background tiling, replaced sizing — works off
    /// <see cref="Width"/>, <see cref="Height"/> and <see cref="Ratio"/>, which mean the same
    /// thing either way.
    /// </remarks>
    public bool IsVector => format is null;

    /// <summary>Intrinsic width in pixels.</summary>
    public int Width { get; }

    /// <summary>Intrinsic height in pixels.</summary>
    public int Height { get; }

    /// <summary>
    /// The intrinsic aspect ratio, or null when either dimension is zero.
    /// </summary>
    /// <remarks>
    /// Null rather than a divide by zero, because a degenerate image must not take the whole
    /// layout down with it — an image is content, and content being wrong is better than a
    /// document failing to render.
    /// </remarks>
    public float? Ratio
    {
        get
        {
            if (Width > 0 && Height > 0)
            {
                return (float) Width / Height;
            }

            return null;
        }
    }

    /// <summary>The krilla image, decoded on first use.</summary>
    /// <exception cref="InvalidOperationException">This is an SVG.</exception>
    public PdfImage Image =>
        image ??= PdfImage.Load(
            format ?? throw new InvalidOperationException("This is an SVG; use Svg."),
            data);

    /// <summary>
    /// The fonts text inside this SVG is shaped against, supplied by <see cref="ImageStore"/>.
    /// </summary>
    /// <remarks>
    /// Assigned rather than passed to <see cref="Read"/>, which is format-agnostic and reads a
    /// header without knowing what it will find. Null leaves an SVG's text unshaped and therefore
    /// undrawn, which is what happens when the native library has no SVG support at all — and in
    /// that case the SVG never gets parsed either.
    /// </remarks>
    public SvgOptions? SvgOptions { get; set; }

    /// <summary>The krilla SVG, parsed on first use.</summary>
    /// <exception cref="InvalidOperationException">This is a raster image.</exception>
    public PdfSvg Svg
    {
        get
        {
            if (!IsVector)
            {
                throw new InvalidOperationException("This is a raster image; use Image.");
            }

            return svg ??= PdfSvg.Load(data, SvgOptions);
        }
    }

    /// <summary>
    /// Reads <paramref name="data"/>, or returns null when it is not an image format krilla can
    /// decode.
    /// </summary>
    public static ImageData? Read(byte[] data)
    {
        if (TryPng(data, out var width, out var height))
        {
            return new(data, ImageFormat.Png, width, height);
        }

        if (TryJpeg(data, out width, out height))
        {
            return new(data, ImageFormat.Jpeg, width, height);
        }

        if (TryGif(data, out width, out height))
        {
            return new(data, ImageFormat.Gif, width, height);
        }

        if (TryWebp(data, out width, out height))
        {
            return new(data, ImageFormat.Webp, width, height);
        }

        // Last, because it is the one format identified by its content rather than by a magic
        // number at a fixed offset, so its sniff is the only one that could plausibly match
        // something else.
        if (SvgHeader.TryRead(data, out width, out height, out var intrinsic))
        {
            return new(data, null, width, height, intrinsic);
        }

        return null;
    }

    /// <summary>
    /// PNG: an 8-byte signature, then an IHDR chunk whose first two fields are the dimensions.
    /// </summary>
    static bool TryPng(byte[] data, out int width, out int height)
    {
        width = height = 0;

        if (data.Length < 24 ||
            data[0] != 0x89 || data[1] != 'P' || data[2] != 'N' || data[3] != 'G')
        {
            return false;
        }

        width = (int) BigEndian32(data, 16);
        height = (int) BigEndian32(data, 20);
        return true;
    }

    /// <summary>
    /// JPEG: a marker chain that has to be walked, because the frame header carrying the
    /// dimensions sits after a variable number of other segments.
    /// </summary>
    static bool TryJpeg(byte[] data, out int width, out int height)
    {
        width = height = 0;

        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return false;
        }

        var offset = 2;

        while (offset + 4 <= data.Length)
        {
            if (data[offset] != 0xFF)
            {
                return false;
            }

            var marker = data[offset + 1];

            // Padding between segments is legal and encoded as repeated 0xFF bytes.
            if (marker == 0xFF)
            {
                offset++;
                continue;
            }

            // Standalone markers carry no length field.
            if (marker is 0xD8 or 0x01 or >= 0xD0 and <= 0xD7)
            {
                offset += 2;
                continue;
            }

            var length = (data[offset + 2] << 8) | data[offset + 3];
            if (length < 2)
            {
                return false;
            }

            // Any Start Of Frame carries the dimensions. The excluded values in these ranges are
            // DHT, JPG and DAC, which are not frame headers despite sitting among them.
            var isStartOfFrame =
                marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF;

            if (isStartOfFrame)
            {
                if (offset + 9 > data.Length)
                {
                    return false;
                }

                height = (data[offset + 5] << 8) | data[offset + 6];
                width = (data[offset + 7] << 8) | data[offset + 8];
                return true;
            }

            offset += 2 + length;
        }

        return false;
    }

    /// <summary>GIF: dimensions sit in the logical screen descriptor, little-endian.</summary>
    static bool TryGif(byte[] data, out int width, out int height)
    {
        width = height = 0;

        if (data.Length < 10 || data[0] != 'G' || data[1] != 'I' || data[2] != 'F')
        {
            return false;
        }

        width = data[6] | (data[7] << 8);
        height = data[8] | (data[9] << 8);
        return true;
    }

    /// <summary>
    /// WebP: a RIFF container whose dimensions live in whichever of three chunk types it uses.
    /// </summary>
    static bool TryWebp(byte[] data, out int width, out int height)
    {
        width = height = 0;

        if (data.Length < 30 ||
            data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F' ||
            data[8] != 'W' || data[9] != 'E' || data[10] != 'B' || data[11] != 'P')
        {
            return false;
        }

        var chunk = Encoding.ASCII.GetString(data, 12, 4);

        switch (chunk)
        {
            case "VP8 ":
                // Lossy. Dimensions follow the 3-byte frame tag and the 3-byte start code, and
                // carry two high bits of scaling that are not part of the size.
                width = (data[26] | (data[27] << 8)) & 0x3FFF;
                height = (data[28] | (data[29] << 8)) & 0x3FFF;
                return true;

            case "VP8L":
                // Lossless. 14 bits each, stored minus one, packed across four bytes.
                var bits = (uint) (data[21] | (data[22] << 8) | (data[23] << 16) | (data[24] << 24));
                width = (int) (bits & 0x3FFF) + 1;
                height = (int) ((bits >> 14) & 0x3FFF) + 1;
                return true;

            case "VP8X":
                // Extended. Canvas size as two 24-bit little-endian values, stored minus one.
                width = (data[24] | (data[25] << 8) | (data[26] << 16)) + 1;
                height = (data[27] | (data[28] << 8) | (data[29] << 16)) + 1;
                return true;

            default:
                return false;
        }
    }

    static uint BigEndian32(byte[] data, int offset) =>
        ((uint) data[offset] << 24) |
        ((uint) data[offset + 1] << 16) |
        ((uint) data[offset + 2] << 8) |
        data[offset + 3];

    /// <inheritdoc />
    public void Dispose()
    {
        image?.Dispose();
        image = null;
        svg?.Dispose();
        svg = null;
    }
}
