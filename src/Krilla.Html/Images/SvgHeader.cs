using System.IO.Compression;

/// <summary>
/// Reads an SVG's intrinsic size out of its root element, without parsing the document.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart to <see cref="ImageData"/>'s PNG and JPEG header readers, and it exists for
/// the same reason: layout needs an intrinsic size, and going through <see cref="PdfSvg"/> for
/// it would drag the native library into layout and take the box comparison with it.
/// </para>
/// <para>
/// The resolution deliberately mirrors usvg's, because krilla-svg scales the tree from the size
/// USVG resolved into the rectangle layout computed from the size resolved here. The two have to
/// agree or the drawing fills a rectangle of the wrong size.
/// </para>
/// <para>
/// Not an XML parse. Only the root element's attributes are wanted, and reaching them needs the
/// start tag and nothing else — an SVG can carry a megabyte of paths behind a fifty-byte header.
/// </para>
/// </remarks>
static class SvgHeader
{
    /// <summary>
    /// How far in the root <c>&lt;svg&gt;</c> tag is looked for.
    /// </summary>
    /// <remarks>
    /// Generous enough for an XML declaration, a DOCTYPE and a licence comment, which is what
    /// stands in front of it in practice, and bounded so a file that merely happens to contain
    /// the text later is not scanned to its end.
    /// </remarks>
    const int headerBytes = 64 * 1024;

    /// <summary>
    /// Reads the size, or returns false when <paramref name="data"/> is not an SVG.
    /// </summary>
    /// <param name="data">The candidate SVG bytes.</param>
    /// <param name="width">The width in CSS pixels.</param>
    /// <param name="height">The height in CSS pixels.</param>
    /// <param name="intrinsic">
    /// Whether the numbers written out are an intrinsic SIZE or only a ratio.
    /// </param>
    /// <remarks>
    /// <para>
    /// The distinction is the whole reason this returns three things rather than two, and it does
    /// not arise for a raster image, which always has both. SVG's own specification defaults the
    /// root element's <c>width</c> and <c>height</c> to <c>100%</c> rather than leaving them
    /// absent — so a document carrying only a <c>viewBox</c> declares a percentage, which is a
    /// share of a containing block rather than a size. It therefore has an aspect ratio and no
    /// intrinsic dimensions, and CSS resolves the percentage where the containing block is known.
    /// </para>
    /// <para>
    /// Measured, because reading it the other way is entirely plausible and wrong: the
    /// <c>#viewbox</c> row of <c>image/svg</c> is 816 wide in Chrome — the full content width —
    /// not the 40 its viewBox names, and not the 300 of the default object size that CSS 2.1's
    /// rule for a replaced element with no intrinsic width would give. Inline and block-level
    /// alike, which is what says this is the percentage resolving rather than a block box's
    /// <c>width: auto</c> filling its container.
    /// </para>
    /// </remarks>
    public static bool TryRead(byte[] data, out int width, out int height, out bool intrinsic)
    {
        width = height = 0;
        intrinsic = false;

        var text = Header(data);
        if (text is null)
        {
            return false;
        }

        var tag = RootTag(text);
        if (tag is null)
        {
            return false;
        }

        // usvg's own fallback for a document declaring neither a size nor a viewBox. It has to
        // be usvg's number rather than a better one: krilla-svg scales the tree from the size
        // usvg resolved, so a different answer here would fill the rectangle at the wrong scale.
        width = 100;
        height = 100;
        intrinsic = true;

        var declaredWidth = TryLength(Attribute(tag, "width"));
        var declaredHeight = TryLength(Attribute(tag, "height"));

        if (declaredWidth is { } w && declaredHeight is { } h)
        {
            width = w;
            height = h;
            return true;
        }

        // A viewBox alone is the common case for an SVG meant to scale. Its extent carries the
        // aspect ratio and is NOT a size; its first two numbers are the origin, which is ignored.
        if (Attribute(tag, "viewBox") is { } viewBox)
        {
            var parts = viewBox.Split(
                [' ', ',', '\t', '\n', '\r'],
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 4 &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var boxWidth) &&
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var boxHeight) &&
                boxWidth > 0 &&
                boxHeight > 0)
            {
                width = Round(boxWidth);
                height = Round(boxHeight);
                intrinsic = false;
            }
        }

        return true;
    }

    /// <summary>
    /// The leading bytes as text, decompressing an <c>.svgz</c> first, or null when the data
    /// cannot be one.
    /// </summary>
    static string? Header(byte[] data)
    {
        if (data.Length < 4)
        {
            return null;
        }

        if (data[0] == 0x1F && data[1] == 0x8B)
        {
            return Decompress(data);
        }

        return Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, headerBytes));
    }

    /// <remarks>
    /// Only the header is inflated, so a compression bomb costs <see cref="headerBytes"/> rather
    /// than whatever the file claims to expand to.
    /// </remarks>
    static string? Decompress(byte[] data)
    {
        try
        {
            using var source = new MemoryStream(data);
            using var gzip = new GZipStream(source, CompressionMode.Decompress);

            var buffer = new byte[headerBytes];
            var read = gzip.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);

            return Encoding.UTF8.GetString(buffer, 0, read);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>
    /// The root <c>&lt;svg&gt;</c> start tag's attribute text, or null when there is none.
    /// </summary>
    /// <remarks>
    /// The element name has to be matched with its delimiter, or an <c>&lt;svgfoo&gt;</c> — or
    /// the word inside a comment ahead of it — would be taken for the root.
    /// </remarks>
    static string? RootTag(string text)
    {
        var index = 0;

        while (true)
        {
            index = text.IndexOf("<svg", index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var after = index + 4;
            if (after >= text.Length)
            {
                return null;
            }

            if (char.IsWhiteSpace(text[after]) || text[after] is '>' or '/')
            {
                var end = text.IndexOf('>', after);

                return end < 0 ? null : text[after..end];
            }

            index = after;
        }
    }

    /// <summary>
    /// The value of <paramref name="name"/> within a start tag's attribute text.
    /// </summary>
    static string? Attribute(string tag, string name)
    {
        var index = 0;

        while (true)
        {
            index = tag.IndexOf(name, index, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            var end = index + name.Length;

            // The name has to stand alone: `width` must not match inside `stroke-width`, and
            // must not match the `width` of `widths`.
            var boundedBefore = index == 0 || char.IsWhiteSpace(tag[index - 1]);
            var rest = tag.AsSpan(end).TrimStart();

            if (boundedBefore && rest.Length > 0 && rest[0] == '=')
            {
                var value = rest[1..].TrimStart();

                if (value.Length > 0 && value[0] is '"' or '\'')
                {
                    var quote = value[0];
                    var close = value[1..].IndexOf(quote);

                    if (close >= 0)
                    {
                        return value[1..(close + 1)].ToString();
                    }
                }

                return null;
            }

            index = end;
        }
    }

    /// <summary>
    /// An absolute length in CSS pixels, or null when the value is a percentage, carries a
    /// font-relative unit, or does not parse.
    /// </summary>
    /// <remarks>
    /// A percentage is not an intrinsic size — it is a share of a containing block the image
    /// does not have — so it falls through to the viewBox exactly as an absent attribute does.
    /// </remarks>
    static int? TryLength(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var text = value.Trim();
        if (text.Length == 0 || text.EndsWith('%'))
        {
            return null;
        }

        var (suffix, scale) = Unit(text);

        if (!float.TryParse(
                text.AsSpan(0, text.Length - suffix),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number) ||
            number <= 0)
        {
            return null;
        }

        return Round(number * scale);
    }

    /// <summary>
    /// The unit's length in characters and its size in CSS pixels, at usvg's 96 dpi.
    /// </summary>
    static (int Suffix, float Scale) Unit(string text) =>
        text switch
        {
            _ when text.EndsWith("px", StringComparison.OrdinalIgnoreCase) => (2, 1f),
            _ when text.EndsWith("pt", StringComparison.OrdinalIgnoreCase) => (2, 96f / 72),
            _ when text.EndsWith("pc", StringComparison.OrdinalIgnoreCase) => (2, 16f),
            _ when text.EndsWith("in", StringComparison.OrdinalIgnoreCase) => (2, 96f),
            _ when text.EndsWith("cm", StringComparison.OrdinalIgnoreCase) => (2, 96f / 2.54f),
            _ when text.EndsWith("mm", StringComparison.OrdinalIgnoreCase) => (2, 96f / 25.4f),
            // Anything else is either unitless — which SVG reads as pixels — or a unit that
            // depends on a font or a viewport this image does not have, and both fall to the
            // parse below: a font-relative value leaves a letter in front of the number and
            // fails to parse, which is the answer wanted.
            _ => (0, 1f)
        };

    static int Round(float value) =>
        (int) MathF.Round(value, MidpointRounding.AwayFromZero);
}
