/// <summary>
/// The two per-page metrics the corpus records against the browser reference render. Both come
/// from a single decode of each image.
///
/// * <c>AbsoluteError</c> — the fraction of pixels that differ at all (0 = identical). Pixels
///   outside the overlap of two differently-sized pages count as differing, and the result is
///   normalised by the REFERENCE page's pixel count, so a page-size mismatch scores near 1 rather
///   than silently comparing a sub-window.
/// * <c>Ssim</c> — structural similarity (1 = identical), 8x8-window and luminance-only. Null when
///   the two images differ in size: <see cref="Ssim.Compare(PngImage, PngImage)"/> indexes the
///   second image with the first image's geometry, so a sub-window score would be silently wrong
///   rather than merely imprecise.
///
/// Read them together. They fail differently: AE is blind to structure — a page with headings
/// drawn through body text can score the same as a clean one when most pixels are white — while
/// SSIM is blind to sparse pixel-exact differences.
///
/// Neither is asserted against a threshold. Both are recorded into the scenario's Verify snapshot,
/// so a change in fidelity shows up as a snapshot diff that has to be consciously accepted. For a
/// text-heavy page the numbers plateau well short of identical however correct the layout is,
/// because PDFium and Skia do not rasterise a glyph edge the same way; that floor is why
/// <see cref="BoxComparison"/> rather than this is the primary signal.
///
/// Ported from Morph's src/Tests/Compare/PageComparison.cs, which carries the same behaviour
/// around size mismatch and normalisation. Keep the two in step.
/// </summary>
static class PageComparison
{
    public static (double AbsoluteError, double? Ssim) Compare(string referencePngPath, byte[] actualPng)
    {
        using var referenceStream = File.OpenRead(referencePngPath);
        var reference = PngDecoder.Decode(referenceStream);
        var actual = PngDecoder.Decode(new MemoryStream(actualPng));

        return (AbsoluteError(reference, actual), Similarity(reference, actual));
    }

    /// <summary>
    /// A page size can disagree by a pixel without anything being wrong: a page whose height in
    /// CSS pixels is not an integer leaves two independent rasterisers free to round it
    /// differently. Dropping SSIM over that would score most of the corpus on error metric alone.
    ///
    /// So a difference of at most one pixel per axis is treated as the rounding artefact it is,
    /// and both images are cropped to their overlap first. Anything larger stays null:
    /// <see cref="Ssim"/> indexes the second image with the first's geometry, and a genuine size
    /// difference would score a silently wrong sub-window rather than merely an imprecise one.
    /// </summary>
    static double? Similarity(PngImage reference, PngImage actual)
    {
        if (Math.Abs(reference.Width - actual.Width) > 1 ||
            Math.Abs(reference.Height - actual.Height) > 1)
        {
            return null;
        }

        if (reference.Width == actual.Width && reference.Height == actual.Height)
        {
            return Math.Round(Ssim.Compare(reference, actual), 4);
        }

        var width = Math.Min(reference.Width, actual.Width);
        var height = Math.Min(reference.Height, actual.Height);
        return Math.Round(Ssim.Compare(Crop(reference, width, height), Crop(actual, width, height)), 4);
    }

    static PngImage Crop(PngImage image, int width, int height)
    {
        if (image.Width == width && image.Height == height)
        {
            return image;
        }

        var cropped = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            Array.Copy(image.Rgba, y * image.Width * 4, cropped, y * width * 4, width * 4);
        }

        return new(width, height, cropped);
    }

    static double AbsoluteError(PngImage reference, PngImage actual)
    {
        var overlapWidth = Math.Min(reference.Width, actual.Width);
        var overlapHeight = Math.Min(reference.Height, actual.Height);
        var referenceRgba = reference.Rgba;
        var actualRgba = actual.Rgba;

        var differing = 0;
        for (var y = 0; y < overlapHeight; y++)
        {
            var referenceRow = y * reference.Width * 4;
            var actualRow = y * actual.Width * 4;
            for (var x = 0; x < overlapWidth; x++)
            {
                var e = referenceRow + x * 4;
                var a = actualRow + x * 4;
                if (referenceRgba[e] != actualRgba[a] ||
                    referenceRgba[e + 1] != actualRgba[a + 1] ||
                    referenceRgba[e + 2] != actualRgba[a + 2])
                {
                    differing++;
                }
            }
        }

        var total = reference.Width * reference.Height;
        differing += total - overlapWidth * overlapHeight;

        return Math.Round((double) differing / total, 4);
    }

    /// <summary>
    /// The number of distinct colours in a PNG.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="BaselineHealthTests"/> to spot a page that has collapsed to a solid
    /// fill. Lives here because it is the only other thing in the suite that needs a decoded
    /// image, and decoding belongs in one place.
    /// </remarks>
    public static int CountColors(string path)
    {
        using var stream = File.OpenRead(path);
        var image = PngDecoder.Decode(stream);

        var colors = new HashSet<uint>();
        for (var offset = 0; offset + 3 < image.Rgba.Length; offset += 4)
        {
            colors.Add(
                ((uint) image.Rgba[offset] << 24) |
                ((uint) image.Rgba[offset + 1] << 16) |
                ((uint) image.Rgba[offset + 2] << 8) |
                image.Rgba[offset + 3]);

            // The threshold this feeds is tiny, so there is no reason to hash a whole page once
            // the answer is already "more than one".
            if (colors.Count > 8)
            {
                break;
            }
        }

        return colors.Count;
    }
}
