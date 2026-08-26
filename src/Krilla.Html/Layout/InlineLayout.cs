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
/// A break opportunity is a property of a token rather than of the gap between two of them, and
/// <c>Token.BreaksBefore</c> carries it. That is the more roundabout of the two available models
/// and it is the one that matches a browser: adjacency is NOT an opportunity, so two inline
/// elements written with no space between them are one word and overflow together, while a dash
/// offers a break in the middle of what looks like a single token.
/// </para>
/// <para>
/// Opportunities are at spaces, at dashes, at soft hyphens, either side of an atomic inline, and —
/// where <c>overflow-wrap</c> or <c>word-break</c> permits it — between any two characters of a
/// word. What is missing is a hyphenation dictionary and the Unicode line breaking algorithm (UAX
/// #14) beyond the dashes, so CJK in particular will overflow rather than wrap. The corpus stays
/// out of that territory until UAX #14 is worth implementing.
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
        FloatContext floats,
        float? contentHeight = null)
    {
        box.Lines.Clear();

        var tokens = Tokenize(box.Inlines, fonts, contentWidth, containingHeight: contentHeight);
        if (tokens.Count == 0)
        {
            return 0;
        }

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
        // An `inside` list marker narrows the first line exactly as `text-indent` does, and for
        // the same reason: it occupies the start of that line and nothing else. Adding the two
        // rather than choosing between them is what a browser does — an indented inside list item
        // starts its text past both.
        var indent = box.Style.TextIndent.Resolve(contentWidth) + ListMarkers.Reserved(box, fonts);

        if (indent != 0)
        {
            band = new(band.Left + indent, Math.Max(0, band.Width - indent));
        }

        var unbreakable = UnbreakableWidths(tokens);

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];

            if (token.Kind == TokenKind.Break)
            {
                // A forced break ends the line even when empty, which is what produces the blank
                // line for a double newline in preformatted text.
                Flush(box, current, currentWidth, band, fonts, ref y, forced: true, token.Selector);
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

            if (token.Kind == TokenKind.Tab)
            {
                // The distance to the next stop, from where the line has reached. `Width` on the
                // token is the STOP SPACING rather than an advance, which is the one place a
                // token's width means something other than how wide it is.
                var stop = token.Width;
                var advance = stop * MathF.Floor(currentWidth / stop + 1) - currentWidth;

                if (pendingSpace is {} beforeTab)
                {
                    current.Add(beforeTab);
                    currentWidth += beforeTab.Width;
                    pendingSpace = null;
                }

                current.Add(token with {Width = advance});
                currentWidth += advance;
                continue;
            }

            var spaceWidth = pendingSpace?.Width ?? 0;

            // A pending space is an opportunity whatever follows it; otherwise the token has to
            // offer one itself. Without this second half a line breaks between any two adjacent
            // tokens, which puts a break inside a word split across two inline elements — measured
            // against Chrome, which overflows the line instead.
            //
            // Asked of the TOKEN at the opportunity rather than of the block, because `white-space`
            // inherits and an element inside a wrapping paragraph can turn wrapping off for its own
            // text alone. Reading the block's suppressed nothing on `<span style="white-space:
            // nowrap">`, which is how the property is nearly always written — a whole block that
            // never wraps is the rarer case.
            var breakable =
                (pendingSpace is {} opportunity && opportunity.Style.Wraps) ||
                (token.BreaksBefore && token.Style.Wraps);

            // Measured over the whole UNBREAKABLE RUN starting here, not over this token alone.
            // The two differ whenever a break opportunity is followed by tokens that offer none —
            // an inline element's padding and the first word inside it, or two elements written
            // with no space between them. Measuring the first token alone lets it onto the line
            // and then appends the rest with nowhere left to break, so the line overruns its band
            // instead of moving the group down whole.
            if (breakable &&
                current.Count > 0 &&
                currentWidth + spaceWidth + unbreakable[index] > band.Width)
            {
                // The pending space is deliberately dropped rather than carried down: it sits at
                // the break, and a trailing space must not affect where the previous line's
                // alignment puts its content.
                Flush(box, current, currentWidth, band, fonts, ref y, forced: false);

                // The next line sits further down the page, so the floats beside it need not be
                // the same ones. Asked again rather than reused, which is what lets text close up
                // underneath a float that has ended.
                band = OpenLine(floats, contentX, contentY, contentWidth, strut, ref y);
                current = [];
                currentWidth = 0;
                pendingSpace = null;

                // Deliberately no `continue`: the token falls through to the splitting loop below,
                // because a word moved to a fresh line may STILL not fit on it. Adding it here
                // instead is what let a break-word paragraph overflow — the ordinary break had
                // already placed the word before anything asked whether it could be cut.
            }

            if (pendingSpace is {} space)
            {
                current.Add(space);
                currentWidth += space.Width;
                pendingSpace = null;
            }

            // `word-break` and `overflow-wrap` let a line break INSIDE a word, which every rule
            // above forbids. The loop runs because one cut is rarely enough: a word wider than
            // several lines is cut once per line it crosses.
            while (token.Style.Wraps &&
                   Splittable(token, band.Width) &&
                   currentWidth + token.Width > band.Width &&
                   Split(token, band.Width - currentWidth, current.Count > 0) is var (head, tail))
            {
                current.Add(head);
                Flush(box, current, currentWidth + head.Width, band, fonts, ref y, forced: false);
                band = OpenLine(floats, contentX, contentY, contentWidth, strut, ref y);
                current = [];
                currentWidth = 0;
                token = tail;
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
    /// Whether a line may be broken inside <paramref name="token"/>.
    /// </summary>
    /// <remarks>
    /// <c>break-all</c> permits it always; <c>break-word</c> only for a word that fits on no line
    /// of this width at all, which is the distinction between the two values and the reason they
    /// are not folded together. Only a shaped word can be cut — an image or an inline-block is
    /// atomic whatever the property says.
    /// </remarks>
    static bool Splittable(Token token, float bandWidth) =>
        token is {Kind: TokenKind.Word, Shaped: not null} &&
        token.Style.WordBreaking switch
        {
            WordBreaking.Always => true,
            WordBreaking.OnOverflow => token.Width > bandWidth,
            _ => false
        };

    /// <summary>
    /// Cuts <paramref name="token"/> at the widest prefix fitting <paramref name="room"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns null when nothing can be cut off: when the first character alone does not fit and
    /// the line already holds something, the whole token goes down to the next line instead, where
    /// it will have the full band to be cut against. On an EMPTY line one character is taken
    /// regardless — otherwise a character wider than the band loops forever.
    /// </para>
    /// <para>
    /// Linear rather than a binary search, because the answer is a running sum: <c>ShapedText</c>
    /// answers a sub-range by summing known advances, so walking outward from the start costs the
    /// same as one width query and gives every candidate on the way.
    /// </para>
    /// </remarks>
    static (Token Head, Token Tail)? Split(Token token, float room, bool lineHasContent)
    {
        var shaped = token.Shaped!;
        var cut = token.TextStart;

        for (var end = token.TextStart + 1; end < token.TextEnd; end++)
        {
            if (shaped.Width(token.TextStart, end) > room)
            {
                break;
            }

            cut = end;
        }

        if (cut == token.TextStart)
        {
            if (lineHasContent || token.TextEnd - token.TextStart < 2)
            {
                return null;
            }

            cut = token.TextStart + 1;
        }

        return (
            token with
            {
                TextEnd = cut,
                Width = shaped.Width(token.TextStart, cut),
                // A word cut mid-way takes no hyphen: the break was forced by the box, not offered
                // by the text, and a browser draws nothing there.
                HyphenAfter = false
            },
            token with
            {
                TextStart = cut,
                Width = shaped.Width(cut, token.TextEnd),
                BreaksBefore = false
            });
    }

    /// <summary>
    /// The run that draws a hyphen after <paramref name="token"/>, at its own style and face.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shaped here rather than carried on the token, because it is needed on at most one line per
    /// break and building it for every hyphenated segment would shape a string per opportunity for
    /// the sake of the one that is taken.
    /// </para>
    /// <para>
    /// It carries the token's selector and inline ancestry, which is measured rather than assumed:
    /// a browser's rectangle for a hyphenated element is 5.33px wider than the text inside it, so
    /// the hyphen belongs to the element that broke. An inline background reaches under it for the
    /// same reason.
    /// </para>
    /// </remarks>
    static TextRun Hyphen(Token token)
    {
        var shaped = ShapedText.Create(
            token.Face,
            "-",
            token.Style.FontSize,
            token.Style.LetterSpacing,
            token.Style.WordSpacing);

        var (glyphs, text) = shaped.Slice(0, 1);

        return new(
            text,
            token.Style,
            token.Face,
            0,
            0,
            shaped.Width(0, 1),
            token.Link,
            glyphs,
            token.Selector,
            token.Backdrops);
    }

    /// <summary>
    /// Removes the soft hyphens from <paramref name="text"/>, returning the offsets they stood at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The offsets are into the RETURNED string, and each names the position a break may fall at —
    /// so a soft hyphen between "hy" and "phen" comes back as the offset of the "p".
    /// </para>
    /// <para>
    /// Null when there were none, which is the overwhelmingly common case and worth not allocating
    /// a set for.
    /// </para>
    /// </remarks>
    static string SoftHyphens(string text, out HashSet<int>? offsets)
    {
        offsets = null;

        if (!text.Contains('­'))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        offsets = [];

        foreach (var character in text)
        {
            if (character == '­')
            {
                offsets.Add(builder.Length);
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// The widest single character of a token, which is its min-content width when a line may break
    /// anywhere inside it.
    /// </summary>
    /// <remarks>
    /// Per CHARACTER rather than per glyph, and measured through the same shaped run the layout uses,
    /// so a ligature reports the width of the characters it covers rather than of the glyph. Falls
    /// back to the whole token for anything with no shaped text — an image or an inline-block, which
    /// cannot be broken whatever the property says.
    /// </remarks>
    static float Narrowest(Token token)
    {
        if (token.Shaped is not {} shaped)
        {
            return token.Width;
        }

        var widest = 0f;

        for (var index = token.TextStart; index < token.TextEnd; index++)
        {
            widest = Math.Max(widest, shaped.Width(index, index + 1));
        }

        return widest;
    }

    /// <summary>
    /// For each token, the width of the unbreakable run that starts there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A run is the token plus every token after it that offers no break of its own, which is how
    /// a group that has to move together is recognised: an inline element's opening edge glued to
    /// the first word inside it, a word split across two elements, a word followed by the closing
    /// edge of the element it sat in and then a comma.
    /// </para>
    /// <para>
    /// Computed backwards in one pass rather than by looking ahead at each candidate, which would
    /// be quadratic in the length of a paragraph made entirely of glued tokens.
    /// </para>
    /// </remarks>
    static float[] UnbreakableWidths(List<Token> tokens)
    {
        var widths = new float[tokens.Count];

        for (var index = tokens.Count - 1; index >= 0; index--)
        {
            if (Ends(tokens[index]))
            {
                continue;
            }

            widths[index] = tokens[index].Width;

            if (index + 1 < tokens.Count &&
                !Ends(tokens[index + 1]) &&
                !Offers(tokens[index + 1]))
            {
                widths[index] += widths[index + 1];
            }
        }

        return widths;

        // A tab or a forced break is not content and ends the run rather than joining it. A tab in
        // particular must not be summed: its width field holds the stop spacing rather than an
        // advance, and adding that to a fit test measures nothing. A SPACE ends the run only where
        // it is an opportunity — inside a `nowrap` element it is content like any other, and a run
        // that stopped there would be measured short of the group that has to move together.
        static bool Ends(Token token) =>
            token.Kind is TokenKind.Break or TokenKind.Tab ||
            (token.Kind == TokenKind.Space && token.Style.Wraps);

        static bool Offers(Token token) =>
            token.BreaksBefore && token.Style.Wraps;
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

            // A break opportunity that is not a space — after a dash, or either side of an
            // atomic inline — ends the current unbreakable run exactly as a space does. Without
            // this the min-content width of a hyphenated word is the whole word, so a table column
            // is sized to hold text that layout would have broken and comes out too wide.
            if (token.BreaksBefore && token.Style.Wraps)
            {
                min = Math.Max(min, unbreakable);
                unbreakable = 0;
            }

            // An inline-block contributes its max-content width to the maximum and its
            // min-content width to the minimum. Every other token is unbreakable, so the one
            // number serves for both — unless the style lets a line break INSIDE a word, where the
            // minimum is the widest single character rather than the widest word.
            //
            // Only `break-all` and `anywhere` narrow it. `break-word` deliberately does not: CSS
            // has it break a word that would otherwise overflow WITHOUT changing what the box asks
            // for, which is what keeps a table column from collapsing to one character wide the
            // moment the property is set on it.
            var floor = token.Kind == TokenKind.Box
                ? token.MinWidth
                : token.Style.WordBreaking == WordBreaking.Always
                    ? Narrowest(token)
                    : token.Width;

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
        bool measuring = false,
        float? containingHeight = null)
    {
        var tokens = new List<Token>();

        // Whether a break is allowed BEFORE the next token emitted. It survives across items on
        // purpose: an element boundary is not an opportunity, so a word ending one inline element
        // and continuing in the next has to carry `false` over the join.
        var breakable = true;

        foreach (var item in items)
        {
            var face = fonts.Resolve(item.Style.FontFamilies, item.Style.FontWeight, item.Style.Italic);

            // Resolved once per item rather than once per token: the faces are the same for every
            // word the item produces, and resolving is a lookup through the whole family list.
            var backdrops = item.Backdrops is {Count: > 0} ancestors
                ? ancestors
                    .Select(_ => new InlineBackdrop(
                        _,
                        fonts.Resolve(_.FontFamilies, _.FontWeight, _.Italic)))
                    .ToArray()
                : null;

            // An inline element's opening or closing edge. It carries no text and takes the
            // advance its padding and border ask for, which is what pushes the words after it
            // along — the whole reason this is a layout concern rather than a painting one.
            if (item.Edge != InlineEdgeKind.None)
            {
                var leading = item.Edge == InlineEdgeKind.Leading;

                // The horizontal margin is part of the advance and outside the border box, so it
                // is carried in the token's width and subtracted again when the box is placed.
                // CSS drops the VERTICAL margins on an inline element entirely, which is why only
                // this pair appears anywhere in the inline path.
                tokens.Add(new(
                    item.Style,
                    face,
                    leading
                        ? item.Style.MarginLeft.Resolve(contentWidth) +
                          item.Style.PaddingLeft.Resolve(contentWidth) + item.Style.BorderLeft
                        : item.Style.MarginRight.Resolve(contentWidth) +
                          item.Style.PaddingRight.Resolve(contentWidth) + item.Style.BorderRight,
                    TokenKind.Edge,
                    Link: item.Link,
                    Selector: item.Selector,
                    Backdrops: backdrops,
                    BreaksBefore: breakable,
                    Edge: item.Edge));

                // A line may break before the opening edge and never between it and the first word
                // inside, which would leave the padding stranded at the end of the line above.
                breakable = false;
                continue;
            }

            if (item.ForcedBreak)
            {
                tokens.Add(new(item.Style, face, 0, TokenKind.Break, Link: item.Link, Selector: item.Selector));
                breakable = true;
                continue;
            }

            if (item.Box is {} inline)
            {
                // Either side of an atomic inline is an opportunity, with or without a space —
                // measured, and the reason this is stated rather than inherited from `breakable`.
                tokens.Add(InlineBlock(item, inline, face, fonts, contentWidth, measuring, containingHeight) with
                {
                    BreaksBefore = true
                });
                breakable = true;
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
                // A marker image is measured differently from an ordinary inline image, and the
                // two placements differ again. OUTSIDE takes no advance at all — the line starts
                // where it would have without one and the image is drawn back beyond that. INSIDE
                // takes its own width plus the marker gap, which is the same seven pixels an
                // outside marker leaves and was measured on both.
                // An atomic inline occupies its MARGIN box on the line and hangs the bottom of that
                // box on the baseline, so its whole box model counts — including the vertical
                // margins, which a non-replaced inline does not get. A marker image is not that
                // box: it is generated content with no element of its own, so it keeps the two
                // measured advances above.
                var outerWidth = width + item.Style.SurroundX(contentWidth) +
                                 item.Style.MarginLeft.Resolve(contentWidth) +
                                 item.Style.MarginRight.Resolve(contentWidth);

                var outerHeight = height + item.Style.SurroundY(contentWidth) +
                                  item.Style.MarginTop.Resolve(contentWidth) +
                                  item.Style.MarginBottom.Resolve(contentWidth);

                var advance = item.Marker switch
                {
                    MarkerPlacement.Outside => 0,
                    MarkerPlacement.Inside => image.Width + ListMarkers.MarkerGap,
                    _ => outerWidth
                };

                tokens.Add(new(
                    item.Style, face, advance, TokenKind.Replaced, image,
                    item.Marker == MarkerPlacement.None ? outerHeight : height,
                    item.Selector, item.Link, BreaksBefore: true, Marker: item.Marker));
                breakable = true;
                continue;
            }

            // Soft hyphens are removed BEFORE shaping and their positions kept as break
            // opportunities. They cannot survive into the shaper: this face maps U+00AD onto a real
            // hyphen glyph with a real advance, so a word carrying two of them would measure wider
            // than the same word without and would draw hyphens that no break called for. What
            // makes the property work is that an unbroken word measures exactly as it would with
            // nothing in it.
            var text = SoftHyphens(item.Text, out var conditional);

            // Shaped once for the whole item, then sliced. Shaping each word separately would
            // also lose the kerning between a word and the punctuation attached to it.
            //
            // One run per FACE, which is one run over the whole item for every document whose
            // text the resolved face covers — which is nearly all of them, and is exactly what
            // this was before coverage was consulted at all.
            var runs = ShapedRuns(text, item.Style, face, fonts);
            var index = 0;

            while (index < text.Length)
            {
                var character = text[index];

                if (character == '\n')
                {
                    // A newline in preformatted text ends a line without being an element, so it reports
                    // no rectangle — unlike the <br> above, which is one.
                    tokens.Add(new(item.Style, face, 0, TokenKind.Break, Link: item.Link));
                    breakable = true;
                    index++;
                    continue;
                }

                var start = index;

                // A tab advances to the next tab stop rather than to a width of its own, and where
                // that stop falls depends on how far along the line the tab sits — which is not
                // known here. So the token carries the STOP SPACING and its width is settled when
                // the line is being filled.
                //
                // Measured out of Chrome: the stops sit at multiples of `tab-size` space advances
                // from the line's start edge, and a tab already exactly on one advances to the
                // next rather than to nothing.
                if (character == '\t')
                {
                    index++;

                    var stop = At(runs, start);

                    tokens.Add(new(
                        item.Style,
                        stop.Face,
                        item.Style.TabStop ?? item.Style.TabSize * face.Advance(' ', item.Style.FontSize),
                        TokenKind.Tab,
                        Link: item.Link,
                        Selector: item.Selector,
                        Backdrops: backdrops,
                        Shaped: stop.Shaped,
                        TextStart: start - stop.Start,
                        TextEnd: index - stop.Start));

                    // A break opportunity, exactly as a space is, which is what lets `pre-wrap`
                    // wrap a tabulated line.
                    breakable = true;
                    continue;
                }

                if (character == ' ')
                {
                    while (index < text.Length && text[index] == ' ')
                    {
                        index++;
                    }

                    // Preserved white space keeps its full run, since every space of it occupies
                    // the page. Collapsed white space arrives here already reduced to one, so the
                    // range is one character wide either way.
                    var end = item.Style.PreservesSpaces ? index : start + 1;
                    var spaces = At(runs, start);

                    tokens.Add(new(
                        item.Style,
                        spaces.Face,
                        spaces.Shaped.Width(start - spaces.Start, end - spaces.Start),
                        TokenKind.Space,
                        Link: item.Link,
                        Selector: item.Selector,
                        Backdrops: backdrops,
                        Shaped: spaces.Shaped,
                        TextStart: start - spaces.Start,
                        TextEnd: end - spaces.Start,
                        Generated: item.Generated));
                    breakable = true;
                    continue;
                }

                while (index < text.Length && text[index] is not (' ' or '\n' or '\t'))
                {
                    index++;
                }

                // One token per dash-terminated segment rather than one per word. Splitting here
                // rather than at the break itself keeps every width a sub-range of the one shaped
                // run, so the segments sum to exactly the width the whole word had and the kerning
                // across the dash survives.
                var segment = start;

                for (var scan = start; scan < index; scan++)
                {
                    // Nothing follows a dash that ends the item's text, so it offers no break here
                    // — though it still leaves `breakable` true, and the next item's first word
                    // can start a line. That is what makes `<span>page-</span><span>break</span>`
                    // break where `<span>page</span><span>break</span>` does not.
                    // A soft hyphen offers a break AFTER the character it followed, exactly as
                    // a real dash does — the difference being only whether the hyphen is drawn.
                    var soft = conditional?.Contains(scan + 1) == true;

                    if ((!BreaksAfter(text[scan]) && !soft) || scan + 1 >= index)
                    {
                        continue;
                    }

                    Word(segment, scan + 1, soft);
                    segment = scan + 1;
                }

                Word(segment, index, conditional?.Contains(index) == true);

                void Word(int from, int to, bool hyphenates)
                {
                    // One token per face the segment crosses, glued together: a change of face is
                    // not a break opportunity, so only the first carries `BreaksBefore` and only
                    // the last can hyphenate. With one run — every document whose text its own
                    // face covers — this is one token, exactly as it was.
                    var first = true;

                    foreach (var run in runs)
                    {
                        var head = Math.Max(from, run.Start);
                        var tail = Math.Min(to, run.End);

                        if (head >= tail)
                        {
                            continue;
                        }

                        tokens.Add(new(
                            item.Style,
                            run.Face,
                            run.Shaped.Width(head - run.Start, tail - run.Start),
                            TokenKind.Word,
                            Link: item.Link,
                            Selector: item.Selector,
                            Shaped: run.Shaped,
                            TextStart: head - run.Start,
                            TextEnd: tail - run.Start,
                            BreaksBefore: first && breakable,
                            Backdrops: backdrops,
                            HyphenAfter: hyphenates && tail == to,
                            Generated: item.Generated));

                        first = false;
                    }

                    // A run of dashes therefore offers a break after each one, and greedy line
                    // breaking takes the last that fits — which is how `a--b` keeps both dashes on
                    // the line above rather than carrying the second one down.
                    breakable = hyphenates || BreaksAfter(text[to - 1]);
                }
            }
        }

        return tokens;
    }

    /// <summary>One stretch of an item's text that one face draws, shaped on its own.</summary>
    /// <param name="Start">Where it begins in the item's text.</param>
    /// <param name="End">Where it ends, exclusive.</param>
    /// <param name="Face">The face covering it.</param>
    /// <param name="Shaped">That stretch shaped, indexed from its own start.</param>
    readonly record struct ShapedRun(int Start, int End, FontFace Face, ShapedText Shaped);

    /// <summary>
    /// Splits an item's text where the resolved face stops covering it, and shapes each piece.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="FontSet.Resolve"/> answers a font-family list and nothing else, so a character
    /// the resolved face lacks was drawn as <c>.notdef</c>: a document in Greek set in a face with
    /// no Greek came out as a row of boxes, silently, with the family resolution having done
    /// exactly what it was asked. Coverage is the other half of the question and it is asked per
    /// CHARACTER, so the answer is a list of runs rather than one face.
    /// </para>
    /// <para>
    /// Each run is shaped over its OWN substring rather than sliced out of one shaping of the
    /// whole. It has to be: a shaper works in one face, and the kerning it would find across a
    /// boundary where the face changes is not kerning any face defines. The cost is that a
    /// document needing fallback shapes once per run, which is the price of drawing the character
    /// at all.
    /// </para>
    /// <para>
    /// The common case is one run over the whole item — every document whose text its own face
    /// covers — and it is the same single shaping this did before coverage was consulted. That
    /// matters more than it sounds: it is what says the corpus, which is entirely Latin, cannot
    /// have moved.
    /// </para>
    /// </remarks>
    static List<ShapedRun> ShapedRuns(
        string text,
        ComputedStyle style,
        FontFace primary,
        FontSet fonts)
    {
        FontFace[]? chosen = null;

        for (var index = 0; index < text.Length; index++)
        {
            var codepoint = char.ConvertToUtf32(text, index);
            var length = char.IsSurrogatePair(text, index) ? 2 : 1;

            if (!primary.Covers(codepoint))
            {
                if (chosen is null)
                {
                    chosen = new FontFace[text.Length];
                    Array.Fill(chosen, primary, 0, index);
                }

                var face = fonts.Covering(
                    style.FontFamilies,
                    style.FontWeight,
                    style.Italic,
                    codepoint,
                    primary);

                for (var offset = 0; offset < length; offset++)
                {
                    chosen[index + offset] = face;
                }
            }
            else if (chosen is not null)
            {
                for (var offset = 0; offset < length; offset++)
                {
                    chosen[index + offset] = primary;
                }
            }

            index += length - 1;
        }

        if (chosen is null)
        {
            return [new(0, text.Length, primary, Shape(primary, text, style))];
        }

        var runs = new List<ShapedRun>();
        var start = 0;

        for (var index = 1; index <= text.Length; index++)
        {
            if (index < text.Length && ReferenceEquals(chosen[index], chosen[start]))
            {
                continue;
            }

            runs.Add(new(
                start,
                index,
                chosen[start],
                Shape(chosen[start], text[start..index], style)));

            start = index;
        }

        return runs;

        static ShapedText Shape(FontFace face, string run, ComputedStyle style) =>
            ShapedText.Create(
                face,
                run,
                style.FontSize,
                style.LetterSpacing,
                style.WordSpacing);
    }

    /// <summary>The run holding <paramref name="position"/>.</summary>
    /// <remarks>
    /// A linear walk, because the list is one entry for every document that needs no fallback and
    /// a handful for one that does.
    /// </remarks>
    static ShapedRun At(List<ShapedRun> runs, int position)
    {
        foreach (var run in runs)
        {
            if (position < run.End)
            {
                return run;
            }
        }

        return runs[^1];
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
        bool measuring,
        float? containingHeight)
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

        // The containing height goes down with the width, so an inline-block's own percentage
        // height resolves against the block that holds the line — measured: a 50% one inside a
        // 200px frame is 100px, and its contents then see that as a definite height in turn.
        var height = BlockLayout.Layout(
            box,
            0,
            0,
            available,
            fonts,
            assigned,
            containingHeight: containingHeight);

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
        bool forced,
        string? endedBy = null)
    {
        // Trailing spaces hang outside the line box: they must not push a right-aligned line left
        // or shift a centred one. Preserved white space is exempt, being content rather than
        // separation.
        var lastContent = tokens.FindLastIndex(
            _ => _.Kind is TokenKind.Word or TokenKind.Edge or TokenKind.Tab || IsAtomic(_.Kind));
        if (lastContent >= 0 && !box.Style.PreservesSpaces)
        {
            for (var index = tokens.Count - 1; index > lastContent; index--)
            {
                width -= tokens[index].Width;
                tokens.RemoveAt(index);
            }
        }

        // A soft hyphen renders only where the break actually falls on it, which is the whole of
        // what distinguishes it from a dash already in the text. The line it ends grows by the
        // hyphen's advance, so alignment measures the line as it will be drawn.
        TextRun? hyphen = !forced && lastContent >= 0 && tokens[lastContent].HyphenAfter
            ? Hyphen(tokens[lastContent])
            : null;

        if (hyphen is {} drawn)
        {
            width += drawn.Width;
        }

        var line = new LineBox();

        // Every line starts from the strut: the zero-width inline box the block's own font and
        // line-height would produce. It is why an empty line still has height, and why a line
        // holding only small text is not shorter than the block's line-height.
        var strutFace = fonts.Resolve(box.Style.FontFamilies, box.Style.FontWeight, box.Style.Italic);
        var (above, below) = Extents(box.Style, strutFace);

        // How far each token is moved off the baseline by `vertical-align`, down-positive. Held
        // per token rather than folded into the extents, because it is needed twice: once to size
        // the line and again to place the token in it.
        var shifts = new float[tokens.Count];

        // `top` and `bottom` measure against the LINE BOX, which is not known until every other
        // token has been counted — so they are set aside and resolved below.
        var deferred = new List<int>();

        for (var index = 0; index < tokens.Count; index++)
        {
            var (tokenAbove, tokenBelow) = BaseExtents(tokens[index]);
            var align = InlineAlign(tokens[index], box.Style);

            if (align is VerticalAlignKind.Top or VerticalAlignKind.Bottom)
            {
                deferred.Add(index);
                continue;
            }

            var shift = Shift(align, tokens[index], tokenAbove, tokenBelow, box.Style, strutFace);
            shifts[index] = shift;

            above = Math.Max(above, tokenAbove - shift);
            below = Math.Max(below, tokenBelow + shift);
        }

        foreach (var index in deferred)
        {
            var (tokenAbove, tokenBelow) = BaseExtents(tokens[index]);

            // Against the line as the rest of it settled. A box taller than that grows the line
            // away from the edge it is pinned to — downward for `top`, upward for `bottom` — which
            // leaves its own offset unchanged and so needs no second pass.
            if (InlineAlign(tokens[index], box.Style) == VerticalAlignKind.Top)
            {
                shifts[index] = tokenAbove - above;
                below = Math.Max(below, tokenBelow + shifts[index]);
            }
            else
            {
                shifts[index] = below - tokenBelow;
                above = Math.Max(above, tokenAbove - shifts[index]);
            }
        }

        var height = above + below;

        // The line box is the BAND, not the content box: beside a float a line starts where the
        // float ends and is only as wide as what is left. Alignment follows from that — a
        // right-aligned line beside a left float ends at the content edge but begins inside the
        // band, which is measurably what a browser does rather than an interpretation of it.
        line.Bounds = new(band.Left, y, band.Width, height);
        line.Baseline = above;

        // `text-align-last` decides the last line of the block and the line before a forced
        // break, which is exactly what `forced` marks. Its `auto` hands the decision back to
        // `text-align` with the one carve-out CSS makes for it: the last line of a justified block
        // aligns to the start edge rather than being stretched. A declared value replaces the whole
        // of that rule, which is what lets `text-align-last: justify` stretch the line the default
        // exempts.
        var alignment = forced
            ? box.Style.TextAlignLast ??
              (box.Style.TextAlign == TextAlignKind.Justify ? TextAlignKind.Left : box.Style.TextAlign)
            : box.Style.TextAlign;

        var x = alignment switch
        {
            TextAlignKind.Center => band.Left + (band.Width - width) / 2,
            TextAlignKind.Right => band.Left + band.Width - width,
            _ => band.Left
        };

        var justify = alignment == TextAlignKind.Justify;
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
                // An outside marker sits its own width to the left of the line, with the same gap
                // a symbol marker leaves — the same constant, because it is the same gap.
                var left = replaced.Marker == MarkerPlacement.Outside
                    ? x - image.Width - ListMarkers.MarkerGap
                    : x;

                // The token's extent is the MARGIN box; the rectangle recorded is the BORDER box,
                // which is what the browser reports and what the background and border paint into.
                // A marker image has no such box, so its two edges are zero and the two agree.
                var ordinary = replaced.Marker == MarkerPlacement.None;
                var style = replaced.Style;

                var marginLeft = ordinary ? style.MarginLeft.Resolve(band.Width) : 0;
                var marginTop = ordinary ? style.MarginTop.Resolve(band.Width) : 0;
                var marginRight = ordinary ? style.MarginRight.Resolve(band.Width) : 0;
                var marginBottom = ordinary ? style.MarginBottom.Resolve(band.Width) : 0;

                var outer = new Rect(
                    left + marginLeft,
                    y + above + shifts[runStart] - replaced.Height + marginTop,
                    ordinary ? replaced.Width - marginLeft - marginRight : image.Width,
                    replaced.Height - marginTop - marginBottom);

                var insetLeft = ordinary ? style.BorderLeft + style.PaddingLeft.Resolve(band.Width) : 0;
                var insetTop = ordinary ? style.BorderTop + style.PaddingTop.Resolve(band.Width) : 0;
                var insetRight = ordinary ? style.BorderRight + style.PaddingRight.Resolve(band.Width) : 0;
                var insetBottom = ordinary ? style.BorderBottom + style.PaddingBottom.Resolve(band.Width) : 0;

                line.Images.Add(new(
                    image,
                    outer,
                    new(
                        outer.X + insetLeft,
                        outer.Y + insetTop,
                        Math.Max(0, outer.Width - insetLeft - insetRight),
                        Math.Max(0, outer.Height - insetTop - insetBottom)),
                    replaced.Selector,
                    style,
                    ordinary));

                x += replaced.Width;
                runStart++;
                continue;
            }

            // A tab is pure advance. It carries a Shaped range like any other token, so without
            // this it would join the run beside it and the tab character would be drawn as a glyph
            // — with the run's summed width and the glyph's own advance disagreeing, which shifts
            // every glyph after it inside the same run.
            if (tokens[runStart].Kind == TokenKind.Tab)
            {
                x += tokens[runStart].Width;
                runStart++;
                continue;
            }

            if (tokens[runStart] is {Kind: TokenKind.Edge} edge)
            {
                var baseline = y + above + shifts[runStart];
                var (top, bottom) = InlineMetrics.Extent(edge.Style, edge.Face, baseline);

                // The margin is advance and nothing else: it sits outside the border box, before
                // it at the opening edge and after it at the closing one.
                var margin = edge.Edge == InlineEdgeKind.Leading
                    ? edge.Style.MarginLeft.Resolve(0)
                    : 0;

                var surround = edge.Width - (edge.Edge == InlineEdgeKind.Leading
                    ? edge.Style.MarginLeft.Resolve(0)
                    : edge.Style.MarginRight.Resolve(0));

                line.Edges.Add(new(
                    edge.Style,
                    edge.Face,
                    new(x + margin, top, surround, bottom - top),
                    edge.Edge,
                    edge.Selector,
                    baseline,
                    edge.Backdrops));

                x += edge.Width;
                runStart++;
                continue;
            }

            // An inline-block was laid out with its margin box at the origin, so one translate
            // puts the whole tree where the line wants it: its own baseline onto the line's.
            if (tokens[runStart] is {Kind: TokenKind.Box, Box: {} inline} atomic)
            {
                inline.Translate(x, y + above + shifts[runStart] - atomic.Baseline);
                line.Boxes.Add(inline);

                x += atomic.Width;
                runStart++;
                continue;
            }

            var runEnd = runStart;
            while (runEnd + 1 < tokens.Count &&
                   !IsAtomic(tokens[runEnd + 1].Kind) &&
                   // A tab's text range is contiguous with the words either side of it, so without
                   // this it joins their run and the tab CHARACTER is drawn — at the glyph's own
                   // advance rather than at the distance to the tab stop, which moves every glyph
                   // after it inside the same run.
                   tokens[runEnd + 1].Kind != TokenKind.Tab &&
                   tokens[runEnd + 1].Link == tokens[runStart].Link &&
                   ReferenceEquals(tokens[runEnd + 1].Style, tokens[runStart].Style) &&
                   ReferenceEquals(tokens[runEnd + 1].Face, tokens[runStart].Face) &&
                   // Two runs under different inline ancestors paint different backgrounds, so
                   // merging them would put one element's highlight behind the other's text.
                   ReferenceEquals(tokens[runEnd + 1].Backdrops, tokens[runStart].Backdrops) &&
                   tokens[runEnd + 1].Selector == tokens[runStart].Selector &&
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
                y + above + shifts[runStart],
                runWidth,
                tokens[runStart].Link,
                glyphs,
                tokens[runStart].Selector,
                tokens[runStart].Backdrops,
                tokens[runStart].Generated));

            x += runWidth;
            runStart = runEnd + 1;
        }

        if (hyphen is {} trailing)
        {
            line.Runs.Add(trailing with {X = x, Y = y + above + shifts[lastContent]});
        }

        // The <br> that ended the line, where the line stopped and as tall as the block's own
        // text. Measured: Chrome puts it at the end of the content rather than at the band's edge,
        // so an empty line reports it at the start and a centred one at the end of the centred
        // text.
        if (endedBy is {} selector)
        {
            var size = box.Style.FontSize;

            line.Breaks.Add(new(
                selector,
                new(x, y + above - strutFace.Ascent(size), 0, strutFace.Ascent(size) + strutFace.Descent(size))));
        }

        box.Lines.Add(line);
        y += height;
    }

    /// <summary>
    /// How far a token reaches above and below its own alignment point, before
    /// <c>vertical-align</c> moves it.
    /// </summary>
    /// <remarks>
    /// A replaced inline sits its bottom margin edge on the baseline, which is what
    /// <c>vertical-align: baseline</c> means for one. So it reaches its whole height above the
    /// baseline and nothing below — and a tall image consequently pushes the line's top up rather
    /// than growing it downward.
    ///
    /// An inline-block is the case that shows why that is a special rule rather than the general
    /// one: it has a baseline of its own, taken from its last line, so part of it sits BELOW the
    /// line's baseline and the text beside it lines up with its text.
    /// </remarks>
    static (float Above, float Below) BaseExtents(Token token) =>
        token.Kind switch
        {
            TokenKind.Replaced => (token.Height, 0f),
            TokenKind.Box => (token.Baseline, token.Height - token.Baseline),
            _ => Extents(token.Style, token.Face)
        };

    /// <summary>
    /// The alignment that applies to <paramref name="token"/> as an inline-level box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two guards, and both are needed. A token sharing the block's own style is the block's own
    /// text rather than an inline-level box of its own, and <c>vertical-align</c> does not apply
    /// to a block. And a value that was INHERITED rather than declared does not apply either —
    /// which is what keeps a table cell's <c>middle</c>, handed down so the cell can read it, from
    /// shifting every run of text inside the cell by half an x-height.
    /// </para>
    /// <para>
    /// Together they leave exactly the intended case: an element that asked for an alignment,
    /// including <c>&lt;sub&gt;</c> and <c>&lt;sup&gt;</c>, whose values come from the default
    /// stylesheet and are therefore declared.
    /// </para>
    /// </remarks>
    static VerticalAlignKind InlineAlign(Token token, ComputedStyle block)
    {
        if (!ReferenceEquals(token.Style, block) && token.Style.VerticalAlignDeclared)
        {
            return token.Style.VerticalAlign;
        }

        return VerticalAlignKind.Baseline;
    }

    /// <summary>
    /// How far <c>vertical-align</c> moves a token off the baseline, down-positive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every keyword measures against the PARENT's font rather than the aligned box's own, which
    /// is what CSS says for <c>middle</c> and what measurement confirms for the rest: giving the
    /// aligned box its own <c>font-size: 10px</c> inside a 32px paragraph moves it not at all.
    /// </para>
    /// <para>
    /// The superscript and subscript offsets are user-agent defined, so — as with list markers and
    /// <c>line-height: normal</c> — there is no correct value to compute and agreeing with the
    /// reference browser is the useful target. Measured across three sizes: the offset is linear
    /// in the font size with an intercept of exactly one pixel, giving <c>size / 3 + 1</c> for
    /// <c>super</c> and <c>size / 5 + 1</c> for <c>sub</c>. Neither is a font metric — the OS/2
    /// table's own superscript offset for this face is 7.63px at 16px where the browser uses 6.33.
    /// </para>
    /// <para>
    /// <c>middle</c> is the only one that reads a metric, and it reads the x-height unrounded:
    /// the ratio holds at 0.5283 of the size at 16, 24 and 32 pixels, which is this face's
    /// <c>sxHeight</c> over its em.
    /// </para>
    /// </remarks>
    static float Shift(
        VerticalAlignKind align,
        Token token,
        float tokenAbove,
        float tokenBelow,
        ComputedStyle block,
        FontFace strut)
    {
        var size = block.FontSize;

        return align switch
        {
            VerticalAlignKind.Super => -Quantise(size / 3 + 1),
            VerticalAlignKind.Sub => Quantise(size / 5 + 1),
            VerticalAlignKind.TextTop => tokenAbove - strut.Ascent(size),
            VerticalAlignKind.TextBottom => strut.Descent(size) - tokenBelow,
            VerticalAlignKind.Middle =>
                tokenAbove - (tokenAbove + tokenBelow) / 2 - strut.XHeight(size) / 2,
            // The exception to the paragraph above: a length is the box's OWN, because `em` and a
            // percentage are ordinary CSS value resolution against the element that declared them
            // rather than the user-agent arithmetic the keywords are. Negated because a positive
            // `vertical-align` raises and this is down-positive.
            VerticalAlignKind.Length => -Quantise(
                token.Style.VerticalAlignOffset.Resolve(token.Style.ResolveLineHeight(token.Face))),
            _ => 0
        };
    }

    /// <summary>
    /// Snaps a length onto the 1/64 pixel grid a browser lays out on, rounding down.
    /// </summary>
    /// <remarks>
    /// Blink holds every layout length as a fixed-point <c>LayoutUnit</c> of 1/64 pixel and
    /// truncates toward it, so a superscript offset of 16/3 + 1 is stored as 6.328125 rather than
    /// 6.3333. That is a fortieth of a pixel and it is still visible: the line box it sizes ends
    /// on a fractional row, and the paragraph background painted to that row comes out a different
    /// shade from the browser's. Applied only where a browser's own arithmetic lands off the grid
    /// — everything else here is already whole pixels or comes from the font.
    /// </remarks>
    static float Quantise(float value) =>
        MathF.Floor(value * 64) / 64;

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
        Box,

        /// <summary>
        /// One end of an inline element carrying padding or a border: no glyphs, and the advance
        /// that surround asks for.
        /// </summary>
        Edge,

        /// <summary>
        /// A preserved tab, whose width is the distance to the next tab stop and so is not known
        /// until the line it sits on is being filled.
        /// </summary>
        Tab
    }

    /// <summary>
    /// Whether a line may break directly after <paramref name="character"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three dashes, and measured out of Chrome one arrangement at a time rather than taken
    /// from UAX #14 — which was worth doing, because the obvious exceptions turn out not to exist.
    /// A hyphen between two digits breaks (<c>1234567890-1234567890</c> wraps), a hyphen at the
    /// START of a word breaks and leaves the dash alone on the line above, and a hyphen followed
    /// by digits breaks. There is no numeric context rule here to implement and no leading-dash
    /// rule either: every hyphen-minus with something after it offers a break.
    /// </para>
    /// <para>
    /// U+2011 NON-BREAKING HYPHEN is excluded, which is the whole of its purpose, and it reads as
    /// an ordinary word character here for that reason. U+00AD SOFT HYPHEN is a break opportunity
    /// in a browser and is deliberately NOT one here: it has to paint a hyphen when the break is
    /// taken there and nothing at all when it is not, which is a conditional glyph rather than a
    /// break rule. It is unimplemented rather than decided against.
    /// </para>
    /// <para>
    /// U+002F SOLIDUS is absent because Chrome does not break after one — measured, and worth
    /// stating because a URL is the obvious thing a reader would expect to wrap.
    /// </para>
    /// </remarks>
    static bool BreaksAfter(char character) =>
        character is '-' or '\u2013' or '\u2014';

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
    ///
    /// <c>BreaksBefore</c> is whether a line may START at this token. It is false for the ordinary
    /// case of one word following another with nothing between them, which is what stops a word
    /// split across two inline elements from breaking at the join.
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
        float MinWidth = 0,
        bool BreaksBefore = false,
        bool HyphenAfter = false,
        bool Generated = false,
        MarkerPlacement Marker = MarkerPlacement.None,
        IReadOnlyList<InlineBackdrop>? Backdrops = null,
        InlineEdgeKind Edge = InlineEdgeKind.None);
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
