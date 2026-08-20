namespace Krilla.Html;

/// <summary>
/// Which image sources a conversion may load from.
/// </summary>
/// <remarks>
/// <para>
/// Checked before the resolver runs, so it constrains a caller-supplied
/// <see cref="HtmlOptions.ImageResolver"/> as well as the built-in one. That is the point: a
/// resolver that fetches is easy to write and easy to write without a host allow-list, and this is
/// the allow-list.
/// </para>
/// <para>
/// A <c>data:</c> URI is never gated. It carries its own bytes, so loading one reaches nothing
/// outside the document it is already in.
/// </para>
/// <para>
/// A refusal is not silent: it reaches <see cref="HtmlOptions.OnDiagnostic"/>, because an image
/// that is missing from the output because policy declined it looks exactly like one missing
/// because the file was not there.
/// </para>
/// </remarks>
public sealed class ImagePolicy
{
    readonly bool allowAll;
    readonly Func<string, bool>? filter;

    ImagePolicy(bool allowAll, Func<string, bool>? filter)
    {
        this.allowAll = allowAll;
        this.filter = filter;
    }

    /// <summary>
    /// Refuses every source.
    /// </summary>
    public static ImagePolicy Deny() =>
        new(allowAll: false, null);

    /// <summary>
    /// Allows any source.
    /// </summary>
    public static ImagePolicy AllowAll() =>
        new(allowAll: true, null);

    /// <summary>
    /// Allows local files under any of <paramref name="directories"/>, and nothing else.
    /// </summary>
    /// <remarks>
    /// Compares full paths, so a <c>..</c> segment climbing out of an allowed directory is
    /// refused rather than followed.
    /// </remarks>
    public static ImagePolicy SafeDirectories(params string[] directories)
    {
        var allowed = Array.ConvertAll(directories, Normalize);

        return new(
            allowAll: false,
            source =>
            {
                var path = source;

                if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
                    {
                        return false;
                    }

                    path = uri.LocalPath;
                }

                try
                {
                    var full = Path.GetFullPath(path);
                    return Array.Exists(
                        allowed,
                        directory => full.StartsWith(directory, StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    // A source that is not a usable path at all cannot be under an allowed
                    // directory, so it is refused rather than throwing out of the conversion.
                    return false;
                }
            });
    }

    /// <summary>
    /// Allows web sources on any of <paramref name="domains"/> or their subdomains, and nothing
    /// else.
    /// </summary>
    public static ImagePolicy SafeDomains(params string[] domains)
    {
        var allowed = domains.ToArray();

        return new(
            allowAll: false,
            source =>
            {
                if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                var host = uri.Host;

                return Array.Exists(
                    allowed,
                    domain =>
                        string.Equals(host, domain, StringComparison.OrdinalIgnoreCase) ||
                        host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase));
            });
    }

    /// <summary>
    /// Allows the sources <paramref name="predicate"/> accepts.
    /// </summary>
    public static ImagePolicy Filter(Func<string, bool> predicate) =>
        new(allowAll: false, predicate);

    internal bool IsAllowed(string source) =>
        allowAll || (filter is not null && filter(source));

    static string Normalize(string directory)
    {
        var full = Path.GetFullPath(directory);

        // With a trailing separator, so that an allowed "C:\a" does not also allow "C:\ab".
        return full.EndsWith(Path.DirectorySeparatorChar) || full.EndsWith(Path.AltDirectorySeparatorChar)
            ? full
            : full + Path.DirectorySeparatorChar;
    }
}
