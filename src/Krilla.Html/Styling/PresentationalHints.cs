namespace Krilla.Html.Styling;

/// <summary>
/// Maps HTML's presentational content attributes onto the cascade, which AngleSharp does not do.
/// </summary>
/// <remarks>
/// <para>
/// HTML defines <c>&lt;table width&gt;</c>, <c>&lt;td bgcolor&gt;</c>, <c>&lt;p align&gt;</c> and
/// the rest as declarations in an origin of their own, above the user-agent sheet and below every
/// author rule. AngleSharp performs none of that mapping, so before this they reached the cascade
/// as nothing at all and were merely REPORTED — a poor answer for markup that reporting tools and
/// mail merges emit by the thousand.
/// </para>
/// <para>
/// The declaration <see cref="Apply"/> writes into is the one
/// <c>IStyleCollection.ComputeCascadedStyle</c> just returned, which is a fresh mutable object per
/// call rather than a view onto the cascade — so setting a property here affects this element's
/// resolution and nothing else.
/// </para>
/// <para>
/// A hint is applied only where the cascade has NOTHING for the property, which is what puts it
/// below every author rule. Where the user-agent sheet supplies a value the hint is meant to beat
/// — <c>table { border-spacing: 2px }</c>, <c>th { text-align: center }</c>,
/// <c>ol { list-style-type: decimal }</c> and the rest — the cascaded value is compared against
/// that known default instead, and the hint wins when the two agree. That is the same
/// separated-by-VALUE heuristic a pseudo-element's own declarations go through, and it carries the
/// same limitation: an author who restates the user-agent's own value loses to the attribute. The
/// alternative is filtering declarations by origin, which <c>ComputeCascadedStyle</c> does not
/// expose.
/// </para>
/// </remarks>
static class PresentationalHints
{
    static readonly string[] sides = ["top", "right", "bottom", "left"];

    /// <summary>
    /// The user-agent declarations a hint is allowed to overwrite, keyed by element and property.
    /// </summary>
    /// <remarks>
    /// Every entry was measured out of the cascade rather than read off a stylesheet, because two
    /// sheets contribute: AngleSharp's own, and <see cref="UserAgentStyles.Corrections"/>. An
    /// entry naming the wrong string silently stops its hint applying, which is why they are
    /// listed once here rather than spelled out at each call site.
    /// </remarks>
    static readonly Dictionary<string, string> defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["table/border-spacing"] = "2px",
        ["td/padding-top"] = "1px",
        ["td/padding-right"] = "1px",
        ["td/padding-bottom"] = "1px",
        ["td/padding-left"] = "1px",
        ["th/padding-top"] = "1px",
        ["th/padding-right"] = "1px",
        ["th/padding-bottom"] = "1px",
        ["th/padding-left"] = "1px",
        ["td/vertical-align"] = "inherit",
        ["th/vertical-align"] = "inherit",
        ["tr/vertical-align"] = "inherit",
        ["thead/vertical-align"] = "inherit",
        ["tbody/vertical-align"] = "inherit",
        ["tfoot/vertical-align"] = "inherit",
        ["th/text-align"] = "center",
        ["caption/text-align"] = "center"
    };

    /// <summary>
    /// Writes <paramref name="element"/>'s presentational attributes into
    /// <paramref name="declaration"/>.
    /// </summary>
    public static void Apply(IElement element, ICssStyleDeclaration declaration) =>
        Populate(new(element, declaration, null));

    /// <summary>
    /// A declaration nothing reads, for the reporting pass to write its candidate values into.
    /// </summary>
    /// <remarks>
    /// Whether a value is usable is a question only the CSS parser can answer — <c>silver</c> is a
    /// colour and <c>chucknorris</c> is not, and neither is anything this file could tell apart —
    /// so the reporting pass needs somewhere to try. An element that is never added to the
    /// document is the same route a page margin box and a pseudo-element's own declarations take.
    /// </remarks>
    static ICssStyleDeclaration Scratch(IElement element) =>
        element.Owner!.CreateElement("div").GetStyle();

    /// <summary>
    /// Reports the attributes this understands whose VALUE means nothing — <c>align="char"</c>, a
    /// <c>bgcolor</c> naming no colour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same walk <see cref="Apply"/> takes, writing nowhere. Running it twice rather than
    /// reporting from inside the application is what keeps the two gates separate: a hint applies
    /// to every element, and a report is owed only for an element a browser would have drawn — so
    /// this is called from the same place, and under the same <c>display: none</c> check, as every
    /// other attribute report.
    /// </para>
    /// <para>
    /// Reported here rather than by <see cref="UnsupportedAttributes"/>, which is left with the
    /// attributes that reach no property at all. Splitting it that way is what keeps the
    /// diagnostic invariant true through the change: an attribute stops being reported exactly
    /// when it starts being applied.
    /// </para>
    /// </remarks>
    public static void Report(IElement element, Action<HtmlDiagnostic>? sink) =>
        Populate(new(element, Scratch(element), sink));

    static void Populate(Hints hints)
    {
        switch (hints.Element.LocalName)
        {
            case "table":
                Table(hints);
                break;
            case "td":
            case "th":
                Cell(hints);
                break;
            case "tr":
            case "thead":
            case "tbody":
            case "tfoot":
                Row(hints);
                break;
            case "caption":
                Caption(hints);
                break;
            case "p":
            case "div":
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                hints.Map("align", Align, "text-align");
                break;
        }
    }

    /// <summary>
    /// One element's hints in progress: the attributes to read, the declaration to write, and
    /// where an unusable value is reported. Exactly one of the two is supplied.
    /// </summary>
    sealed class Hints(
        IElement element,
        ICssStyleDeclaration declaration,
        Action<HtmlDiagnostic>? sink)
    {
        public IElement Element => element;

        /// <summary>The raw value of <paramref name="attribute"/>, or null when it is absent.</summary>
        public string? Raw(string attribute) =>
            element.GetAttribute(attribute);

        /// <summary>
        /// Sets <paramref name="property"/>, unless an author rule already decided it.
        /// </summary>
        public void Set(string property, string value)
        {
            var declared = declaration.GetPropertyValue(property);

            if (declared.Length != 0 &&
                !(defaults.TryGetValue($"{element.LocalName}/{property}", out var fallback) &&
                  declared.Equals(fallback, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            declaration.SetProperty(property, value);
        }

        /// <summary>
        /// Reads <paramref name="attribute"/>, converts it, and writes the result to every named
        /// property. Reports it when it is present and converts to nothing.
        /// </summary>
        public void Map(string attribute, Func<string, string?> convert, params string[] properties)
        {
            if (element.GetAttribute(attribute) is not {} raw)
            {
                return;
            }

            if (convert(raw) is not {} value)
            {
                Report(attribute, raw);
                return;
            }

            foreach (var property in properties)
            {
                Set(property, value);
            }

            // A property still empty is one the CSS parser refused the value for, which is the
            // only failure this cannot see coming. An author rule winning leaves it non-empty, so
            // the two cases stay apart without either being tested for.
            if (declaration.GetPropertyValue(properties[0]).Length == 0)
            {
                Report(attribute, raw);
            }
        }

        public void Report(string attribute, string? value) =>
            Diagnostic.Attribute(
                sink,
                element.LocalName,
                attribute,
                value is {Length: > 0} ? value : null,
                "names nothing this maps onto CSS");
    }

    static void Table(Hints hints)
    {
        hints.Map("width", Pixels, "width");
        hints.Map("height", Pixels, "height");
        hints.Map("bgcolor", Color, "background-color");
        hints.Map("cellspacing", Spacing, "border-spacing");

        // `align` FLOATS a table, where the same attribute on a paragraph aligns its text.
        // Centring is the exception and is the auto-margin idiom, a floated box having no centre.
        if (hints.Raw("align") is {} align)
        {
            switch (align.Trim().ToLowerInvariant())
            {
                case "left":
                    hints.Set("float", "left");
                    break;
                case "right":
                    hints.Set("float", "right");
                    break;
                case "center":
                    hints.Set("margin-left", "auto");
                    hints.Set("margin-right", "auto");
                    break;
                default:
                    hints.Report("align", align);
                    break;
            }
        }

        if (hints.Raw("border") is {} declared)
        {
            if (Integer(declared) is not {} border)
            {
                hints.Report("border", declared);
            }
            else
            {
                foreach (var side in sides)
                {
                    hints.Set($"border-{side}-width", $"{border}px");
                    hints.Set($"border-{side}-style", border > 0 ? "outset" : "none");
                }
            }
        }
    }

    static void Cell(Hints hints)
    {
        hints.Map("width", Pixels, "width");
        hints.Map("height", Pixels, "height");
        hints.Map("bgcolor", Color, "background-color");
        hints.Map("align", Align, "text-align");
        hints.Map("valign", VerticalAlign, "vertical-align");

        if (hints.Element.HasAttribute("nowrap"))
        {
            hints.Set("white-space", "nowrap");
        }

        // `cellpadding` and `border` are declared on the TABLE and land on its cells, which is the
        // one place a hint is not a property of the element carrying the attribute. A cell reached
        // through `display: table-cell` rather than through markup has no such ancestor and gets
        // nothing, which is also what a browser does.
        if (hints.Element.Closest("table") is not {} table)
        {
            return;
        }

        if (Integer(table.GetAttribute("cellpadding")) is {} padding)
        {
            foreach (var side in sides)
            {
                hints.Set($"padding-{side}", $"{padding}px");
            }
        }

        if (Integer(table.GetAttribute("border")) > 0)
        {
            foreach (var side in sides)
            {
                hints.Set($"border-{side}-width", "1px");
                hints.Set($"border-{side}-style", "inset");
            }
        }
    }

    static void Row(Hints hints)
    {
        hints.Map("height", Pixels, "height");
        hints.Map("bgcolor", Color, "background-color");
        hints.Map("align", Align, "text-align");
        hints.Map("valign", VerticalAlign, "vertical-align");
    }

    static void Caption(Hints hints)
    {
        // The only attribute here that maps onto two different properties depending on its value:
        // `top` and `bottom` choose the side the caption sits on, `left` and `right` align its
        // text.
        if (hints.Raw("align") is not {} align)
        {
            return;
        }

        switch (align.Trim().ToLowerInvariant())
        {
            case "top":
                hints.Set("caption-side", "top");
                break;
            case "bottom":
                hints.Set("caption-side", "bottom");
                break;
            case "left":
            case "right":
                hints.Map("align", Align, "text-align");
                break;
            default:
                hints.Report("align", align);
                break;
        }
    }

    static string? Spacing(string value) =>
        Integer(value) is {} spacing ? $"{spacing}px" : null;

    /// <summary>
    /// A dimension attribute as a CSS length: a bare number is pixels, and a percentage stays one.
    /// </summary>
    static string? Pixels(string value)
    {
        var text = value.Trim();

        if (text.EndsWith('%'))
        {
            return Integer(text[..^1]) is {} percent ? $"{percent}%" : null;
        }

        return Integer(text) is {} pixels ? $"{pixels}px" : null;
    }

    /// <summary>
    /// HTML's non-negative integer parse: leading digits win and trailing rubbish is ignored,
    /// which is what makes <c>width="300px"</c> in a legacy document mean 300.
    /// </summary>
    static int? Integer(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var text = value.Trim();
        var length = 0;

        while (length < text.Length && char.IsAsciiDigit(text[length]))
        {
            length++;
        }

        return length == 0 ? null : int.Parse(text[..length]);
    }

    static string? Align(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "left" => "left",
            "right" => "right",
            "center" or "middle" => "center",
            "justify" => "justify",
            _ => null
        };

    static string? VerticalAlign(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "top" => "top",
            "middle" => "middle",
            "bottom" => "bottom",
            "baseline" => "baseline",
            _ => null
        };

    /// <summary>
    /// HTML's legacy colour parse, narrowed to what a document actually contains: the value as
    /// written, and a bare <c>ff0000</c> given the hash it omitted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Whether the value is a colour at all is left to the CSS parser, which is the only thing
    /// that knows the named ones — and it has to be, because the cascade normalises <c>silver</c>
    /// into an <c>rgba()</c> long before anything here would see it.
    /// </para>
    /// <para>
    /// The full legacy algorithm truncates, pads and reinterprets anything at all as a colour —
    /// <c>bgcolor="chucknorris"</c> famously being red. That is faithful and useless: a value
    /// nothing recognises is far more likely to be a mistake than a colour, and turning it into one
    /// would paint a background nobody asked for. It is reported instead.
    /// </para>
    /// </remarks>
    static string? Color(string value)
    {
        var text = value.Trim();

        if (text.Length == 0)
        {
            return null;
        }

        return Hex(text) ? $"#{text}" : text;
    }

    /// <summary>Whether <paramref name="text"/> is a hash colour written without its hash.</summary>
    static bool Hex(string text)
    {
        if (text.Length is not (3 or 4 or 6 or 8))
        {
            return false;
        }

        foreach (var digit in text)
        {
            if (!char.IsAsciiHexDigit(digit))
            {
                return false;
            }
        }

        return true;
    }
}
