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
    public static float Layout(
        LayoutBox box,
        float contentX,
        float contentY,
        float contentWidth,
        FontSet fonts,
        FloatContext floats)
    {
        box.Lines.Clear();

        var tokens = Tokenize(box.Inlines, fonts, contentWidth);
        if (tokens.Count == 0)
        {
            return 0;
        }

        var wraps = box.Style.Wraps;
        var y = 0f;
        var current = new List<Token>();
        var currentWidth = 0f;
        Token? pendingSpace = null;

        // The height the band is sampled over. The strut is the line-height of the block itself,
        // which is every line height unless something taller sits on the line — and the band has
        // to be known before the line is filled, so the final height is not available to use.
        var strutFace = fonts.Resolve(box.Style.FontFamilies, box.Style.FontWeight, box.Style.Italic);
        var (strutAbove, strutBelow) = Extents(box.Style, strutFace);
        var strut = strutAbove + strutBelow;

        var band = OpenLine(floats, contentX, contentY, contentWidth, strut, ref y);

        // `text-indent` applies to the FIRST line of a block container and to no other, which is
        // why it is applied here rather than inside OpenLine — every later line reopens through
        // that method and must not pick it up. It narrows the band from the start edge rather than
        // shifting it, so alignment still measures against the room the line actually has. A
        // negative value widens it the other way, hanging the first line outside the content box.
        if (box.Style.TextIndent.Resolve(contentWidth) is var indent and not 0)
        {
            band = new(band.Left + indent, Math.Max(0, band.Width - indent));
        }

        foreach (var token in tokens)
        {
            if (token.Kind == TokenKind.Break)
            {
                // A forced break ends the line even when empty, which is what produces the blank
                // line for a double newline in preformatted text.
                Flush(box, current, currentWidth, band, fonts, ref y, forced: true);
                band = OpenLine(floats, contentX, contentY, contentWidth, strut, ref y);
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
                currentWidth + spaceWidth + token.Width > band.Width)
            {
                // The pending space is deliberately dropped rather than carried down: it sits at
                // the break, and a trailing space must not affect where the previous line's
                // alignment puts its content.
                Flush(box, current, currentWidth, band, fonts, ref y, forced: false);

                // The next line sits further down the page, so the floats beside it need not be
                // the same ones. Asked again rather than reused, which is what lets text close up
                // underneath a float that has ended.
                band = OpenLine(floats, contentX, contentY, contentWidth, strut, ref y);
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
            Flush(box, current, currentWidth, band, fonts, ref y, forced: true);
        }

        return y;
    }

    /// <summary>
    /// The horizontal room a line starting at <paramref name="y"/> has, moving it down past floats
    /// that leave it none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Answered relative to the content box, because that is the space lines are built in. The
    /// float context works in absolute coordinates, and the conversion happens here.
    /// </para>
    /// <para>
    /// The descent is CSS 2.1 §9.5: a line box shortened to nothing is shifted downward until
    /// either it fits or no floats remain. It applies only once the band has closed completely — a
    /// band merely too narrow for the next word lets that word overflow, which is what a browser
    /// does with a long word beside a float.
    /// </para>
    /// </remarks>
    static Band OpenLine(
        FloatContext floats,
        float contentX,
        float contentY,
        float contentWidth,
        float strut,
        ref float y)
    {
        var edge = contentX + contentWidth;

        while (true)
        {
            var top = contentY + y;
            var (left, right) = floats.Band(top, top + strut, contentX, edge);

            if (right > left || floats.NextBottomBelow(top) is not {} next)
            {
                return new(left - contentX, Math.Max(0, right - left));
            }

            y = next - contentY;
        }
    }

    /// <summary>
    /// The min-content and max-content widths of <paramref name="items"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured from the same tokens line breaking uses, which is the point of putting it here
    /// rather than in <see cref="IntrinsicWidths"/>: a column sized from one set of measurements
    /// and filled by another would disagree with itself, and the disagreement would show as text
    /// overflowing a column that was supposedly wide enough.
    /// </para>
    /// <para>
    /// The maximum is the widest run between forced breaks. The minimum is the widest run between
    /// break OPPORTUNITIES, which is not the same as the widest word: under a non-wrapping
    /// white-space value a space is not an opportunity, so the whole run is unbreakable and the
    /// two measurements converge.
    /// </para>
    /// </remarks>
    public static (float Min, float Max) Intrinsic(List<InlineItem> items, FontSet fonts)
    {
        var min = 0f;
        var max = 0f;
        var segment = 0f;
        var unbreakable = 0f;
        var pendingSpace = 0f;
        var started = false;

        foreach (var token in Tokenize(items, fonts, 0, measuring: true))
        {
            if (token.Kind == TokenKind.Break)
            {
                max = Math.Max(max, segment);
                min = Math.Max(min, unbreakable);
                segment = 0;
                unbreakable = 0;
                pendingSpace = 0;
                started = false;
                continue;
            }

            if (token.Kind == TokenKind.Space)
            {
                // Leading white space collapses away and occupies nothing, exactly as it does when
                // a line is flushed — unless it is preserved, where it is content.
                if (!started && !token.Style.PreservesSpaces)
                {
                    continue;
                }

                if (token.Style.Wraps)
                {
                    // A break opportunity: it ends the current unbreakable run, and the space
                    // itself only counts toward the maximum if a word follows it.
                    min = Math.Max(min, unbreakable);
                    unbreakable = 0;
                    pendingSpace += token.Width;
                }
                else
                {
                    segment += token.Width;
                    unbreakable += token.Width;
                }

                started = true;
                continue;
            }

            // An inline-block contributes its max-content width to the maximum and its
            // min-content width to the minimum. Every other token is unbreakable, so the one
            // number serves for both.
            var floor = token.Kind == TokenKind.Box ? token.MinWidth : token.Width;

            segment += pendingSpace + token.Width;
            unbreakable += floor;
            pendingSpace = 0;
            started = true;
            min = Math.Max(min, unbreakable);
        }

        return (min, Math.Max(max, segment));
    }

    /// <summary>
    /// Turns the block's inline items into breakable tokens, measuring each.
    /// </summary>
    static List<Token> Tokenize(
        List<InlineItem> items,
        FontSet fonts,
        float contentWidth,
        bool measuring = false)
    {
        var tokens = new List<Token>();

        foreach (var item in items)
        {
            var face = fonts.Resolve(item.Style.FontFamilies, item.Style.FontWeight, item.Style.Italic);

            if (item.ForcedBreak)
            {
                tokens.Add(new(item.Style, face, 0, TokenKind.Break, Link: item.Link));
                continue;
            }

            if (item.Box is {} inline)
            {
                tokens.Add(InlineBlock(item, inline, face, fonts, contentWidth, measuring));
                continue;
            }

            if (item.Image is {} image)
            {
                // An atomic inline: a box on the line rather than a run of glyphs. It breaks like
                // a word — a line can break before or after it, but never inside it.
                var (width, height) = ReplacedSizing.Resolve(
                    item.Style,
                    image,
                    contentWidth,
                    item.Style.SurroundX(contentWidth),
                    item.Style.SurroundY(contentWidth));
                tokens.Add(new(
                    item.Style, face, width, TokenKind.Replaced, image, height, item.Selector,
                    item.Link));
                continue;
            }

            // Shaped once for the whole item, then sliced. Shaping each word separately would
            // also lose the kerning between a word and the punctuation attached to it.
            var shaped = ShapedText.Create(face, item.Text, item.Style.FontSize);
            var index = 0;

            while (index < item.Text.Length)
            {
                var character = item.Text[index];

                if (character == '\n')
                {
                    tokens.Add(new(item.Style, face, 0, TokenKind.Break, Link: item.Link));
                    index++;
                    continue;
                }

                var start = index;

                if (character == ' ')
                {
                    while (index < item.Text.Length && item.Text[index] == ' ')
                    {
                        index++;
                    }

                    // Preserved white space keeps its full run, since every space of it occupies
                    // the page. Collapsed white space arrives here already reduced to one, so the
                    // range is one character wide either way.
                    var end = item.Style.PreservesSpaces ? index : start + 1;

                    tokens.Add(new(
                        item.Style,
                        face,
                        shaped.Width(start, end),
                        TokenKind.Space,
                        Link: item.Link,
                        Shaped: shaped,
                        TextStart: start,
                        TextEnd: end));
                    continue;
                }

                while (index < item.Text.Length && item.Text[index] is not (' ' or '\n'))
                {
                    index++;
                }

                tokens.Add(new(
                    item.Style,
                    face,
                    shaped.Width(start, index),
                    TokenKind.Word,
                    Link: item.Link,
                    Shaped: shaped,
                    TextStart: start,
                    TextEnd: index));
            }
        }

        return tokens;
    }

    /// <summary>
    /// Lays out one <c>inline-block</c> and measures it as a token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Laid out here rather than in <see cref="BlockLayout"/> because the line cannot be filled
    /// until the box's size is known, and its size is a whole layout of its own. It gets a fresh
    /// <see cref="FloatContext"/> by taking the default: an inline-block establishes its own block
    /// formatting context, so it sees no float from outside and grows to contain those inside it.
    /// </para>
    /// <para>
    /// Sized to the containing block's width rather than to the band left beside a float, which
    /// is the same approximation an inline image gets and for the same reason: tokenising happens
    /// once, before any line has been opened, and which band the box lands in is not yet known.
    /// </para>
    /// <para>
    /// The box is moved so its MARGIN box starts at the origin, which is what lets
    /// <see cref="Flush"/> place it with a single translate rather than carrying the margins
    /// through to the call site.
    /// </para>
    /// </remarks>
    static Token InlineBlock(
        InlineItem item,
        LayoutBox box,
        FontFace face,
        FontSet fonts,
        float contentWidth,
        bool measuring)
    {
        var style = box.Style;
        var marginLeft = style.MarginLeft.Resolve(contentWidth);
        var marginRight = style.MarginRight.Resolve(contentWidth);
        var marginTop = style.MarginTop.Resolve(contentWidth);
        var marginBottom = style.MarginBottom.Resolve(contentWidth);
        var horizontal = marginLeft + marginRight;

        var (minimum, maximum) = IntrinsicWidths.Measure(box, fonts);

        if (measuring)
        {
            // The intrinsic pass wants both extremes and no layout: it runs to decide a column's
            // width, so laying the box out now would size it against a width nobody has settled.
            return new(
                item.Style, face, maximum + horizontal, TokenKind.Box,
                Selector: item.Selector, Link: item.Link, MinWidth: minimum + horizontal);
        }

        var available = Math.Max(0, contentWidth - horizontal);

        // Always an assigned width, never the ordinary horizontal resolution: that path reads
        // `margin: auto` as a request to centre, which is what it means for a block in flow and
        // not what it means here — an auto margin on an inline-block is zero.
        var assigned = BlockLayout.ShrinkToFit(box, available, fonts) ??
                       Declared(style, available);

        var height = BlockLayout.Layout(box, 0, 0, available, fonts, assigned);

        box.Translate(marginLeft - box.BorderBox.X, marginTop - box.BorderBox.Y);

        return new(
            item.Style,
            face,
            box.BorderBox.Width + horizontal,
            TokenKind.Box,
            Height: height + marginTop + marginBottom,
            Selector: item.Selector,
            Link: item.Link,
            Box: box,
            Baseline: LastBaseline(box) ?? height + marginTop + marginBottom,
            MinWidth: minimum + horizontal);
    }

    /// <summary>
    /// The border-box width a declared <c>width</c> asks for, or null when it is auto.
    /// </summary>
    static float? Declared(ComputedStyle style, float available)
    {
        if (style.Width.ResolveOrNull(available) is { } width)
        {
            // Under `border-box` the declaration is already the border box, and a declaration
            // narrower than the surround leaves the box as narrow as its own padding allows.
            var surround = style.SurroundX(available);
            return style.ContentSize(width, surround) + surround;
        }

        return null;
    }

    /// <summary>
    /// The baseline an <c>inline-block</c> aligns on, measured down from its margin-box top, or
    /// null when it has no in-flow line box to take one from.
    /// </summary>
    /// <remarks>
    /// The LAST line box, not the first: a two-line inline-block sits with its second line on the
    /// text baseline beside it, which is why one in a sentence pushes the line's top up rather
    /// than hanging below.
    ///
    /// In-flow only. A float is out of flow and offers no baseline, and a nested atomic inline
    /// sits inside a line that has already been counted, so descending into either would take a
    /// baseline from a box that does not own one.
    /// </remarks>
    static float? LastBaseline(LayoutBox box)
    {
        float? baseline = null;
        var lowest = float.NegativeInfinity;

        Walk(box);
        return baseline;

        void Walk(LayoutBox current)
        {
            foreach (var line in current.Lines)
            {
                if (line.Bounds.Bottom >= lowest)
                {
                    lowest = line.Bounds.Bottom;
                    baseline = line.Bounds.Y + line.Baseline;
                }
            }

            foreach (var child in current.Children)
            {
                Walk(child);
            }
        }
    }

    /// <summary>
    /// Turns the accumulated tokens into a line, positions its runs, and advances
    /// <paramref name="y"/> past it.
    /// </summary>
    static void Flush(
        LayoutBox box,
        List<Token> tokens,
        float width,
        Band band,
        FontSet fonts,
        ref float y,
        bool forced)
    {
        // Trailing spaces hang outside the line box: they must not push a right-aligned line left
        // or shift a centred one. Preserved white space is exempt, being content rather than
        // separation.
        var lastContent = tokens.FindLastIndex(_ => _.Kind == TokenKind.Word || IsAtomic(_.Kind));
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
            // A replaced inline sits its bottom margin edge on the baseline, which is what
            // `vertical-align: baseline` means for one. So it reaches its whole height above the
            // baseline and nothing below — and a tall image consequently pushes the line's top up
            // rather than growing it downward.
            //
            // An inline-block is the case that shows why that is a special rule rather than the
            // general one: it has a baseline of its own, taken from its last line, so part of it
            // sits BELOW the line's baseline and the text beside it lines up with its text.
            var (tokenAbove, tokenBelow) = token.Kind switch
            {
                TokenKind.Replaced => (token.Height, 0f),
                TokenKind.Box => (token.Baseline, token.Height - token.Baseline),
                _ => Extents(token.Style, token.Face)
            };

            above = Math.Max(above, tokenAbove);
            below = Math.Max(below, tokenBelow);
        }

        var height = above + below;

        // The line box is the BAND, not the content box: beside a float a line starts where the
        // float ends and is only as wide as what is left. Alignment follows from that — a
        // right-aligned line beside a left float ends at the content edge but begins inside the
        // band, which is measurably what a browser does rather than an interpretation of it.
        line.Bounds = new(band.Left, y, band.Width, height);
        line.Baseline = above;

        var x = box.Style.TextAlign switch
        {
            TextAlignKind.Center => band.Left + (band.Width - width) / 2,
            TextAlignKind.Right => band.Left + band.Width - width,
            // The last line of a justified block is not stretched, so it aligns to the start edge
            // like any other left-aligned line.
            TextAlignKind.Justify when !forced => band.Left,
            _ => band.Left
        };

        var justify = box.Style.TextAlign == TextAlignKind.Justify && !forced;
        var extra = justify ? ExtraSpacePerGap(tokens, band.Width - width) : 0;

        // Adjacent tokens sharing a style become one run: fewer glyph draws, and the painted text
        // stays one selectable unit in the PDF.
        var runStart = 0;
        while (runStart < tokens.Count)
        {
            // An image is not text, so it interrupts the run rather than joining it. Its bottom
            // edge sits on the baseline.
            if (tokens[runStart] is {Kind: TokenKind.Replaced, Image: {} image} replaced)
            {
                line.Images.Add(new(
                    image,
                    new(x, y + above - replaced.Height, replaced.Width, replaced.Height),
                    replaced.Selector));

                x += replaced.Width;
                runStart++;
                continue;
            }

            // An inline-block was laid out with its margin box at the origin, so one translate
            // puts the whole tree where the line wants it: its own baseline onto the line's.
            if (tokens[runStart] is {Kind: TokenKind.Box, Box: {} inline} atomic)
            {
                inline.Translate(x, y + above - atomic.Baseline);
                line.Boxes.Add(inline);

                x += atomic.Width;
                runStart++;
                continue;
            }

            var runEnd = runStart;
            while (runEnd + 1 < tokens.Count &&
                   !IsAtomic(tokens[runEnd + 1].Kind) &&
                   tokens[runEnd + 1].Link == tokens[runStart].Link &&
                   ReferenceEquals(tokens[runEnd + 1].Style, tokens[runStart].Style) &&
                   ReferenceEquals(tokens[runEnd + 1].Face, tokens[runStart].Face) &&
                   // Only tokens contiguous within one shaped run can be joined: their glyph
                   // ranges are adjacent slices of the same array, and merging across a gap
                   // would silently swallow whatever sat between them.
                   ReferenceEquals(tokens[runEnd + 1].Shaped, tokens[runStart].Shaped) &&
                   tokens[runEnd + 1].TextStart == tokens[runEnd].TextEnd &&
                   extra == 0)
            {
                runEnd++;
            }

            var runWidth = 0f;

            for (var index = runStart; index <= runEnd; index++)
            {
                runWidth += tokens[index].Width;

                if (extra > 0 && tokens[index].Kind == TokenKind.Space)
                {
                    runWidth += extra;
                }
            }

            var (glyphs, runText) = tokens[runStart].Shaped is {} shaped
                ? shaped.Slice(tokens[runStart].TextStart, tokens[runEnd].TextEnd)
                : ([], "");

            line.Runs.Add(new(
                runText,
                tokens[runStart].Style,
                tokens[runStart].Face,
                x,
                y + above,
                runWidth,
                tokens[runStart].Link,
                glyphs));

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
        if (gaps == 0)
        {
            return 0;
        }

        return slack / gaps;
    }

    enum TokenKind
    {
        Word,
        Space,
        Break,

        /// <summary>An image, which occupies a box rather than glyphs.</summary>
        Replaced,

        /// <summary>An <c>inline-block</c>, which occupies a box holding a tree of its own.</summary>
        Box
    }

    /// <summary>
    /// Whether a token is an atomic inline: one unbreakable box on the line, interrupting the run
    /// of glyphs around it rather than joining it.
    /// </summary>
    static bool IsAtomic(TokenKind kind) =>
        kind is TokenKind.Replaced or TokenKind.Box;

    /// <summary>One measured, unbreakable piece of a line.</summary>
    /// <remarks>
    /// <c>Width</c> is the token's advance, and for an atomic inline it is the MARGIN box: a
    /// horizontal margin holds the text around it away exactly as the box itself does.
    /// <c>Height</c> is the margin box too.
    ///
    /// <c>Baseline</c> is where an <c>inline-block</c> aligns, measured down from the top of its
    /// margin box. CSS 2.1 §10.8.1 puts it on the baseline of the box's LAST in-flow line, or on
    /// its bottom margin edge when it has none — which is what makes an empty one, or one holding
    /// nothing but an image, sit on the line the way an image does.
    ///
    /// <c>MinWidth</c> is an <c>inline-block</c>'s min-content width, which is not derivable from
    /// <c>Width</c>: the box has already been sized to the room available, and
    /// <see cref="Intrinsic"/> needs to know how far it could be squeezed.
    /// </remarks>
    readonly record struct Token(
        ComputedStyle Style,
        FontFace Face,
        float Width,
        TokenKind Kind,
        ImageData? Image = null,
        float Height = 0,
        string? Selector = null,
        string? Link = null,
        ShapedText? Shaped = null,
        int TextStart = 0,
        int TextEnd = 0,
        LayoutBox? Box = null,
        float Baseline = 0,
        float MinWidth = 0);
}

/// <summary>
/// The horizontal room available to one line, relative to its block content box.
/// </summary>
/// <param name="Left">Offset of the line box from the content box left edge.</param>
/// <param name="Width">How wide the line box is.</param>
/// <remarks>
/// Without floats this is always (0, contentWidth). With them it is what the floats beside this
/// particular line have left over, which differs line by line down the same block.
/// </remarks>
readonly record struct Band(float Left, float Width);
