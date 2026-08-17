namespace Krilla;

/// <summary>
/// Encoded image formats krilla can decode.
/// </summary>
public enum ImageFormat
{
    /// <summary>PNG.</summary>
    Png = 0,

    /// <summary>JPEG.</summary>
    Jpeg = 1,

    /// <summary>GIF.</summary>
    Gif = 2,

    /// <summary>WebP.</summary>
    Webp = 3
}

/// <summary>
/// A decoded raster image.
/// </summary>
/// <remarks>
/// Expensive to create and cheap to draw, and krilla deduplicates repeated use in the output,
/// so one instance should be shared everywhere the same image appears.
/// </remarks>
public sealed class PdfImage :
    IDisposable
{
    IntPtr handle;

    PdfImage(IntPtr handle, uint width, uint height)
    {
        this.handle = handle;
        Width = width;
        Height = height;
    }

    internal IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
            return handle;
        }
    }

    /// <summary>
    /// Width in pixels.
    /// </summary>
    public uint Width { get; }

    /// <summary>
    /// Height in pixels.
    /// </summary>
    public uint Height { get; }

    /// <summary>
    /// Decodes an image from encoded bytes.
    /// </summary>
    /// <param name="format">The encoding of <paramref name="data"/>.</param>
    /// <param name="data">The encoded bytes. Copied, so the buffer need not be retained.</param>
    /// <param name="interpolate">
    /// Asks viewers to smooth the image when it is scaled up. PDF/A forbids this, so enabling
    /// it makes an archival document fail validation when it is finished.
    /// </param>
    public static PdfImage Load(ImageFormat format, ReadOnlySpan<byte> data, bool interpolate = false)
    {
        KrillaNative.EnsureLoaded();

        Status.Check(
            KrillaNative.krilla_image_new_encoded(
                (int) format,
                data,
                (nuint) data.Length,
                interpolate,
                out var handle),
            "Decoding an image");

        return Wrap(handle);
    }

    /// <summary>
    /// Decodes an image from a file, inferring the format from its extension.
    /// </summary>
    public static PdfImage LoadFile(string path, bool interpolate = false)
    {
        var format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => ImageFormat.Png,
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".gif" => ImageFormat.Gif,
            ".webp" => ImageFormat.Webp,
            var extension => throw new ArgumentException(
                $"Cannot infer an image format from '{extension}'. Use Load and name the format.",
                nameof(path))
        };

        return Load(format, File.ReadAllBytes(path), interpolate);
    }

    /// <summary>
    /// Builds an image from raw, non-premultiplied RGBA bytes, four per pixel, row-major.
    /// </summary>
    /// <exception cref="KrillaException">
    /// The buffer length is not exactly <c>width * height * 4</c>.
    /// </exception>
    public static PdfImage FromRgba(
        ReadOnlySpan<byte> data,
        uint width,
        uint height)
    {
        KrillaNative.EnsureLoaded();

        Status.Check(
            KrillaNative.krilla_image_new_rgba8(
                data,
                (nuint) data.Length,
                width,
                height,
                out var handle),
            "Creating an image from RGBA data");

        return Wrap(handle);
    }

    static PdfImage Wrap(IntPtr handle)
    {
        Status.Check(
            KrillaNative.krilla_image_size(handle, out var width, out var height),
            "Reading image size");

        return new(handle, width, height);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_image_free(handle);
        handle = IntPtr.Zero;
    }
}
