/// <summary>
/// The font metrics layout needs, read straight out of the font file.
/// </summary>
/// <remarks>
/// <para>
/// This exists because krilla exposes none of it. The C API surfaces exactly one metric —
/// <c>krilla_font_units_per_em</c> — with no cmap lookup, no advance widths and no vertical
/// metrics, so a line cannot be broken using krilla's font handle alone. Everything here is
/// parsed from the same bytes that were handed to <see cref="Font.Load"/>.
/// </para>
/// <para>
/// Deliberately narrow: <c>head</c>, <c>hhea</c>, <c>hmtx</c>, <c>cmap</c>, <c>OS/2</c> and
/// <c>name</c>. No <c>GSUB</c>/<c>GPOS</c>, so no kerning, ligatures or complex-script shaping —
/// text is measured by summing raw <c>hmtx</c> advances. That is a real limit, and the corpus is
/// built so it does not bite: <c>Inputs/reset.css</c> disables kerning and ligatures, which makes
/// a browser's advances the same raw <c>hmtx</c> values this returns. Text needing more than that
/// wants a shaper, and the right one is the rustybuzz krilla already links — exposed through
/// krilla-capi rather than added here as a second shaping implementation.
/// </para>
/// </remarks>
sealed class OpenTypeMetrics
{
    readonly ushort[] advances;
    readonly CharacterMap characters;

    OpenTypeMetrics(
        float unitsPerEm,
        float ascender,
        float descender,
        float lineGap,
        ushort glyphCount,
        int weight,
        bool italic,
        string familyName,
        float underlineOffset,
        float underlineThickness,
        float strikeoutOffset,
        float strikeoutThickness,
        float xHeight,
        ushort[] advances,
        CharacterMap characters)
    {
        UnderlineOffset = underlineOffset;
        UnderlineThickness = underlineThickness;
        StrikeoutOffset = strikeoutOffset;
        StrikeoutThickness = strikeoutThickness;
        XHeight = xHeight;
        UnitsPerEm = unitsPerEm;
        Ascender = ascender;
        Descender = descender;
        LineGap = lineGap;
        GlyphCount = glyphCount;
        Weight = weight;
        Italic = italic;
        FamilyName = familyName;
        this.advances = advances;
        this.characters = characters;
    }

    /// <summary>Design units per em. 2048 for most TrueType fonts, 1000 for most CFF ones.</summary>
    public float UnitsPerEm { get; }

    /// <summary>Distance from the baseline to the top of the em box, in design units.</summary>
    public float Ascender { get; }

    /// <summary>
    /// Distance from the baseline to the bottom of the em box, in design units. Negative, as the
    /// font stores it.
    /// </summary>
    public float Descender { get; }

    /// <summary>Recommended extra leading between lines, in design units.</summary>
    public float LineGap { get; }

    /// <summary>Number of glyphs in the font.</summary>
    public ushort GlyphCount { get; }

    /// <summary>The weight class, 100-900, from <c>OS/2.usWeightClass</c>.</summary>
    public int Weight { get; }

    /// <summary>Whether the font is italic or oblique.</summary>
    public bool Italic { get; }

    /// <summary>The typographic family name, used to match a CSS <c>font-family</c>.</summary>
    public string FamilyName { get; }

    /// <summary>
    /// Distance below the baseline to the top of an underline, in design units.
    /// </summary>
    /// <remarks>
    /// Positive downward, unlike the font's own value, which measures upward and is therefore
    /// negative for every font that puts its underline where one belongs.
    /// </remarks>
    public float UnderlineOffset { get; }

    /// <summary>Underline thickness, in design units.</summary>
    public float UnderlineThickness { get; }

    /// <summary>
    /// Distance from the baseline UP to the bottom of a strike, in design units.
    /// </summary>
    /// <remarks>
    /// From <c>OS/2.yStrikeoutPosition</c>, which is measured upward from the baseline where the
    /// underline's equivalent is measured down — hence the sign difference between this and
    /// <see cref="UnderlineOffset"/>, which negates what it reads.
    /// </remarks>
    public float StrikeoutOffset { get; }

    /// <summary>Strike thickness, in design units.</summary>
    public float StrikeoutThickness { get; }

    /// <summary>
    /// The height of a lower-case <c>x</c>, in design units.
    /// </summary>
    /// <remarks>
    /// From <c>OS/2.sxHeight</c>, which exists from version 2 of the table. Only
    /// <c>vertical-align: middle</c> reads it, and it is what that keyword aligns against: the
    /// midpoint of the aligned box goes half an x-height above the parent's baseline.
    /// </remarks>
    public float XHeight { get; }

    /// <summary>
    /// The glyph for <paramref name="codepoint"/>, or 0 (<c>.notdef</c>) when the font has none.
    /// </summary>
    public ushort GlyphIndex(int codepoint) =>
        characters.Lookup(codepoint);

    /// <summary>
    /// The horizontal advance of <paramref name="glyphId"/>, in design units.
    /// </summary>
    /// <remarks>
    /// <c>hmtx</c> stores advances for the first <c>numberOfHMetrics</c> glyphs only; every glyph
    /// past that shares the last one. Monospace fonts exploit this and ship a single entry, so the
    /// clamp is the normal path there rather than an edge case.
    /// </remarks>
    public float Advance(ushort glyphId)
    {
        if (advances.Length == 0)
        {
            return 0;
        }

        return advances[Math.Min(glyphId, advances.Length - 1)];
    }

    /// <summary>
    /// Parses <paramref name="data"/>, selecting <paramref name="index"/> from a collection.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The bytes are not a font, or a table layout needs is missing.
    /// </exception>
    public static OpenTypeMetrics Read(ReadOnlySpan<byte> data, uint index = 0)
    {
        var tables = ReadTableDirectory(data, index);

        var head = Require(tables, "head");
        var unitsPerEm = (float) ReadUInt16(data, head + 18);
        if (unitsPerEm <= 0)
        {
            throw new InvalidDataException("The font declares a non-positive unitsPerEm.");
        }

        var hhea = Require(tables, "hhea");
        var hheaAscender = (float) ReadInt16(data, hhea + 4);
        var hheaDescender = (float) ReadInt16(data, hhea + 6);
        var hheaLineGap = (float) ReadInt16(data, hhea + 8);
        var metricCount = ReadUInt16(data, hhea + 34);

        var glyphCount = tables.TryGetValue("maxp", out var maxp) ? ReadUInt16(data, maxp + 4) : metricCount;

        var weight = 400;
        var italic = false;
        var ascender = hheaAscender;
        var descender = hheaDescender;
        var lineGap = hheaLineGap;

        // Conventional fallbacks for a font carrying no OS/2 table: a strike halfway up the
        // x-height, itself half an em, and a rule as thick as an underline.
        var strikeoutOffset = unitsPerEm / 4;
        var strikeoutThickness = unitsPerEm / 20;
        var xHeight = unitsPerEm / 2;

        if (tables.TryGetValue("OS/2", out var os2))
        {
            weight = ReadUInt16(data, os2 + 4);
            strikeoutThickness = ReadInt16(data, os2 + 26);
            strikeoutOffset = ReadInt16(data, os2 + 28);

            // sxHeight arrived in version 2, so a version 0 or 1 table keeps the fallback rather
            // than reading whatever follows the table.
            if (ReadUInt16(data, os2) >= 2 && os2 + 88 <= data.Length)
            {
                xHeight = ReadInt16(data, os2 + 86);
            }

            var selection = ReadUInt16(data, os2 + 62);
            // fsSelection bit 0 is ITALIC, bit 9 is OBLIQUE. CSS treats both as font-style: italic.
            italic = (selection & 0x01) != 0 || (selection & 0x200) != 0;

            // Bit 7, USE_TYPO_METRICS, is the font asking for its sTypo* metrics to win over hhea.
            // Honouring it matters: get this wrong and every line-height:normal line box is off by
            // a few percent, which compounds down a page into a wrong page count. Browsers follow
            // the same rule, so following it is what keeps the reference comparison meaningful.
            if ((selection & 0x80) != 0)
            {
                ascender = ReadInt16(data, os2 + 68);
                descender = ReadInt16(data, os2 + 70);
                lineGap = ReadInt16(data, os2 + 72);
            }
        }

        var advances = tables.TryGetValue("hmtx", out var hmtx)
            ? ReadAdvances(data, hmtx, metricCount)
            : [];

        var characters = tables.TryGetValue("cmap", out var cmap)
            ? CharacterMap.Read(data, cmap)
            : CharacterMap.Empty;

        var familyName = tables.TryGetValue("name", out var name) ? ReadFamilyName(data, name) : "";

        // post carries the underline geometry. Defaults are the conventional fallbacks for a
        // font without one: a tenth of an em below the baseline, a twentieth thick.
        var underlineOffset = unitsPerEm / 10;
        var underlineThickness = unitsPerEm / 20;

        if (tables.TryGetValue("post", out var post) && post + 12 <= data.Length)
        {
            underlineOffset = -ReadInt16(data, post + 8);
            underlineThickness = ReadInt16(data, post + 10);
        }

        return new(
            unitsPerEm,
            ascender,
            descender,
            lineGap,
            glyphCount,
            weight,
            italic,
            familyName,
            underlineOffset,
            underlineThickness,
            strikeoutOffset,
            strikeoutThickness,
            xHeight,
            advances,
            characters);
    }

    /// <summary>Parses a font file from disk.</summary>
    public static OpenTypeMetrics ReadFile(string path, uint index = 0) =>
        Read(File.ReadAllBytes(path), index);

    static ushort[] ReadAdvances(ReadOnlySpan<byte> data, int offset, ushort metricCount)
    {
        var advances = new ushort[metricCount];

        for (var i = 0; i < metricCount; i++)
        {
            // Each longHorMetric is a uint16 advance followed by an int16 left side bearing; only
            // the advance is needed to lay out a line.
            advances[i] = ReadUInt16(data, offset + i * 4);
        }

        return advances;
    }

    /// <summary>
    /// The family name from the <c>name</c> table, preferring the typographic family (id 16) over
    /// the legacy one (id 1).
    /// </summary>
    /// <remarks>
    /// The distinction is load-bearing for families with more than four faces: a legacy family
    /// name splits them into "Foo" and "Foo Light" so the four-face style-linking limit is not
    /// exceeded, while the typographic name keeps the whole family as "Foo". CSS matches the
    /// latter, so preferring id 16 is what makes <c>font-weight: 300</c> resolve.
    /// </remarks>
    static string ReadFamilyName(ReadOnlySpan<byte> data, int offset)
    {
        var count = ReadUInt16(data, offset + 2);
        var storage = offset + ReadUInt16(data, offset + 4);

        var legacy = "";
        var typographic = "";

        for (var i = 0; i < count; i++)
        {
            var record = offset + 6 + i * 12;
            var nameId = ReadUInt16(data, record + 6);
            if (nameId != 1 && nameId != 16)
            {
                continue;
            }

            var platformId = ReadUInt16(data, record);
            var length = ReadUInt16(data, record + 8);
            var stringOffset = storage + ReadUInt16(data, record + 10);
            if (stringOffset + length > data.Length)
            {
                continue;
            }

            var bytes = data.Slice(stringOffset, length);
            // Platform 1 is Macintosh Roman, single byte. Everything else here is UTF-16BE.
            var value = platformId == 1
                ? Encoding.Latin1.GetString(bytes)
                : Encoding.BigEndianUnicode.GetString(bytes);

            if (value.Length == 0)
            {
                continue;
            }

            if (nameId == 16)
            {
                typographic = value;
            }
            else if (legacy.Length == 0)
            {
                legacy = value;
            }
        }

        if (typographic.Length > 0)
        {
            return typographic;
        }

        return legacy;
    }

    static Dictionary<string, int> ReadTableDirectory(ReadOnlySpan<byte> data, uint index)
    {
        if (data.Length < 12)
        {
            throw new InvalidDataException("The data is too short to be a font.");
        }

        var start = 0;

        if (ReadUInt32(data, 0) == 0x74746366) // 'ttcf'
        {
            var fontCount = ReadUInt32(data, 8);
            if (index >= fontCount)
            {
                throw new InvalidDataException(
                    $"The collection holds {fontCount} fonts; index {index} is out of range.");
            }

            start = (int) ReadUInt32(data, 12 + (int) index * 4);
        }
        else if (index != 0)
        {
            throw new InvalidDataException("The data is a single font, so only index 0 exists.");
        }

        var version = ReadUInt32(data, start);
        if (version is not (0x00010000 or 0x4F54544F or 0x74727565)) // 1.0, 'OTTO', 'true'
        {
            throw new InvalidDataException("The data is not a TrueType or OpenType font.");
        }

        var tableCount = ReadUInt16(data, start + 4);
        var tables = new Dictionary<string, int>(tableCount, StringComparer.Ordinal);

        for (var i = 0; i < tableCount; i++)
        {
            var record = start + 12 + i * 16;
            if (record + 16 > data.Length)
            {
                break;
            }

            var tag = Encoding.ASCII.GetString(data.Slice(record, 4));
            tables[tag] = (int) ReadUInt32(data, record + 8);
        }

        return tables;
    }

    static int Require(Dictionary<string, int> tables, string tag)
    {
        if (tables.TryGetValue(tag, out var offset))
        {
            return offset;
        }

        throw new InvalidDataException($"The font has no '{tag}' table.");
    }

    internal static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset) =>
        (ushort) ((data[offset] << 8) | data[offset + 1]);

    internal static short ReadInt16(ReadOnlySpan<byte> data, int offset) =>
        (short) ((data[offset] << 8) | data[offset + 1]);

    internal static uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
        ((uint) data[offset] << 24) |
        ((uint) data[offset + 1] << 16) |
        ((uint) data[offset + 2] << 8) |
        data[offset + 3];
}
