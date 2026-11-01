namespace Krilla;

/// <summary>
/// Finds fonts on disk by family name and style.
/// </summary>
/// <remarks>
/// <para>
/// krilla has no font database, so this is one: it scans directories for <c>.ttf</c>,
/// <c>.otf</c>, <c>.ttc</c> and <c>.otc</c> files, reads each face's names and style out of its
/// <c>name</c>, <c>OS/2</c> and <c>head</c> tables, and matches a request against them.
/// <see cref="System"/> scans the platform's installed fonts; a repository built from a
/// directory list scans exactly those, which is how a test pins the fonts it renders with.
/// </para>
/// <para>
/// The scan runs once, on first use, and is cached. It is safe to call from several threads at
/// once: they share one scan. <see cref="Refresh"/> discards it, for a caller that has installed
/// fonts since.
/// </para>
/// <para>
/// A request matches a face's typographic family (name id 16) or legacy family (id 1), in any
/// language the font carries, ignoring case. The PDF Standard-14 names are mapped onto the faces
/// readers substitute for them — <c>Helvetica</c> to Arial, <c>Times-Roman</c> to Times New
/// Roman, <c>Courier</c> to Courier New — and those three are followed by their
/// metric-compatible stand-ins, Liberation and Croscore, for a machine that lacks them. A name
/// matching no family is then tried as a full name or a PostScript name, such as
/// <c>Arial Bold</c> or <c>Arial-BoldMT</c>, which name one face and so ignore the requested
/// style. Last, <see cref="FallbackFamily"/>, if set.
/// </para>
/// <para>
/// Among a family's faces the closest style wins rather than an exact one: italic first, then
/// bold, then nearest weight. A bold request against a family with no bold face returns the
/// regular one; <see cref="FontFaceInfo.Bold"/> and <see cref="FontFaceInfo.Italic"/> on the
/// result say what was actually found.
/// </para>
/// </remarks>
public sealed class FontRepository
{
    static readonly Lazy<IReadOnlyList<string>> systemDirectories = new(SystemFontDirectories.Resolve);
    static readonly Lazy<FontRepository> system = new(() => new(SystemDirectories));
    static FontRepository? @default;

    static readonly string[] extensions = [".ttf", ".otf", ".ttc", ".otc"];

    static readonly StringComparer pathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    Lazy<IReadOnlyList<FontFaceInfo>> faces;

    /// <summary>
    /// A repository scanning exactly <paramref name="directories"/>, recursively.
    /// </summary>
    /// <remarks>
    /// To add directories to the installed fonts rather than replace them, include
    /// <see cref="SystemDirectories"/>: <c>new FontRepository([..FontRepository.SystemDirectories, mine])</c>.
    /// A directory that does not exist is skipped, not an error.
    /// </remarks>
    public FontRepository(params IEnumerable<string> directories)
    {
        Directories = [..directories.Select(Path.GetFullPath)];
        faces = NewScan();
    }

    /// <summary>
    /// The installed-font directories on this platform: on Windows the system and per-user font
    /// folders, on macOS <c>/System/Library/Fonts</c>, <c>/Library/Fonts</c> and
    /// <c>~/Library/Fonts</c>, and on Linux the fontconfig <c>&lt;dir&gt;</c> entries plus the
    /// XDG defaults. Only directories that exist are listed.
    /// </summary>
    public static IReadOnlyList<string> SystemDirectories => systemDirectories.Value;

    /// <summary>
    /// The repository over <see cref="SystemDirectories"/>.
    /// </summary>
    public static FontRepository System => system.Value;

    /// <summary>
    /// The repository <see cref="Font.Find"/> searches. <see cref="System"/> unless replaced.
    /// </summary>
    /// <remarks>
    /// Process-wide, which is the point: a test suite that wants every lookup to resolve against
    /// its own fonts sets this once at startup.
    /// </remarks>
    public static FontRepository Default
    {
        get => Volatile.Read(ref @default) ?? System;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Volatile.Write(ref @default, value);
        }
    }

    /// <summary>
    /// The directories this repository scans.
    /// </summary>
    public IReadOnlyList<string> Directories { get; }

    /// <summary>
    /// The family to fall back to when a request matches nothing, with the same style. Null,
    /// the default, makes a failed lookup return null.
    /// </summary>
    public string? FallbackFamily { get; init; }

    /// <summary>
    /// Every face the scan found, in directory order and then file name order. Scans on first
    /// access.
    /// </summary>
    public IReadOnlyList<FontFaceInfo> Faces => Volatile.Read(ref faces).Value;

    /// <summary>
    /// Discards the cached scan, so the next lookup scans again.
    /// </summary>
    public void Refresh() =>
        Volatile.Write(ref faces, NewScan());

    /// <summary>
    /// The face best matching <paramref name="family"/> and the requested style, or null when
    /// neither it nor <see cref="FallbackFamily"/> matches anything.
    /// </summary>
    public FontFaceInfo? FindFace(string family, bool bold = false, bool italic = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);

        var found = Match(family.Trim(), bold, italic);
        if (found != null)
        {
            return found;
        }

        if (string.IsNullOrWhiteSpace(FallbackFamily))
        {
            return null;
        }

        return Match(FallbackFamily.Trim(), bold, italic);
    }

    /// <summary>
    /// Loads the face best matching <paramref name="family"/> and the requested style, or
    /// returns null when nothing matches. See <see cref="FindFace"/>.
    /// </summary>
    /// <remarks>
    /// Each call loads a new <see cref="Font"/>, which the caller owns and disposes. Loading is
    /// the expensive part, so find a font once and share it across every page.
    /// </remarks>
    public Font? Find(string family, bool bold = false, bool italic = false) =>
        FindFace(family, bold, italic)?.Load();

    FontFaceInfo? Match(string family, bool bold, bool italic)
    {
        var all = Faces;
        var (families, impliedBold, impliedItalic) = FontAliases.Resolve(family);
        bold |= impliedBold;
        italic |= impliedItalic;

        foreach (var candidate in families)
        {
            var best = Closest(all, candidate, bold, italic);
            if (best != null)
            {
                return best;
            }
        }

        foreach (var face in all)
        {
            if (face.FullName.Equals(family, StringComparison.OrdinalIgnoreCase) ||
                face.PostScriptName.Equals(family, StringComparison.OrdinalIgnoreCase))
            {
                return face;
            }
        }

        return null;
    }

    static FontFaceInfo? Closest(IReadOnlyList<FontFaceInfo> all, string family, bool bold, bool italic)
    {
        var target = 400;
        if (bold)
        {
            target = 700;
        }

        FontFaceInfo? best = null;
        var bestScore = int.MaxValue;

        foreach (var face in all)
        {
            if (!face.FamilyNames.Contains(family, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            // Slope outranks weight, which is the order a reader notices them in, and the bold
            // flag outranks the weight number, which some older fonts get wrong.
            var score = Math.Abs(face.Weight - target);
            if (face.Bold != bold)
            {
                score += 1_000;
            }

            if (face.Italic != italic)
            {
                score += 10_000;
            }

            // Strictly less, so a tie goes to the face scanned first and the answer is stable.
            if (score < bestScore)
            {
                best = face;
                bestScore = score;
            }
        }

        return best;
    }

    Lazy<IReadOnlyList<FontFaceInfo>> NewScan() =>
        new(Scan, LazyThreadSafetyMode.ExecutionAndPublication);

    IReadOnlyList<FontFaceInfo> Scan()
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            // Bounds a symlink loop, which a recursive walk would otherwise follow forever.
            MaxRecursionDepth = 16,
            AttributesToSkip = FileAttributes.Hidden
        };

        var seen = new HashSet<string>(pathComparer);
        var files = new List<string>();

        foreach (var directory in Directories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            List<string> found;
            try
            {
                found = Directory.EnumerateFiles(directory, "*", options)
                    .Where(_ => extensions.Contains(Path.GetExtension(_), StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            found.Sort(StringComparer.Ordinal);
            files.AddRange(found.Where(seen.Add));
        }

        // Reading is independent per file and dominated by I/O, so it parallelises; each slot
        // keeps its file's position, which is what keeps the result in a stable order.
        var perFile = new List<FontFaceInfo>?[files.Count];
        Parallel.For(0, files.Count, _ =>
        {
            try
            {
                perFile[_] = FontFileReader.Read(files[_]);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                // Not a font, or not one this can read: a font folder holds both.
            }
        });

        return [..perFile.Where(_ => _ != null).SelectMany(_ => _!)];
    }
}
