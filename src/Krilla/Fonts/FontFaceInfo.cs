namespace Krilla;

/// <summary>
/// One face found by a <see cref="FontRepository"/> scan: its names, its style, and where its
/// bytes are.
/// </summary>
/// <remarks>
/// Describes the face without loading it. <see cref="Load"/> does that, and the result is the
/// caller's to dispose.
/// </remarks>
public sealed class FontFaceInfo
{
    internal FontFaceInfo(
        string family,
        string subfamily,
        string fullName,
        string postScriptName,
        IReadOnlyList<string> familyNames,
        int weight,
        bool bold,
        bool italic,
        string path,
        uint index)
    {
        Family = family;
        Subfamily = subfamily;
        FullName = fullName;
        PostScriptName = postScriptName;
        FamilyNames = familyNames;
        Weight = weight;
        Bold = bold;
        Italic = italic;
        Path = path;
        Index = index;
    }

    /// <summary>
    /// The English family name: the typographic family (name id 16) where the font has one,
    /// otherwise the legacy family (id 1).
    /// </summary>
    public string Family { get; }

    /// <summary>
    /// The English style name, such as <c>Bold Italic</c>: name id 17, otherwise id 2.
    /// </summary>
    public string Subfamily { get; }

    /// <summary>The full name (id 4), such as <c>Arial Bold Italic</c>. Empty when absent.</summary>
    public string FullName { get; }

    /// <summary>The PostScript name (id 6), such as <c>Arial-BoldItalicMT</c>. Empty when absent.</summary>
    public string PostScriptName { get; }

    /// <summary>
    /// Every family name the face answers to: typographic and legacy, in every language the
    /// font carries. <see cref="Family"/> is first.
    /// </summary>
    public IReadOnlyList<string> FamilyNames { get; }

    /// <summary>The weight class, 100-900, from <c>OS/2.usWeightClass</c>. 400 is regular and 700 bold.</summary>
    public int Weight { get; }

    /// <summary>
    /// Whether the face is bold: the font's own bold flag, or a weight of 600 or more.
    /// </summary>
    public bool Bold { get; }

    /// <summary>Whether the face is italic or oblique.</summary>
    public bool Italic { get; }

    /// <summary>The font file.</summary>
    public string Path { get; }

    /// <summary>
    /// The face's index within <see cref="Path"/>: its position in a <c>.ttc</c> or <c>.otc</c>
    /// collection, and zero for a single-font file. Passed to <see cref="Font.Load"/>.
    /// </summary>
    public uint Index { get; }

    /// <summary>
    /// Loads the face. The caller owns the result and should share it across every page that
    /// draws with it.
    /// </summary>
    public Font Load() =>
        Font.LoadFile(Path, Index);

    /// <inheritdoc />
    public override string ToString()
    {
        var name = FullName;
        if (name.Length == 0)
        {
            name = $"{Family} {Subfamily}";
        }

        if (Index == 0)
        {
            return $"{name} ({Path})";
        }

        return $"{name} ({Path}#{Index})";
    }
}
