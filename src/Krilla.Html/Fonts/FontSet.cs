namespace Krilla.Html.Fonts;

/// <summary>
/// The fonts available to a conversion, and the matching rules that pick one.
/// </summary>
/// <remarks>
/// <para>
/// krilla has no font database — it does not enumerate installed fonts and does not match on
/// family or style — so a caller supplies the faces and this resolves CSS font properties against
/// them. Registering nothing is a configuration error rather than a silent fallback to whatever
/// the host has installed: reproducibility is the whole reason the corpus can be compared to a
/// browser at all.
/// </para>
/// <para>
/// Not thread safe, and neither is a <see cref="FontFace"/>: both are tied to the document being
/// built.
/// </para>
/// </remarks>
public sealed class FontSet :
    IDisposable
{
    readonly Dictionary<string, List<FontFace>> families = new(StringComparer.OrdinalIgnoreCase);
    readonly List<FontFace> owned = [];

    /// <summary>
    /// The face whose family is used when nothing else matches. Defaults to the first face
    /// registered, or to a regular upright one when <see cref="AddDirectory"/> supplied them.
    /// </summary>
    /// <remarks>
    /// Its FAMILY rather than the face itself, so weight and style still resolve against it: an
    /// element asking for bold gets this family's bold face when one is registered. Setting this to
    /// a bold or an italic face therefore selects that family and not that face, which is what a
    /// default font means — a document does not become entirely bold because its default happened
    /// to be named by a bold face. A face whose family holds nothing else is returned as itself.
    /// </remarks>
    public FontFace? Fallback { get; set; }

    /// <summary>
    /// The family a generic <c>serif</c> resolves to. Unset means "fall through to
    /// <see cref="Fallback"/>".
    /// </summary>
    public string? Serif { get; set; }

    /// <summary>The family a generic <c>sans-serif</c> resolves to.</summary>
    public string? SansSerif { get; set; }

    /// <summary>The family a generic <c>monospace</c> resolves to.</summary>
    public string? Monospace { get; set; }

    /// <summary>
    /// Registers a face, taking ownership so it is disposed with this set.
    /// </summary>
    public FontSet Add(FontFace face)
    {
        owned.Add(face);
        AddUnowned(face);
        return this;
    }

    /// <summary>
    /// Registers a face without taking ownership, for a face shared across conversions.
    /// </summary>
    public FontSet AddUnowned(FontFace face)
    {
        if (!families.TryGetValue(face.Family, out var faces))
        {
            faces = [];
            families[face.Family] = faces;
        }

        faces.Add(face);
        Fallback ??= face;
        return this;
    }

    /// <summary>Registers every font file in <paramref name="directory"/>.</summary>
    /// <remarks>
    /// Enumerated in a fixed order so nothing depends on the order the file system happens to
    /// return, and the <see cref="Fallback"/> is then taken to be a regular upright face rather
    /// than whichever file sorted first. A directory holding <c>Bold</c> ahead of <c>Regular</c>
    /// alphabetically — which is most of them — would otherwise name the document's default with a
    /// bold face.
    /// </remarks>
    public FontSet AddDirectory(string directory)
    {
        // Only when the caller has not already chosen one, so a directory added after an explicit
        // Fallback does not silently take it over.
        var chooseFallback = Fallback is null;

        var files = Directory.EnumerateFiles(directory)
            .Where(IsFontFile)
            .OrderBy(_ => _, StringComparer.OrdinalIgnoreCase);

        var added = new List<FontFace>();

        foreach (var file in files)
        {
            var face = FontFace.LoadFile(file);
            Add(face);
            added.Add(face);
        }

        if (chooseFallback && added.Count > 0)
        {
            Fallback = Select(added, 400, italic: false);
        }

        return this;
    }

    static bool IsFontFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".otf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves a CSS font-family list against the registered faces.
    /// </summary>
    /// <param name="familyList">
    /// Families in preference order, as <c>font-family</c> lists them.
    /// </param>
    /// <param name="weight">The desired weight, 1-1000.</param>
    /// <param name="italic">Whether an italic face is wanted.</param>
    /// <exception cref="InvalidOperationException">No faces are registered.</exception>
    public FontFace Resolve(IReadOnlyList<string> familyList, int weight, bool italic)
    {
        foreach (var requested in familyList)
        {
            var family = ResolveGeneric(requested);
            if (family is not null && families.TryGetValue(family, out var faces))
            {
                return Select(faces, weight, italic);
            }
        }

        var fallback = Fallback ??
                       throw new InvalidOperationException(
                           "No fonts are registered. Krilla has no font database, so a FontSet " +
                           "must be populated before HTML can be converted.");

        // Its family, not the face itself. Returning the face loses weight and style for every
        // element that reaches here — and in a document setting no font-family anywhere that is
        // every element, so an unstyled <h1> came out unbolded and an <em> upright.
        return families.TryGetValue(fallback.Family, out var fallbackFaces)
            ? Select(fallbackFaces, weight, italic)
            : fallback;
    }

    string? ResolveGeneric(string family) =>
        family.ToLowerInvariant() switch
        {
            "serif" => Serif,
            "sans-serif" => SansSerif,
            "monospace" => Monospace,
            // The remaining generics have no distinct faces here, so they take the closest thing
            // registered rather than failing the whole family list.
            "ui-serif" => Serif,
            "ui-sans-serif" or "system-ui" => SansSerif,
            "ui-monospace" => Monospace,
            "cursive" or "fantasy" => null,
            _ => family
        };

    /// <summary>
    /// Picks the face closest to <paramref name="weight"/> and <paramref name="italic"/>.
    /// </summary>
    /// <remarks>
    /// Style is matched before weight, matching CSS Fonts 4's ordering: a request for bold italic
    /// takes a regular italic over a non-italic bold, because a wrong slant reads as a different
    /// face while a wrong weight reads as the same face rendered lighter.
    /// </remarks>
    static FontFace Select(List<FontFace> faces, int weight, bool italic)
    {
        var candidates = faces.Where(_ => _.Italic == italic).ToList();
        if (candidates.Count == 0)
        {
            candidates = faces;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        return candidates
            .OrderBy(_ => WeightDistance(weight, _.Weight))
            .First();
    }

    /// <summary>
    /// How far <paramref name="available"/> is from <paramref name="desired"/> under the CSS
    /// Fonts 4 weight-matching rules.
    /// </summary>
    /// <remarks>
    /// Not a plain absolute difference. The spec searches in a direction that depends on the
    /// desired weight, and the 400/500 pair is special-cased in both directions. Encoding that as
    /// a distance keeps the caller a single ordered pick: within a search direction the ranking is
    /// by nearness, and the two directions are separated by a large constant so the preferred one
    /// always wins outright.
    /// </remarks>
    static int WeightDistance(int desired, int available)
    {
        const int wrongDirection = 10_000;

        if (available == desired)
        {
            return 0;
        }

        // 400 looks to 500 first, and 500 looks to 400 first, before either searches downward.
        if (desired == 400 && available == 500)
        {
            return 1;
        }

        if (desired == 500 && available == 400)
        {
            return 1;
        }

        // At or below 500 the search runs downward first, then upward. Above 500 it runs upward
        // first, then downward.
        var preferLighter = desired <= 500;
        var isLighter = available < desired;

        var distance = Math.Abs(available - desired);
        return isLighter == preferLighter ? distance : wrongDirection + distance;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var face in owned)
        {
            face.Dispose();
        }

        owned.Clear();
        families.Clear();
        Fallback = null;
    }
}
