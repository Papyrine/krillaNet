/// <summary>
/// Places a list item's marker.
/// </summary>
/// <remarks>
/// <para>
/// Every number below was measured out of headless Chromium at one device pixel per CSS pixel,
/// across seventeen font sizes and three families, rather than taken from a specification —
/// because there is no specification. CSS says a marker is placed "outside the principal box" and
/// leaves the offsets to the user agent, so there is no correct value to compute and agreeing with
/// the browser the corpus is measured against is the only useful target. This is the same
/// reasoning that governs <see cref="FontFace.NormalLineHeight"/>.
/// </para>
/// <para>
/// The arithmetic is deliberately integer. Chrome's marker geometry is computed in whole pixels
/// from a whole-pixel ascent, and the truncation is not incidental — a symbol's size steps from 4
/// to 5 pixels between 14px and 15px text, which no rounded float expression reproduces. Every
/// division here truncates, which is what an <c>int</c> division in C# already does for the
/// positive values involved.
/// </para>
/// <para>
/// One consequence worth stating: this is scale-dependent in Chrome too. The same page rendered at
/// a device scale factor of 8 puts its bullets somewhere else entirely, because the whole-pixel
/// rounding happens in device pixels. The corpus renders at one device pixel per CSS pixel, so
/// that is what these reproduce.
/// </para>
/// </remarks>
static class ListMarkers
{
    /// <summary>
    /// The gap Chrome leaves between a symbol marker and the item's edge, before the part that
    /// scales with the text.
    /// </summary>
    const int symbolPadding = 7;

    /// <summary>
    /// The gap an outside marker leaves between itself and the item's border edge.
    /// </summary>
    /// <remarks>
    /// Read by <see cref="InlineLayout"/> for a marker IMAGE, which is placed the same way and so
    /// must not carry a second copy of the number.
    /// </remarks>
    public const int MarkerGap = symbolPadding;

    /// <summary>
    /// What a counter marker has appended to it: a full stop and a space.
    /// </summary>
    /// <remarks>
    /// The trailing space is not decoration — it is what separates the number from the item's text,
    /// and it is included in the width the marker is right-aligned by. Dropping it moves every
    /// number four and a half pixels right at 16px.
    /// </remarks>
    const string counterSuffix = ". ";

    /// <summary>
    /// Positions <paramref name="box"/>'s marker, once its content has been laid out.
    /// </summary>
    /// <remarks>
    /// Must run after the subtree is laid out, because the marker sits on the item's first line —
    /// wherever that line turned out to be, including inside a descendant block and below a
    /// margin that collapsed through.
    /// </remarks>
    public static void Place(LayoutBox box, FontSet fonts)
    {
        if (box.Marker is not {} marker)
        {
            return;
        }

        var style = box.Style;
        var face = fonts.Resolve(style.FontFamilies, style.FontWeight, style.Italic);
        var baseline = FirstBaseline(box, face);

        // The item's BORDER box, not its content box. A marker is placed outside the item's
        // padding as well as its border, so padding-left on an <li> indents the text and leaves
        // the bullet where it was.
        //
        // Under `inside` it is the CONTENT box instead, and the marker sits at the start of the
        // first line rather than before it — so padding does move it, because it is content now.
        var inside = style.ListStylePosition == ListStylePositionKind.Inside;
        var edge = inside
            ? box.ContentBox.X + Advance(style, face, marker)
            : box.BorderBox.X;

        if (style.HasSymbolMarker)
        {
            PlaceSymbol(marker, style, face, baseline, edge, inside);
            return;
        }

        PlaceCounter(marker, style, face, baseline, edge);
    }

    /// <summary>
    /// How much room an <c>inside</c> marker takes at the start of the first line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A counter takes the advance of its own text, <c>N. </c> with the trailing space, which is
    /// the same string the outside marker is right-aligned by. Measured to a hundredth of a pixel
    /// at three sizes, and it agrees because both sides shape the string rather than summing raw
    /// advances.
    /// </para>
    /// <para>
    /// A symbol takes <c>side + font-size + 1</c>, in whole pixels off the whole-pixel ascent —
    /// which is not derivable from anything and was measured at six sizes from 12px to 40px, where
    /// it is exact at every one. No fraction of the em fits: the ratio drifts from 1.375 to 1.325
    /// across that range because the symbol's own side moves in the uneven steps
    /// <see cref="SymbolSize"/> produces.
    /// </para>
    /// </remarks>
    public static float Advance(ComputedStyle style, FontFace face, ListMarker marker)
    {
        if (!style.HasSymbolMarker)
        {
            var text = Counter(marker.Kind, marker.Ordinal) + counterSuffix;
            return ShapedText.Create(face, text, style.FontSize).Width(0, text.Length);
        }

        return SymbolSize((int) face.Ascent(style.FontSize)) + style.FontSize + 1;
    }

    /// <summary>
    /// The room an <c>inside</c> marker needs on <paramref name="box"/>'s first line, or zero.
    /// </summary>
    /// <remarks>
    /// Read by <see cref="InlineLayout"/> before the line is filled, where
    /// <see cref="Place"/> runs after the whole subtree is laid out. The two have to agree, so the
    /// arithmetic lives in one place and both call it.
    /// </remarks>
    public static float Reserved(LayoutBox box, FontSet fonts)
    {
        if (box.Marker is not {} marker ||
            box.Style.ListStylePosition != ListStylePositionKind.Inside)
        {
            return 0;
        }

        var style = box.Style;
        return Advance(style, fonts.Resolve(style.FontFamilies, style.FontWeight, style.Italic), marker);
    }

    /// <summary>
    /// Places a disc, circle or square.
    /// </summary>
    /// <remarks>
    /// Sized and offset from the ascent alone, so a marker tracks the item's own font rather than
    /// whatever the first line happens to be set in — an item whose only child is 32px text still
    /// gets the 16px bullet its own style asks for.
    /// </remarks>
    static void PlaceSymbol(
        ListMarker marker,
        ComputedStyle style,
        FontFace face,
        float baseline,
        float edge,
        bool inside)
    {
        var ascent = (int) face.Ascent(style.FontSize);
        var size = SymbolSize(ascent);

        // Measured, not derived. The shape hangs above the baseline by rather less than half the
        // ascent, which is what keeps a bullet visually centred against lower-case text instead of
        // riding up level with the capitals.
        var top = baseline - ascent + 3 * (ascent - ascent * 2 / 3) / 2;

        // Outside, the marker's right edge is held clear of the item; inside, its LEFT edge starts
        // the line and the reserved advance was already added to `edge`, so the same number is
        // subtracted back off.
        var left = inside
            ? edge - Advance(style, face, marker)
            : edge - symbolPadding - ascent / 3 - size;

        marker.Bounds = new(left, top, size, size);
    }

    /// <summary>
    /// Places a number, letter or numeral.
    /// </summary>
    /// <remarks>
    /// Right-aligned so the END of the advance — the trailing space included — lands on the item's
    /// edge. Shaped through the same path as any other text, so the marker's glyphs come out of
    /// the same shaper that measured them.
    /// </remarks>
    static void PlaceCounter(
        ListMarker marker,
        ComputedStyle style,
        FontFace face,
        float baseline,
        float edge)
    {
        var text = Counter(marker.Kind, marker.Ordinal) + counterSuffix;
        var shaped = ShapedText.Create(face, text, style.FontSize);
        var width = shaped.Width(0, text.Length);
        var (glyphs, runText) = shaped.Slice(0, text.Length);

        var x = edge - width;
        var ascent = face.Ascent(style.FontSize);
        var descent = face.Descent(style.FontSize);

        marker.Bounds = new(x, baseline - ascent, width, ascent + descent);
        marker.Run = new(runText, style, face, x, baseline, width, Glyphs: glyphs);
    }

    /// <summary>
    /// The side of a symbol marker's square, in whole pixels.
    /// </summary>
    /// <remarks>
    /// Two thirds of the ascent, halved, rounded up by the <c>+ 1</c> before the halving. Reproduces
    /// Chrome exactly at every size measured, including the uneven steps its truncation produces —
    /// 14px and 15px text share a four-pixel bullet, then 15px and 16px share a five-pixel one.
    /// </remarks>
    static int SymbolSize(int ascent) =>
        (ascent * 2 / 3 + 1) / 2;

    /// <summary>
    /// The baseline the marker sits on: the item's first line, wherever layout put it.
    /// </summary>
    /// <remarks>
    /// A pre-order walk, so it finds the first line in document order however deeply it is nested.
    /// An item with no line at all — an empty <c>&lt;li&gt;</c> — falls back to where its own first
    /// line would have started, which is what Chrome draws.
    /// </remarks>
    static float FirstBaseline(LayoutBox box, FontFace face)
    {
        foreach (var descendant in box.Descendants())
        {
            if (descendant.Lines.Count > 0)
            {
                var line = descendant.Lines[0];
                return line.Bounds.Y + line.Baseline;
            }
        }

        // The strut's baseline, by the same half-leading rule inline layout uses. Floored for the
        // same reason: an exact half lands on .5 constantly, and half a pixel is a whole pixel once
        // it is rasterised.
        var style = box.Style;
        var lineHeight = style.ResolveLineHeight(face);
        var ascent = face.Ascent(style.FontSize);
        var descent = face.Descent(style.FontSize);

        return box.ContentBox.Y + MathF.Floor((lineHeight - (ascent + descent)) / 2) + ascent;
    }

    /// <summary>
    /// The counter text for <paramref name="ordinal"/>, without its suffix.
    /// </summary>
    /// <remarks>
    /// Public because generated content needs the same formatting: <c>counter(n, upper-roman)</c>
    /// has to produce the numeral a marker of that style would, or one document numbers its
    /// headings differently from its lists.
    /// </remarks>
    public static string Counter(ListStyleKind kind, int ordinal) =>
        kind switch
        {
            ListStyleKind.DecimalLeadingZero => ordinal.ToString("00", CultureInfo.InvariantCulture),
            ListStyleKind.LowerAlpha => Alphabetic(ordinal, 'a'),
            ListStyleKind.UpperAlpha => Alphabetic(ordinal, 'A'),
            ListStyleKind.LowerRoman => Roman(ordinal).ToLowerInvariant(),
            ListStyleKind.UpperRoman => Roman(ordinal),
            _ => ordinal.ToString(CultureInfo.InvariantCulture)
        };

    /// <summary>
    /// The bijective base-26 representation: a to z, then aa to az.
    /// </summary>
    /// <remarks>
    /// Bijective rather than ordinary base 26, which is why the arithmetic subtracts one before
    /// each division: there is no zero digit, so 26 is <c>z</c> and 27 is <c>aa</c>. Plain base 26
    /// would produce <c>a</c> followed by nothing for 26 and skip a value at every power.
    /// </remarks>
    static string Alphabetic(int ordinal, char first)
    {
        // Outside the range the style can express, the specification says to fall back to decimal
        // rather than to invent a glyph for it.
        if (ordinal < 1)
        {
            return ordinal.ToString(CultureInfo.InvariantCulture);
        }

        // Seven digits is the most an int can reach in bijective base 26, so the buffer is a fixed
        // one. The digits arrive least significant first, which is what the builder's Insert was
        // paying for; writing backwards into a span gets the same order for nothing.
        Span<char> digits = stackalloc char[7];
        var index = digits.Length;

        for (var value = ordinal; value > 0; value = (value - 1) / 26)
        {
            digits[--index] = (char) (first + (value - 1) % 26);
        }

        return new(digits[index..]);
    }

    /// <summary>The Roman numeral for <paramref name="ordinal"/>, in upper case.</summary>
    /// <remarks>
    /// Roman numerals have no representation for zero or for anything negative, and none above
    /// 3999 without notation this does not implement, so those fall back to decimal — which is what
    /// CSS requires of a counter style asked for a value outside its range.
    /// </remarks>
    static string Roman(int ordinal)
    {
        if (ordinal is < 1 or > 3999)
        {
            return ordinal.ToString(CultureInfo.InvariantCulture);
        }

        ReadOnlySpan<int> values = [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];

        // Two characters per value, in step with `values` above: a subtractive numeral carries
        // both and a plain one leaves the first empty. A table of one and two character STRINGS
        // reads better and is slower, because copying one out is a call into Buffer.Memmove for
        // the sake of a character or two — which is most of what a short numeral costs.
        CharSpan letters =
        [
            '\0', 'M', 'C', 'M', '\0', 'D', 'C', 'D', '\0', 'C', 'X', 'C', '\0', 'L',
            'X', 'L', '\0', 'X', 'I', 'X', '\0', 'V', 'I', 'V', '\0', 'I'
        ];

        // Fifteen characters is the longest numeral in range: 3888 is MMMDCCCLXXXVIII, and the
        // bound above is what makes the buffer a fixed one.
        Span<char> numeral = stackalloc char[15];
        var length = 0;
        var remaining = ordinal;

        for (var index = 0; index < values.Length; index++)
        {
            var prefix = letters[index * 2];
            var letter = letters[index * 2 + 1];

            while (remaining >= values[index])
            {
                if (prefix != '\0')
                {
                    numeral[length++] = prefix;
                }

                numeral[length++] = letter;
                remaining -= values[index];
            }
        }

        return new(numeral[..length]);
    }
}