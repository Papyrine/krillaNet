namespace Krilla.Html.Images;

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
sealed class ImageStore :
    IDisposable
{
    readonly Dictionary<string, ImageData?> cache = new(StringComparer.Ordinal);
    readonly Func<string, byte[]?> resolver;

    public ImageStore(Func<string, byte[]?> resolver) =>
        this.resolver = resolver;

    /// <summary>
    /// The image for <paramref name="source"/>, or null when it cannot be resolved or is not a
    /// format krilla can decode.
    /// </summary>
    public ImageData? Resolve(string source)
    {
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
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
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
            return path is not null && File.Exists(path) ? File.ReadAllBytes(path) : null;
        };

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
            return directory is null ? null : Path.Combine(directory, source);
        }

        return Directory.Exists(baseUrl)
            ? Path.Combine(baseUrl, source)
            : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var image in cache.Values)
        {
            image?.Dispose();
        }

        cache.Clear();
    }
}
