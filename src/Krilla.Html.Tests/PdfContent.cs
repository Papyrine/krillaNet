/// <summary>
/// The drawing operators a PDF's content streams hold, and whether each is inside a marked-content
/// span.
/// </summary>
/// <remarks>
/// <para>
/// For the one question <see cref="TaggedPdfTests"/> cannot ask any other way: PDF/UA requires
/// every operator that puts ink on the page to be inside either a structure element or an
/// artifact, and nothing in PDFium exposes that. So the streams are inflated and scanned here.
/// </para>
/// <para>
/// A deliberately small scanner. It tokenises on white space and looks only at the operators, which
/// is enough because the question is about NESTING rather than about what is drawn — and a string
/// or an inline image holding the word <c>EMC</c> would have to be constructed on purpose.
/// </para>
/// </remarks>
static class PdfContent
{
    /// <summary>
    /// The drawing operators found outside any marked-content span, with a little context.
    /// </summary>
    public static List<string> Untagged(byte[] pdf)
    {
        var loose = new List<string>();

        foreach (var stream in Streams(pdf))
        {
            var depth = 0;

            foreach (var token in Tokens(stream))
            {
                switch (token)
                {
                    case "BDC":
                    case "BMC":
                        depth++;
                        continue;
                    case "EMC":
                        depth--;
                        continue;
                }

                if (depth == 0 && Draws(token))
                {
                    loose.Add(token);
                }
            }
        }

        return loose;
    }

    /// <summary>
    /// The stream's tokens, strings removed.
    /// </summary>
    /// <remarks>
    /// White space is not a reliable separator in a content stream: a name is self-delimiting, so
    /// krilla writes <c>EMC/Artifact BMC</c> with nothing between the first two — which a naive
    /// split reads as one token and neither of the two operators it holds. Splitting on the
    /// delimiters as well is the whole of the difference between this test failing everywhere and
    /// passing.
    ///
    /// Strings go first, because their contents are arbitrary and a text-showing operand could
    /// hold the letter <c>f</c>.
    /// </remarks>
    static IEnumerable<string> Tokens(string stream)
    {
        var stripped = Regex.Replace(stream, @"\((?:\\.|[^\\()])*\)|<[^>]*>", " ");

        foreach (Match match in Regex.Matches(stripped, @"[^\s/\[\]<>(){}]+"))
        {
            yield return match.Value;
        }
    }

    /// <summary>
    /// Operators that put ink on the page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The path-painting operators and the four text-showing ones. Path CONSTRUCTION is not here:
    /// a <c>re</c> outside a span draws nothing until something paints it, and a clip path is
    /// construction.
    /// </para>
    /// <para>
    /// <c>sh</c> and <c>Do</c> are deliberately absent, and it is the one judgement in this file.
    /// Both REFERENCE content that lives in a stream of its own — a shading, or a form XObject,
    /// which is what krilla emits for an isolated transparency group — and this scanner reaches
    /// every such stream separately, so the ink is checked where it is written rather than where
    /// it is invoked. A gradient's <c>sh</c> and an opacity group's <c>Do</c> both sit outside a
    /// span in the page's own stream while everything they draw is inside one.
    /// </para>
    /// </remarks>
    static bool Draws(string token) =>
        token is
            "f" or "F" or "f*" or "B" or "B*" or "b" or "b*" or "S" or "s" or
            "Tj" or "TJ" or "'" or "\"";

    /// <summary>
    /// Every flate-compressed stream in <paramref name="pdf"/> that inflates to text.
    /// </summary>
    /// <remarks>
    /// Font programs and images inflate to bytes that are not text, and an uncompressed stream is
    /// not one krilla writes for page content. Both are skipped by the same test: a stream holding
    /// a NUL is not a content stream.
    /// </remarks>
    static IEnumerable<string> Streams(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var at = 0;

        while (true)
        {
            var start = text.IndexOf("stream", at, StringComparison.Ordinal);

            if (start < 0)
            {
                yield break;
            }

            var end = text.IndexOf("endstream", start, StringComparison.Ordinal);

            if (end < 0)
            {
                yield break;
            }

            at = end + "endstream".Length;

            var from = start + "stream".Length;

            while (from < end && text[from] is '\r' or '\n')
            {
                from++;
            }

            if (Inflate(pdf, from, end - from) is {} content && !content.Contains('\0'))
            {
                yield return content;
            }
        }
    }

    static string? Inflate(byte[] pdf, int offset, int length)
    {
        if (length <= 0)
        {
            return null;
        }

        try
        {
            using var source = new MemoryStream(pdf, offset, length);
            using var inflated = new ZLibStream(source, CompressionMode.Decompress);
            using var reader = new StreamReader(inflated, Encoding.Latin1);

            return reader.ReadToEnd();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
