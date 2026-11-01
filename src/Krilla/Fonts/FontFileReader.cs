using System.Buffers.Binary;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// Reads the naming and style of every face in a font file, without reading the whole file.
/// </summary>
/// <remarks>
/// <para>
/// A scan of the system font folders touches hundreds of files, some of them collections of
/// twenty megabytes and more, and needs a few hundred bytes out of each. So this reads the
/// table directory and then only <c>name</c>, <c>OS/2</c> and <c>head</c>, at their offsets,
/// rather than handing the file to a parser that would want all of it.
/// </para>
/// <para>
/// Every read is bounds-checked against the file, and anything malformed throws
/// <see cref="InvalidDataException"/>: a font folder routinely holds files that are not fonts,
/// or are fonts in formats nothing here reads, and one of them must cost the scan that file and
/// nothing more.
/// </para>
/// </remarks>
static class FontFileReader
{
    const uint collectionTag = 0x74746366; // 'ttcf'

    // A name table is a few kilobytes; a CJK face's can reach a few hundred. Anything past this
    // is a corrupt length rather than a real table, and allocating it would be the failure.
    const int maxTableLength = 4 * 1024 * 1024;

    public static List<FontFaceInfo> Read(string path)
    {
        using var handle = File.OpenHandle(path);
        var length = RandomAccess.GetLength(handle);
        var header = ReadBytes(handle, length, 0, 12);

        var faces = new List<FontFaceInfo>();

        if (BinaryPrimitives.ReadUInt32BigEndian(header) != collectionTag)
        {
            faces.Add(ReadFace(handle, length, path, 0, 0));
            return faces;
        }

        var count = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8));
        // A collection of more than a few hundred faces is a corrupt count.
        if (count is 0 or > 1024)
        {
            throw new InvalidDataException($"The collection declares {count} faces.");
        }

        var offsets = ReadBytes(handle, length, 12, (int) count * 4);

        for (uint index = 0; index < count; index++)
        {
            var offset = BinaryPrimitives.ReadUInt32BigEndian(offsets.AsSpan((int) index * 4));
            faces.Add(ReadFace(handle, length, path, index, offset));
        }

        return faces;
    }

    static FontFaceInfo ReadFace(SafeFileHandle handle, long length, string path, uint index, long start)
    {
        var directory = ReadBytes(handle, length, start, 12);
        var version = BinaryPrimitives.ReadUInt32BigEndian(directory);
        if (version is not (0x00010000 or 0x4F54544F or 0x74727565)) // 1.0, 'OTTO', 'true'
        {
            throw new InvalidDataException("Not a TrueType or OpenType font.");
        }

        var tableCount = BinaryPrimitives.ReadUInt16BigEndian(directory.AsSpan(4));
        var records = ReadBytes(handle, length, start + 12, tableCount * 16);

        byte[]? name = null;
        byte[]? os2 = null;
        byte[]? head = null;

        for (var i = 0; i < tableCount; i++)
        {
            var record = records.AsSpan(i * 16, 16);
            var tag = BinaryPrimitives.ReadUInt32BigEndian(record);
            var offset = BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            var tableLength = BinaryPrimitives.ReadUInt32BigEndian(record[12..]);

            switch (tag)
            {
                case 0x6E616D65: // 'name'
                    name = ReadTable(handle, length, offset, tableLength);
                    break;
                case 0x4F532F32: // 'OS/2'
                    os2 = ReadTable(handle, length, offset, tableLength);
                    break;
                case 0x68656164: // 'head'
                    head = ReadTable(handle, length, offset, tableLength);
                    break;
            }
        }

        if (name == null)
        {
            throw new InvalidDataException("The font has no 'name' table.");
        }

        var names = FontNames.Read(name);
        var (weight, bold, italic) = ReadStyle(os2, head);

        return new(
            names.Family,
            names.Subfamily,
            names.FullName,
            names.PostScriptName,
            names.Families,
            weight,
            bold,
            italic,
            path,
            index);
    }

    /// <summary>
    /// Weight and slope, from <c>OS/2</c> where the font has one and <c>head.macStyle</c> where
    /// it does not.
    /// </summary>
    /// <remarks>
    /// Bold is the font's own flag OR a weight of 600 and up. A family shipping Semibold as its
    /// style-linked bold sets the flag at 600, and a family whose Black face forgets to set it
    /// is still bold by any reading a caller asking for bold has in mind.
    /// </remarks>
    static (int Weight, bool Bold, bool Italic) ReadStyle(byte[]? os2, byte[]? head)
    {
        if (os2 is { Length: >= 64 })
        {
            var weight = (int) BinaryPrimitives.ReadUInt16BigEndian(os2.AsSpan(4));
            var selection = BinaryPrimitives.ReadUInt16BigEndian(os2.AsSpan(62));
            // fsSelection: bit 0 ITALIC, bit 5 BOLD, bit 9 OBLIQUE.
            var italic = (selection & 0x01) != 0 || (selection & 0x200) != 0;
            var bold = (selection & 0x20) != 0 || weight >= 600;
            return (weight, bold, italic);
        }

        if (head is { Length: >= 46 })
        {
            var style = BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(44));
            // macStyle: bit 0 bold, bit 1 italic.
            var bold = (style & 0x01) != 0;
            var weight = 400;
            if (bold)
            {
                weight = 700;
            }

            return (weight, bold, (style & 0x02) != 0);
        }

        return (400, false, false);
    }

    static byte[] ReadTable(SafeFileHandle handle, long length, uint offset, uint tableLength)
    {
        if (tableLength > maxTableLength)
        {
            throw new InvalidDataException($"A table declares a length of {tableLength} bytes.");
        }

        return ReadBytes(handle, length, offset, (int) tableLength);
    }

    static byte[] ReadBytes(SafeFileHandle handle, long length, long offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > length)
        {
            throw new InvalidDataException("A read runs past the end of the file.");
        }

        var buffer = new byte[count];
        var total = 0;

        while (total < count)
        {
            var read = RandomAccess.Read(handle, buffer.AsSpan(total), offset + total);
            if (read == 0)
            {
                throw new InvalidDataException("The file ended early.");
            }

            total += read;
        }

        return buffer;
    }
}
