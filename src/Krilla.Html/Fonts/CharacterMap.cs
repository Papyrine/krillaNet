namespace Krilla.Html.Fonts;

/// <summary>
/// A codepoint-to-glyph mapping, read from a font's <c>cmap</c> table.
/// </summary>
/// <remarks>
/// Formats 4 and 12 only. Between them they cover every modern font: 4 maps the BMP and is what
/// almost every font ships, 12 extends past U+FFFF and appears alongside 4 when a font carries
/// emoji or historic scripts. The older byte-oriented formats (0, 2, 6) are not read — a font
/// offering only those is decades old and out of scope.
/// </remarks>
sealed class CharacterMap
{
    readonly Segment[] segments;

    CharacterMap(Segment[] segments) =>
        this.segments = segments;

    /// <summary>A map with no entries. Every lookup returns <c>.notdef</c>.</summary>
    public static CharacterMap Empty { get; } = new([]);

    /// <summary>
    /// The glyph for <paramref name="codepoint"/>, or 0 when the font does not cover it.
    /// </summary>
    public ushort Lookup(int codepoint)
    {
        // Segments are sorted and non-overlapping, so a binary search settles it in a few steps.
        // Worth it: this runs once per character of every string measured.
        var low = 0;
        var high = segments.Length - 1;

        while (low <= high)
        {
            var middle = (low + high) / 2;
            var segment = segments[middle];

            if (codepoint < segment.Start)
            {
                high = middle - 1;
            }
            else if (codepoint > segment.End)
            {
                low = middle + 1;
            }
            else
            {
                return segment.GlyphAt(codepoint);
            }
        }

        return 0;
    }

    /// <summary>
    /// Reads the best subtable at <paramref name="offset"/>.
    /// </summary>
    public static CharacterMap Read(ReadOnlySpan<byte> data, int offset)
    {
        var subtable = SelectSubtable(data, offset);
        if (subtable < 0)
        {
            return Empty;
        }

        var format = OpenTypeMetrics.ReadUInt16(data, subtable);
        var segments = format switch
        {
            4 => ReadFormat4(data, subtable),
            12 => ReadFormat12(data, subtable),
            _ => []
        };

        segments.Sort((left, right) => left.Start.CompareTo(right.Start));
        return new([.. segments]);
    }

    /// <summary>
    /// Picks the encoding record to read, preferring the widest Unicode coverage available.
    /// </summary>
    /// <remarks>
    /// Order: Windows UCS-4 (3,10), Windows BMP (3,1), any Unicode platform (0,*), then Windows
    /// Symbol (3,0). Symbol is last because it maps characters into the U+F000 private use block
    /// rather than at their real codepoints, so it is a fallback rather than a choice.
    /// </remarks>
    static int SelectSubtable(ReadOnlySpan<byte> data, int offset)
    {
        var count = OpenTypeMetrics.ReadUInt16(data, offset + 2);
        var best = -1;
        var bestRank = int.MaxValue;

        for (var i = 0; i < count; i++)
        {
            var record = offset + 4 + i * 8;
            if (record + 8 > data.Length)
            {
                break;
            }

            var platformId = OpenTypeMetrics.ReadUInt16(data, record);
            var encodingId = OpenTypeMetrics.ReadUInt16(data, record + 2);
            var subtable = offset + (int) OpenTypeMetrics.ReadUInt32(data, record + 4);
            if (subtable + 4 > data.Length)
            {
                continue;
            }

            var rank = (platformId, encodingId) switch
            {
                (3, 10) => 0,
                (3, 1) => 1,
                (0, _) => 2,
                (3, 0) => 3,
                _ => int.MaxValue
            };

            if (rank < bestRank)
            {
                bestRank = rank;
                best = subtable;
            }
        }

        return best;
    }

    static List<Segment> ReadFormat4(ReadOnlySpan<byte> data, int offset)
    {
        var segCountX2 = OpenTypeMetrics.ReadUInt16(data, offset + 6);
        var segCount = segCountX2 / 2;

        var endCodes = offset + 14;
        // +2 skips reservedPad
        var startCodes = endCodes + segCountX2 + 2;
        var idDeltas = startCodes + segCountX2;
        var idRangeOffsets = idDeltas + segCountX2;

        var segments = new List<Segment>(segCount);

        for (var i = 0; i < segCount; i++)
        {
            var end = OpenTypeMetrics.ReadUInt16(data, endCodes + i * 2);
            var start = OpenTypeMetrics.ReadUInt16(data, startCodes + i * 2);
            if (start > end)
            {
                continue;
            }

            // The final segment is a required 0xFFFF..0xFFFF terminator, not real coverage.
            if (start == 0xFFFF)
            {
                continue;
            }

            var delta = OpenTypeMetrics.ReadInt16(data, idDeltas + i * 2);
            var rangeOffsetPosition = idRangeOffsets + i * 2;
            var rangeOffset = OpenTypeMetrics.ReadUInt16(data, rangeOffsetPosition);

            if (rangeOffset == 0)
            {
                segments.Add(Segment.Delta(start, end, delta));
                continue;
            }

            // A non-zero idRangeOffset is a byte offset from its own slot into glyphIdArray. That
            // self-relative addressing is the format's one genuine oddity; resolve it here so the
            // lookup path stays a plain array read.
            var glyphs = new ushort[end - start + 1];
            for (var code = start; code <= end; code++)
            {
                var position = rangeOffsetPosition + rangeOffset + (code - start) * 2;
                if (position + 2 > data.Length)
                {
                    break;
                }

                var glyph = OpenTypeMetrics.ReadUInt16(data, position);
                // Zero means "no glyph" and must not have the delta applied to it.
                glyphs[code - start] = glyph == 0 ? (ushort) 0 : (ushort) ((glyph + delta) & 0xFFFF);
            }

            segments.Add(Segment.Table(start, end, glyphs));
        }

        return segments;
    }

    static List<Segment> ReadFormat12(ReadOnlySpan<byte> data, int offset)
    {
        var groupCount = (int) OpenTypeMetrics.ReadUInt32(data, offset + 12);
        var segments = new List<Segment>(groupCount);

        for (var i = 0; i < groupCount; i++)
        {
            var group = offset + 16 + i * 12;
            if (group + 12 > data.Length)
            {
                break;
            }

            var start = (int) OpenTypeMetrics.ReadUInt32(data, group);
            var end = (int) OpenTypeMetrics.ReadUInt32(data, group + 4);
            var startGlyph = (int) OpenTypeMetrics.ReadUInt32(data, group + 8);
            if (start > end)
            {
                continue;
            }

            // Format 12 groups are already contiguous runs, which is exactly what a delta segment
            // is: glyph = codepoint - start + startGlyph.
            segments.Add(Segment.Delta(start, end, (short) (startGlyph - start)));
        }

        return segments;
    }

    /// <summary>
    /// A contiguous codepoint range, mapped either by a constant offset or by an explicit table.
    /// </summary>
    readonly struct Segment
    {
        readonly ushort[]? glyphs;
        readonly short delta;

        Segment(int start, int end, short delta, ushort[]? glyphs)
        {
            Start = start;
            End = end;
            this.delta = delta;
            this.glyphs = glyphs;
        }

        public int Start { get; }

        public int End { get; }

        public static Segment Delta(int start, int end, short delta) =>
            new(start, end, delta, null);

        public static Segment Table(int start, int end, ushort[] glyphs) =>
            new(start, end, 0, glyphs);

        public ushort GlyphAt(int codepoint)
        {
            if (glyphs is not null)
            {
                var index = codepoint - Start;
                return index < glyphs.Length ? glyphs[index] : (ushort) 0;
            }

            return (ushort) ((codepoint + delta) & 0xFFFF);
        }
    }
}
