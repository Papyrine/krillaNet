/// <summary>
/// Resolves <c>src</c> attributes to images, once per source per conversion.
/// </summary>
/// <remarks>
/// <para>
/// Caching is not only a speed concern. krilla deduplicates identical images in its output, but
/// only when handed the same <see cref="PdfImage"/> instance — decoding the same file twice
/// produces two handles and embeds the bytes twice, so a document repeating one logo on forty
/// pages would carry forty copies of it.
/// </para>
/// <para>
/// A source that cannot be resolved is cached as a miss, so a broken <c>src</c> is looked up once
/// rather than on every layout pass.
/// </para>
/// </remarks>
sealed class ImageStore(
    Func<string, byte[]?> resolver,
    ImagePolicy local,
    ImagePolicy web,
    FontSet? fonts = null)
    : IDisposable
{
    readonly Dictionary<string, ImageData?> cache = new(StringComparer.Ordinal);

    /// <summary>
    /// The drawings inline <c>&lt;svg&gt;</c> elements were read as, keyed by the element itself.
    /// </summary>
    /// <remarks>
    /// A second cache rather than a second key into the first, because these have no source string
    /// to be keyed by and two identical drawings in one document are still two elements.
    /// </remarks>
    readonly Dictionary<IElement, ImageData?> inline = [];
    SvgOptions? svgOptions;
    bool svgOptionsBuilt;

    /// <summary>
    /// The font database SVG text is shaped against, built from the document's own fonts on first
    /// use, or null when the native library has no SVG support.
    /// </summary>
    /// <remarks>
    /// <para>
    /// usvg resolves text while it parses and matches families itself, so it cannot be handed the
    /// face <see cref="FontSet"/> would pick — it needs the files and does its own matching. Every
    /// registered face is loaded, and the fallback's family becomes the default, so an SVG whose
    /// text names no family draws in the same face the document's own text would.
    /// </para>
    /// <para>
    /// Deferred, because building it loads every font file into a second database and the great
    /// majority of documents contain no SVG at all. Built at most once per conversion, which is
    /// the point of caching a negative result too.
    /// </para>
    /// </remarks>
    SvgOptions? SvgOptions
    {
        get
        {
            if (svgOptionsBuilt)
            {
                return svgOptions;
            }

            svgOptionsBuilt = true;

            if (fonts is null || !PdfSvg.IsSupported)
            {
                return null;
            }

            svgOptions = new();

            foreach (var face in fonts.Faces)
            {
                svgOptions.AddFont(face.Data);
            }

            if (fonts.Fallback is {} fallback)
            {
                svgOptions.SetDefaultFamily(fallback.Family);
            }

            return svgOptions;
        }
    }

    /// <summary>
    /// The drawing an inline <c>&lt;svg&gt;</c> element is, or null when it cannot be read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ungated by the image policy, and for the same reason a <c>data:</c> URI is: the bytes are
    /// already in the document, so there is nothing to fetch and nothing to refuse. Cached against
    /// the element rather than a source string, there being no source.
    /// </para>
    /// <para>
    /// The namespace declaration is added when the markup does not carry one, which is nearly
    /// always: an HTML parser puts an <c>&lt;svg&gt;</c> in the SVG namespace by position rather
    /// than by declaration, so a document almost never writes the <c>xmlns</c> that a standalone
    /// SVG parser then requires.
    /// </para>
    /// </remarks>
    public ImageData? Inline(IElement element)
    {
        if (inline.TryGetValue(element, out var cached))
        {
            return cached;
        }

        ImageData? image = null;

        try
        {
            var markup = element.OuterHtml;

            if (!markup.Contains("xmlns", StringComparison.OrdinalIgnoreCase))
            {
                markup = markup.Insert(4, " xmlns=\"http://www.w3.org/2000/svg\"");
            }

            image = ImageData.Read(Encoding.UTF8.GetBytes(markup));

            if (image is {IsVector: true})
            {
                image.SvgOptions = SvgOptions;
            }
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            image = null;
        }

        inline[element] = image;
        return image;
    }

    /// <summary>
    /// The image for <paramref name="source"/>, or null when policy refuses it, it cannot be
    /// resolved, or it is not a format krilla can decode.
    /// </summary>
    /// <param name="source">The <c>src</c> as written.</param>
    /// <param name="reason">
    /// Why nothing came back, when nothing did. The three causes are indistinguishable from the
    /// output — each leaves the same gap on the page — so the caller reporting the gap needs to be
    /// told which it was.
    /// </param>
    public ImageData? Resolve(string source, out string reason)
    {
        reason = "did not resolve to an image, so no box was generated";

        if (!Allowed(source))
        {
            reason = "was refused by the image policy, so no box was generated";
            return null;
        }

        if (cache.TryGetValue(source, out var cached))
        {
            return cached;
        }

        ImageData? image = null;

        try
        {
            if (resolver(source) is {} bytes)
            {
                image = ImageData.Read(bytes);

                if (image is {IsVector: true})
                {
                    image.SvgOptions = SvgOptions;
                }
            }
        }
        catch (Exception exception) when
            (exception is
                 IOException or
                 UnauthorizedAccessException or
                 FormatException)
        {
            // A missing or unreadable image leaves a gap on the page rather than failing the whole
            // conversion. A document is usually still worth producing without one of its pictures,
            // and the alternative — throwing — makes a single broken src fatal.
            image = null;
        }

        cache[source] = image;
        return image;
    }

    /// <summary>
    /// The default resolver: <c>data:</c> URIs, and files relative to
    /// <paramref name="baseUrl"/>.
    /// </summary>
    /// <remarks>
    /// It does not fetch over the network, and that is a deliberate default rather than a missing
    /// feature. Converting an untrusted document would otherwise issue requests to whatever hosts
    /// that document names, which leaks the fact and timing of the conversion and can be used to
    /// probe hosts reachable from the converting machine. A caller who wants remote images can
    /// supply <see cref="HtmlOptions.ImageResolver"/> and take that decision explicitly.
    /// </remarks>
    public static Func<string, byte[]?> DefaultResolver(string? baseUrl) =>
        source =>
        {
            if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return ReadDataUri(source);
            }

            if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var path = ResolvePath(source, baseUrl);
            if (path is not null && File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }

            return null;
        };

    /// <summary>
    /// Whether policy permits <paramref name="source"/> to be loaded at all.
    /// </summary>
    /// <remarks>
    /// A <c>data:</c> URI is ungated: its bytes are already in the document, so loading one
    /// reaches nothing the conversion did not already have. Everything else is either a web source
    /// or a local one, and there is no third case — a relative <c>src</c> resolves against
    /// <see cref="HtmlOptions.BaseUrl"/> to a file, so it is local.
    /// </remarks>
    bool Allowed(string source)
    {
        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var policy =
            source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? web
                : local;

        return policy.IsAllowed(source);
    }

    /// <summary>
    /// Decodes a <c>data:</c> URI, which is the only source that carries its own bytes.
    /// </summary>
    static byte[]? ReadDataUri(string source)
    {
        var comma = source.IndexOf(',');
        if (comma < 0)
        {
            return null;
        }

        var meta = source[5..comma];
        var payload = source[(comma + 1)..];

        if (!meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            // Percent-encoded rather than base64. Rare for images, but legal.
            return Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
        }

        // Whitespace is legal inside a data URI's base64 payload and common when one has been
        // wrapped across lines in the source, but Convert.FromBase64String rejects newlines.
        return Convert.FromBase64String(payload.Replace("\n", "").Replace("\r", "").Replace(" ", ""));
    }

    /// <summary>
    /// Turns a relative <c>src</c> into a path on disk, against
    /// <paramref name="baseUrl"/>.
    /// </summary>
    static string? ResolvePath(string source, string? baseUrl)
    {
        if (source.StartsWith("file:", StringComparison.OrdinalIgnoreCase) &&
            Uri.TryCreate(source, UriKind.Absolute, out var fileUri))
        {
            return fileUri.LocalPath;
        }

        if (Path.IsPathRooted(source))
        {
            return source;
        }

        if (baseUrl is null)
        {
            return null;
        }

        // A base that is a real directory or a file:// URL gives a directory to resolve against.
        // Anything else — an http base, "about:blank" — has no local meaning, so a relative source
        // under it resolves to nothing rather than to a path on this machine.
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            if (!baseUri.IsFile)
            {
                return null;
            }

            var directory = Path.GetDirectoryName(baseUri.LocalPath);
            if (directory is null)
            {
                return null;
            }

            return Path.Combine(directory, source);
        }

        if (Directory.Exists(baseUrl))
        {
            return Path.Combine(baseUrl, source);
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var image in cache.Values)
        {
            image?.Dispose();
        }

        foreach (var image in inline.Values)
        {
            image?.Dispose();
        }

        cache.Clear();
        inline.Clear();

        // After the images, which hold parsed SVGs that were built against it.
        svgOptions?.Dispose();
        svgOptions = null;
    }
}
