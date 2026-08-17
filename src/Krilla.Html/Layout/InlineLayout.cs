namespace Krilla.Html.Layout;

/// <summary>
/// Flows a block's inline content into lines.
/// </summary>
/// <remarks>
/// <para>
/// Greedy line breaking: take words until one does not fit, then break. That is what browsers do
/// for normal text — the optimal-fit algorithm TeX uses is a different rendering, not a better
/// implementation of the same one — so matching a browser means being greedy here too.
/// </para>
/// <para>
/// Break opportunities are at spaces only. No hyphenation dictionary, and no Unicode line
/// breaking algorithm (UAX #14), so text without spaces does not break at all — CJK in particular
/// will overflow rather than wrap. The corpus stays out of that territory until UAX #14 is worth
/// implementing.
/// </para>
/// </remarks>
static class InlineLayout
{
    /// <summary>
    /// Lays out <paramref name="box"/>'s inline content into lines
    /// <paramref name="contentWidth"/> wide, and returns the total height.
    /// </summary>
    /// <remarks>
    /// Lines are positioned relative to the content box origin; the caller translates them once
    /// the block's own position is settled.
    /// </remarks>
    public static float Layout(LayoutBox box, float contentWidth, FontSet fonts)
    {
        box.Lines.Clear();

        var tokens = Tokenize(box.Inlines, fonts);
        if (tokens.Count == 0)
        {
            return 0;
        }

        var wraps = box.Style.Wraps;
        var y = 0f;
        var current = new List<Token>();
        var currentWidth = 0f;
        Token? pendingSpace = null;

        foreach (var token in tokens)
        {
            if (token.Kind == TokenKind.Break)
            {
                // A forced break ends the line even when empty, which is what produces the blank
                // line for a double newline in preformatted text.
                Flush(box, current, currentWidth, contentWidth, fonts, ref y, forced: true);
                current = [];
                currentWidth = 0;
                pendingSpace = null;
                continue;
            }

            if (token.Kind == TokenKind.Space)
            {
                // A space at the start of a line is dropped — but only where white space
                // collapses. Under `pre` the indentation IS leading spaces, and dropping them
                // left-aligns every line that was deliberately indented, which is the entire
                // point of preformatted text.
                if (current.Count == 0 && !token.Style.PreservesSpaces)
                {
                    continue;
                }

                // Runs collapse into one pending space that only materialises if a word follows on
                // the same line. Under a preserving value the run arrived as a single token
                // already carrying its full width.
                pendingSpace = token;
                continue;
            }

            var spaceWidth = pendingSpace?.Width ?? 0;

            if (wraps &&
                current.Count > 0 &&
                currentWidth + spaceWidth + token.Width > contentWidth)
            {
                // The pending space is deliberately dropped rather than carried down: it sits at
                // the break, and a trailing space must not affect where the previous line's
                // alignment puts its content.
                Flush(box, current, currentWidth, contentWidth, fonts, ref y, forced: false);
                current = [token];
                currentWidth = token.Width;
                pendingSpace = null;
                continue;
            }

            if (pendingSpace is {} space)
            {
                current.Add(space);
                currentWidth += space.Width;
                pendingSpace = null;
            }

            current.Add(token);
            currentWidth += token.Width;
        }

        if (current.Count > 0)
        {
            Flush(box, current, currentWidth, contentWidth, fonts, ref y, forced: true);
        }

        return y;
    }

    /// <summary>
    /// Turns the block's inline items into breakable tokens, measuring each.
    /// </summary>
    static List<Token> Tokenize(List<InlineItem> items, FontSet fonts)
    {
        var tokens = new List<Token>();

        foreach (var item in items)
        {
            var face = fonts.Resolve(item.Style.FontFamilies, item.Style.FontWeight, item.Style.Italic);

            if (item.ForcedBreak)
            {
                tokens.Add(new("", item.Style, face, 0, TokenKind.Break));
                continue;
            }

            var index = 0;

            while (index < item.Text.Length)
            {
                var character = item.Text[index];

                if (character == '\n')
                {
                    tokens.Add(new("", item.Style, face, 0, TokenKind.Break));
                    index++;
                    continue;
                }

                if (character == ' ')
                {
                    var start = index;
                    while (index < item.Text.Length && item.Text[index] == ' ')
                    {
                        index++;
                    }

                    // Preserved white space keeps its full run, since every space of it occupies
                    // the page. Collapsed white space arrives here already reduced to one.
                    var text = item.Style.PreservesSpaces ? item.Text[start..index] : " ";
                    var width = TextMeasurer.Measure(face, text, item.Style.FontSize);
                    tokens.Add(new(text, item.Style, face, width, TokenKind.Space));
                    continue;
                }

                var wordStart = index;
                while (index < item.Text.Length && item.Text[index] is not (' ' or '\n'))
                {
                    index++;
                }

                var word = item.Text[wordStart..index];
                tokens.Add(new(
                    word,
                    item.Style,
                    face,
                    TextMeasurer.Measure(face, word, item.Style.FontSize),
                    TokenKind.Word));
            }
        }

        return tokens;
    }

    /// <summary>
    /// Turns the accumulated tokens into a line, positions its runs, and advances
    /// <paramref name="y"/> past it.
    /// </summary>
    static void Flush(
        LayoutBox box,
        List<Token> tokens,
        float width,
        float contentWidth,
        FontSet fonts,
        ref float y,
        bool forced)
    {
        // Trailing spaces hang outside the line box: they must not push a right-aligned line left
        // or shift a centred one. Preserved white space is exempt, being content rather than
        // separation.
        var lastContent = tokens.FindLastIndex(_ => _.Kind == TokenKind.Word);
        if (lastContent >= 0 && !box.Style.PreservesSpaces)
        {
            for (var index = tokens.Count - 1; index > lastContent; index--)
            {
                width -= tokens[index].Width;
                tokens.RemoveAt(index);
            }
        }

        var line = new LineBox();

        // Every line starts from the strut: the zero-width inline box the block's own font and
        // line-height would produce. It is why an empty line still has height, and why a line
        // holding only small text is not shorter than the block's line-height.
        var strutFace = fonts.Resolve(box.Style.FontFamilies, box.Style.FontWeight, box.Style.Italic);
        var (above, below) = Extents(box.Style, strutFace);

        foreach (var token in tokens)
        {
            var (tokenAbove, tokenBelow) = Extents(token.Style, token.Face);
            above = Math.Max(above, tokenAbove);
            below = Math.Max(below, tokenBelow);
        }

        var height = above + below;
        line.Bounds = new(0, y, contentWidth, height);
        line.Baseline = above;

        var x = box.Style.TextAlign switch
        {
            TextAlignKind.Center => (contentWidth - width) / 2,
            TextAlignKind.Right => contentWidth - width,
            // The last line of a justified block is not stretched, so it aligns to the start edge
            // like any other left-aligned line.
            TextAlignKind.Justify when !forced => 0,
            _ => 0
        };

        var justify = box.Style.TextAlign == TextAlignKind.Justify && !forced;
        var extra = justify ? ExtraSpacePerGap(tokens, contentWidth - width) : 0;

        // Adjacent tokens sharing a style become one run: fewer glyph draws, and the painted text
        // stays one selectable unit in the PDF.
        var runStart = 0;
        while (runStart < tokens.Count)
        {
            var runEnd = runStart;
            while (runEnd + 1 < tokens.Count &&
                   ReferenceEquals(tokens[runEnd + 1].Style, tokens[runStart].Style) &&
                   ReferenceEquals(tokens[runEnd + 1].Face, tokens[runStart].Face) &&
                   extra == 0)
            {
                runEnd++;
            }

            var builder = new StringBuilder();
            var runWidth = 0f;

            for (var index = runStart; index <= runEnd; index++)
            {
                builder.Append(tokens[index].Text);
                runWidth += tokens[index].Width;

                if (extra > 0 && tokens[index].Kind == TokenKind.Space)
                {
                    runWidth += extra;
                }
            }

            line.Runs.Add(new(
                builder.ToString(),
                tokens[runStart].Style,
                tokens[runStart].Face,
                x,
                y + above,
                runWidth));

            x += runWidth;
            runStart = runEnd + 1;
        }

        box.Lines.Add(line);
        y += height;
    }

    /// <summary>
    /// How far an inline box reaches above and below the baseline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Half-leading: the gap between the declared line height and the font's own content height is
    /// split evenly above and below. That is what centres a generous <c>line-height</c> around the
    /// text rather than letting it hang underneath, and it is also why a line-height smaller than
    /// the font's natural height makes lines overlap rather than clip.
    /// </para>
    /// <para>
    /// The half is FLOORED, which matters more than it looks. With integer ascent and descent an
    /// exact half regularly lands on .5 — 16px text in a 24px line gives 3.5 — and a baseline at
    /// 17.5 rasterises a whole pixel lower than one at 17. Browsers floor, so text sits a pixel
    /// higher than the arithmetic alone suggests; measured against Chrome, flooring here is the
    /// difference between a page of text matching to a handful of pixels and matching exactly.
    /// </para>
    /// <para>
    /// <c>Below</c> is then derived by subtraction rather than computed symmetrically, so the two
    /// still sum to exactly the line height. Flooring both would lose a pixel per line, and a
    /// pixel per line compounds into a wrong page count.
    /// </para>
    /// </remarks>
    static (float Above, float Below) Extents(ComputedStyle style, FontFace face)
    {
        var lineHeight = style.ResolveLineHeight(face);
        var ascent = face.Ascent(style.FontSize);
        var descent = face.Descent(style.FontSize);

        var above = MathF.Floor((lineHeight - (ascent + descent)) / 2) + ascent;
        return (above, lineHeight - above);
    }

    /// <summary>
    /// How much to widen each space so a justified line reaches both edges.
    /// </summary>
    static float ExtraSpacePerGap(List<Token> tokens, float slack)
    {
        if (slack <= 0)
        {
            return 0;
        }

        var gaps = tokens.Count(_ => _.Kind == TokenKind.Space);
        return gaps == 0 ? 0 : slack / gaps;
    }

    enum TokenKind
    {
        Word,
        Space,
        Break
    }

    readonly record struct Token(
        string Text,
        ComputedStyle Style,
        FontFace Face,
        float Width,
        TokenKind Kind);
}
