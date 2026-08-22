/// <summary>
/// The marker a list item shows, and where layout put it.
/// </summary>
/// <remarks>
/// Not a <see cref="LayoutBox"/>, deliberately. A marker sits outside its item's principal box, so
/// it contributes nothing to the geometry of anything and does not appear in the browser's
/// <c>getBoundingClientRect()</c> — which means the corpus's box comparison cannot see it, and a
/// marker modelled as a box would show up there as an element the reference does not have. Only
/// the pixel comparison measures a marker.
/// </remarks>
sealed class ListMarker
{
    /// <summary>What this marker shows.</summary>
    public required ListStyleKind Kind { get; init; }

    /// <summary>
    /// This item's number within its list, for the counter styles.
    /// </summary>
    /// <remarks>
    /// Resolved while the tree is built rather than while it is painted, because it depends on
    /// document order among siblings — which the box tree, having dropped every
    /// <c>display: none</c> element and gained anonymous boxes the document never mentioned, is
    /// no longer a faithful record of.
    /// </remarks>
    public required int Ordinal { get; init; }

    /// <summary>
    /// The marker's ink box, in layout units.
    /// </summary>
    /// <remarks>
    /// For a symbol this is the shape's own square. For a counter it is the em box around
    /// <see cref="Run"/>, which is wanted for one thing only: deciding whether the marker falls on
    /// the page being painted.
    /// </remarks>
    public Rect Bounds { get; set; }

    /// <summary>
    /// The positioned glyphs, for a counter style. Null for a symbol, which is drawn as a shape.
    /// </summary>
    public TextRun? Run { get; set; }

    /// <summary>Moves the marker by the given offset.</summary>
    public void Translate(float dx, float dy)
    {
        Bounds = Bounds.Offset(dx, dy);

        if (Run is {} run)
        {
            Run = run with
            {
                X = run.X + dx,
                Y = run.Y + dy
            };
        }
    }
}

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
        var edge = box.BorderBox.X;

        if (style.HasSymbolMarker)
        {
            PlaceSymbol(marker, style, face, baseline, edge);
            return;
        }

        PlaceCounter(marker, style, face, baseline, edge);
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
        float edge)
    {
        var ascent = (int) face.Ascent(style.FontSize);
        var size = SymbolSize(ascent);

        // Measured, not derived. The shape hangs above the baseline by rather less than half the
        // ascent, which is what keeps a bullet visually centred against lower-case text instead of
        // riding up level with the capitals.
        var top = baseline - ascent + 3 * (ascent - ascent * 2 / 3) / 2;
        var right = edge - symbolPadding - ascent / 3;

        marker.Bounds = new(right - size, top, size, size);
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
    static string Counter(ListStyleKind kind, int ordinal) =>
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

        var builder = new StringBuilder();

        for (var value = ordinal; value > 0; value = (value - 1) / 26)
        {
            builder.Insert(0, (char) (first + (value - 1) % 26));
        }

        return builder.ToString();
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
        ReadOnlySpan<string> numerals =
            ["M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I"];

        var builder = new StringBuilder();
        var remaining = ordinal;

        for (var index = 0; index < values.Length; index++)
        {
            while (remaining >= values[index])
            {
                builder.Append(numerals[index]);
                remaining -= values[index];
            }
        }

        return builder.ToString();
    }
}

/// <summary>
/// Numbers the items of one list.
/// </summary>
/// <remarks>
/// Held by the box builder while it walks a list's children, rather than derived per item from the
/// DOM, so that an item's number follows from the items actually generated before it. That is what
/// makes a <c>display: none</c> item skip a number rather than consume one, and it is why the
/// counter is a mutable object passed down instead of a function of the element.
/// </remarks>
sealed class ListNumbering
{
    readonly int step;
    int next;

    ListNumbering(int first, int step)
    {
        next = first;
        this.step = step;
    }

    /// <summary>
    /// The numbering <paramref name="list"/>'s items take, honouring <c>start</c> and
    /// <c>reversed</c>.
    /// </summary>
    /// <remarks>
    /// A reversed list with no <c>start</c> counts down from the number of items it has, which is
    /// the only reason the items need counting before any of them is laid out.
    /// </remarks>
    public static ListNumbering For(IElement list)
    {
        var reversed = list.HasAttribute("reversed");
        var step = reversed ? -1 : 1;

        if (Number(list, "start") is {} start)
        {
            return new(start, step);
        }

        return new(reversed ? list.Children.Count(IsItem) : 1, step);
    }

    /// <summary>
    /// The number for <paramref name="item"/>, advancing the counter past it.
    /// </summary>
    /// <remarks>
    /// A <c>value</c> attribute does not just override this item's number: it moves the counter, so
    /// every item after it continues from there. That is what the HTML Standard specifies and what
    /// makes <c>value</c> usable for resuming an interrupted list.
    /// </remarks>
    public int Take(IElement item)
    {
        if (Number(item, "value") is {} value)
        {
            next = value;
        }

        var ordinal = next;
        next += step;
        return ordinal;
    }

    static bool IsItem(IElement element) =>
        element.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase);

    static int? Number(IElement element, string name) =>
        int.TryParse(
            element.GetAttribute(name),
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
}
