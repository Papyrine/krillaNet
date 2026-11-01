using System.Buffers.Binary;

/// <summary>
/// The names a face carries in its <c>name</c> table.
/// </summary>
/// <remarks>
/// <para>
/// A name record exists per platform, per encoding and per language, so a face has many
/// spellings of the same name. The ones reported are the English ones — Windows platform,
/// language 0x0409, ahead of any other Windows language, ahead of Unicode, ahead of Macintosh —
/// and every family spelling in every language is kept beside them for matching, so a
/// Japanese face can be found by its Japanese name as well as by its English one.
/// </para>
/// <para>
/// The typographic family (id 16) is preferred over the legacy family (id 1), and the
/// typographic subfamily (17) over the legacy subfamily (2). The distinction is load-bearing
/// for a family of more than four faces: the legacy name splits them into "Segoe UI" and
/// "Segoe UI Light" to fit the four-face style-linking model, while the typographic name keeps
/// them one family. Both are kept for matching, so either spelling finds the face.
/// </para>
/// </remarks>
sealed class FontNames
{
    const ushort legacyFamily = 1;
    const ushort legacySubfamily = 2;
    const ushort fullName = 4;
    const ushort postScriptName = 6;
    const ushort typographicFamily = 16;
    const ushort typographicSubfamily = 17;

    FontNames(string family, string subfamily, string fullName, string postScriptName, string[] families)
    {
        Family = family;
        Subfamily = subfamily;
        FullName = fullName;
        PostScriptName = postScriptName;
        Families = families;
    }

    public string Family { get; }
    public string Subfamily { get; }
    public string FullName { get; }
    public string PostScriptName { get; }
    public string[] Families { get; }

    public static FontNames Read(ReadOnlySpan<byte> table)
    {
        if (table.Length < 6)
        {
            throw new InvalidDataException("The 'name' table is truncated.");
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(table[2..]);
        var storage = BinaryPrimitives.ReadUInt16BigEndian(table[4..]);

        // Per name id, the best-ranked value seen so far.
        var best = new Dictionary<ushort, (int Rank, string Value)>();
        var families = new List<string>();

        for (var i = 0; i < count; i++)
        {
            var record = 6 + i * 12;
            if (record + 12 > table.Length)
            {
                break;
            }

            var platform = BinaryPrimitives.ReadUInt16BigEndian(table[record..]);
            var encoding = BinaryPrimitives.ReadUInt16BigEndian(table[(record + 2)..]);
            var language = BinaryPrimitives.ReadUInt16BigEndian(table[(record + 4)..]);
            var nameId = BinaryPrimitives.ReadUInt16BigEndian(table[(record + 6)..]);
            var length = BinaryPrimitives.ReadUInt16BigEndian(table[(record + 8)..]);
            var offset = storage + BinaryPrimitives.ReadUInt16BigEndian(table[(record + 10)..]);

            if (nameId is not (legacyFamily or legacySubfamily or fullName or postScriptName or typographicFamily or typographicSubfamily) ||
                offset + length > table.Length)
            {
                continue;
            }

            var value = Decode(platform, encoding, table.Slice(offset, length));
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            value = value.Trim();

            if (nameId is legacyFamily or typographicFamily &&
                !families.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                families.Add(value);
            }

            var rank = Rank(platform, language);
            if (!best.TryGetValue(nameId, out var current) ||
                rank < current.Rank)
            {
                best[nameId] = (rank, value);
            }
        }

        string Get(ushort id)
        {
            if (best.TryGetValue(id, out var entry))
            {
                return entry.Value;
            }

            return "";
        }

        var family = Get(typographicFamily);
        if (family.Length == 0)
        {
            family = Get(legacyFamily);
        }

        var subfamily = Get(typographicSubfamily);
        if (subfamily.Length == 0)
        {
            subfamily = Get(legacySubfamily);
        }

        if (family.Length == 0)
        {
            throw new InvalidDataException("The font names no family.");
        }

        // The reported family first, so the list leads with the name a caller would expect.
        families.Remove(family);
        families.Insert(0, family);

        return new(family, subfamily, Get(fullName), Get(postScriptName), [..families]);
    }

    /// <summary>
    /// Lower is better: English on Windows, any Windows language, Unicode, then Macintosh.
    /// </summary>
    static int Rank(ushort platform, ushort language) =>
        (platform, language) switch
        {
            (3, 0x0409) => 0,
            (3, _) => 1,
            (0, _) => 2,
            (1, 0) => 3,
            _ => 4
        };

    /// <summary>
    /// The record's string, or null for an encoding nothing here can read.
    /// </summary>
    /// <remarks>
    /// Unicode and Windows records are UTF-16BE whatever the encoding id says, symbol fonts
    /// included. Macintosh Roman is read as Latin-1, which differs only above 0x7F — and an
    /// English family name almost never strays there. The other Macintosh encodings are
    /// legacy CJK code pages that every face carrying them also spells in UTF-16.
    /// </remarks>
    static string? Decode(ushort platform, ushort encoding, ReadOnlySpan<byte> bytes)
    {
        if (platform is 0 or 3)
        {
            return Encoding.BigEndianUnicode.GetString(bytes);
        }

        if (platform == 1 && encoding == 0)
        {
            return Encoding.Latin1.GetString(bytes);
        }

        return null;
    }
}
