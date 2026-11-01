using System.Buffers.Binary;

/// <summary>
/// A font directory built from the pinned Liberation fonts, laid out to exercise a scan.
/// </summary>
/// <remarks>
/// <para>
/// Liberation Sans as four loose files at the top; Liberation Serif's regular and bold in a
/// subdirectory, so the scan has to recurse; Liberation Mono's regular, bold and italic packed
/// into one <c>.ttc</c>, and no bold italic, so a request for one has to settle for the closest;
/// and a <c>.ttf</c> that is not a font, which the scan has to step over.
/// </para>
/// <para>
/// The collection is assembled here rather than committed, since no freely licensed one is
/// small enough to carry. Built once per process, and rebuilt every run so it cannot go stale.
/// </para>
/// </remarks>
static class TestFonts
{
    public static string Source { get; } = Path.Combine(AppContext.BaseDirectory, "Fonts");

    static readonly Lazy<string> directory = new(Build);

    public static string Directory => directory.Value;

    public static FontRepository Repository() =>
        new(Directory);

    public static Font LoadSans() =>
        Font.LoadFile(Path.Combine(Source, "LiberationSans-Regular.ttf"));

    static string Build()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "font-repository");
        if (System.IO.Directory.Exists(root))
        {
            System.IO.Directory.Delete(root, true);
        }

        var serif = Path.Combine(root, "serif");
        System.IO.Directory.CreateDirectory(serif);

        foreach (var style in new[] { "Regular", "Bold", "Italic", "BoldItalic" })
        {
            Copy($"LiberationSans-{style}.ttf", root);
        }

        Copy("LiberationSerif-Regular.ttf", serif);
        Copy("LiberationSerif-Bold.ttf", serif);

        File.WriteAllBytes(
            Path.Combine(root, "LiberationMono.ttc"),
            Collection(
                File.ReadAllBytes(Path.Combine(Source, "LiberationMono-Regular.ttf")),
                File.ReadAllBytes(Path.Combine(Source, "LiberationMono-Bold.ttf")),
                File.ReadAllBytes(Path.Combine(Source, "LiberationMono-Italic.ttf"))));

        File.WriteAllText(Path.Combine(root, "not-a-font.ttf"), "this is not a font");

        return root;
    }

    static void Copy(string file, string target) =>
        File.Copy(Path.Combine(Source, file), Path.Combine(target, file));

    /// <summary>
    /// Packs single fonts into a TrueType collection.
    /// </summary>
    /// <remarks>
    /// A table's offset is measured from the start of the FILE, not of its font, so each font's
    /// table directory is rewritten to account for where the font now starts. Checksums are left
    /// as they were; nothing reading a font for its names or its outlines checks them.
    /// </remarks>
    static byte[] Collection(params byte[][] fonts)
    {
        var header = 12 + fonts.Length * 4;
        var offsets = new int[fonts.Length];
        var length = header;

        for (var i = 0; i < fonts.Length; i++)
        {
            offsets[i] = length;
            length += (fonts[i].Length + 3) & ~3;
        }

        var collection = new byte[length];
        BinaryPrimitives.WriteUInt32BigEndian(collection, 0x74746366); // 'ttcf'
        BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(8), (uint) fonts.Length);

        for (var i = 0; i < fonts.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(12 + i * 4), (uint) offsets[i]);

            var font = collection.AsSpan(offsets[i], fonts[i].Length);
            fonts[i].CopyTo(font);

            var tables = BinaryPrimitives.ReadUInt16BigEndian(font[4..]);
            for (var table = 0; table < tables; table++)
            {
                var field = font.Slice(12 + table * 16 + 8, 4);
                var offset = BinaryPrimitives.ReadUInt32BigEndian(field);
                BinaryPrimitives.WriteUInt32BigEndian(field, offset + (uint) offsets[i]);
            }
        }

        return collection;
    }
}
