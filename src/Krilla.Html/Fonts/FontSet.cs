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
    /// The family names in registration order, which is the order a coverage search walks them in.
    /// </summary>
    /// <remarks>
    /// Kept alongside the dictionary rather than read out of it, because a dictionary's order is
    /// an implementation detail and the answer to "which face draws this character" has to be the
    /// same on two machines or the corpus stops meaning anything.
    /// </remarks>
    readonly List<string> order = [];

    /// <summary>
    /// The face used when nothing else matches. Defaults to the first face registered.
    /// </summary>
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
    /// Every registered face, in no particular order.
    /// </summary>
    /// <remarks>
    /// For <see cref="SvgOptions"/>, which needs the font FILES rather than a resolved face:
    /// usvg matches families itself while it parses, so the choice this class exists to make is
    /// one it has to make again on its own terms. A face registered under two families is
    /// yielded twice, which costs usvg a duplicate database entry and nothing else.
    /// </remarks>
    internal IEnumerable<FontFace> Faces => families.Values.SelectMany(_ => _);

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
            order.Add(face.Family);
        }

        faces.Add(face);
        Fallback ??= face;
        return this;
    }

    /// <summary>Registers every font file in <paramref name="directory"/>.</summary>
    /// <remarks>
    /// Enumerated in a fixed order so the fallback — the first face registered — does not depend
    /// on the order the file system happens to return.
    /// </remarks>
    public FontSet AddDirectory(string directory)
    {
        var files = Directory.EnumerateFiles(directory)
            .Where(IsFontFile)
            .OrderBy(_ => _, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            Add(FontFace.LoadFile(file));
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

        return Fallback ??
               throw new InvalidOperationException(
                   "No fonts are registered. Krilla has no font database, so a FontSet must be " +
                   "populated before HTML can be converted.");
    }

    /// <summary>
    /// A face covering <paramref name="codepoint"/>, preferring <paramref name="primary"/>.
    /// </summary>
    /// <param name="familyList">Families in preference order, as <c>font-family</c> lists them.</param>
    /// <param name="weight">The desired weight, 1-1000.</param>
    /// <param name="italic">Whether an italic face is wanted.</param>
    /// <param name="codepoint">The character that has to be drawn.</param>
    /// <param name="primary">The face family resolution already chose.</param>
    /// <remarks>
    /// <para>
    /// <see cref="Resolve"/> answers a font-family list and nothing else, so a character the
    /// resolved face lacks used to be drawn as <c>.notdef</c> — a document in Greek set in a face
    /// with no Greek came out as a row of boxes with nothing to say why. This is the coverage half
    /// of the same question, asked per character rather than per element.
    /// </para>
    /// <para>
    /// The rest of the element's OWN family list is searched first, which is what a font stack is
    /// for: an author naming three families has said, in order, which face should answer for a
    /// character the first one lacks. Only then does it fall through to everything registered,
    /// in registration order, so that two machines with the same set answer alike.
    /// </para>
    /// <para>
    /// The primary comes back when nothing covers it, which keeps the <c>.notdef</c> in the face
    /// the document asked for rather than in whichever face happened to be looked at last.
    /// </para>
    /// </remarks>
    public FontFace Covering(
        IReadOnlyList<string> familyList,
        int weight,
        bool italic,
        int codepoint,
        FontFace primary)
    {
        if (primary.Covers(codepoint))
        {
            return primary;
        }

        foreach (var requested in familyList)
        {
            if (ResolveGeneric(requested) is {} family &&
                families.TryGetValue(family, out var faces) &&
                Select(faces, weight, italic) is {} candidate &&
                candidate.Covers(codepoint))
            {
                return candidate;
            }
        }

        foreach (var family in order)
        {
            if (Select(families[family], weight, italic) is {} candidate &&
                candidate.Covers(codepoint))
            {
                return candidate;
            }
        }

        return primary;
    }

    /// <summary>
    /// Whether ANY registered face can draw <paramref name="codepoint"/>.
    /// </summary>
    /// <remarks>
    /// The question <see cref="Covering"/> cannot answer, because it returns the primary face when
    /// nothing covers a character and a caller cannot tell that from a hit. False here means the
    /// character will be drawn as <c>.notdef</c> whatever family the document names — which is a
    /// whole-word difference nothing on the page explains, so it is reported.
    /// </remarks>
    public bool AnyCovers(int codepoint)
    {
        foreach (var family in order)
        {
            foreach (var face in families[family])
            {
                if (face.Covers(codepoint))
                {
                    return true;
                }
            }
        }

        return false;
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
        if (isLighter == preferLighter)
        {
            return distance;
        }

        return wrongDirection + distance;
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
        order.Clear();
        Fallback = null;
    }
}
