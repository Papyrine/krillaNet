namespace Krilla.Html.Styling;

/// <summary>
/// Turns AngleSharp.Css's cascade result into a <see cref="ComputedStyle"/>.
/// </summary>
/// <remarks>
/// <para>
/// The division of labour: AngleSharp.Css runs selector matching, specificity and the cascade,
/// which is the part with all the specification detail in it. What comes back is a property bag of
/// strings, which this resolves into usable values and narrows to the properties the engine
/// honours.
/// </para>
/// <para>
/// It reads the CASCADED style, not the computed one. That distinction is load-bearing:
/// <c>ComputeCurrentStyle</c> resolves percentages against the render device's viewport, so
/// <c>width: 25%</c> inside a 600px container comes back as 204px — a quarter of the page rather
/// than a quarter of the container. Percentages resolve against a containing block, which is a
/// layout result AngleSharp has no way to know. The cascaded style leaves them as <c>25%</c>, and
/// <see cref="CssLength"/> carries them into layout to be resolved where the answer exists.
/// </para>
/// <para>
/// The cascaded style also carries no inherited values, which suits: inheritance happens here
/// against the parent style, where the resolved parent value is already known.
/// </para>
/// <para>
/// Resolution order matters: <c>font-size</c> is computed first because every <c>em</c> in the
/// same declaration is relative to it, not to the parent's. Getting that backwards makes
/// <c>padding: 1em</c> on an element with a larger <c>font-size</c> silently wrong.
/// </para>
/// </remarks>
static class StyleResolver
{
    /// <summary>
    /// Resolves <paramref name="element"/>'s style against its <paramref name="parent"/>.
    /// </summary>
    public static ComputedStyle Resolve(IElement element, ComputedStyle parent, DocumentContext context)
    {
        var declaration = context.Cascade(element);
        var rootFontSize = context.RootFontSize;

        var fontSize = ResolveFontSize(declaration, parent, rootFontSize);
        var color = CssValues.ParseColor(declaration.GetPropertyValue("color")) ?? parent.Color;

        var families = CssValues.ParseFamilies(declaration.GetPropertyValue("font-family"));
        if (families.Count == 0)
        {
            families = [.. parent.FontFamilies];
        }

        return new()
        {
            Display = ParseDisplay(declaration.GetPropertyValue("display"), element.LocalName),
            MarginTop = Length(declaration, "margin-top", fontSize, rootFontSize),
            MarginRight = Length(declaration, "margin-right", fontSize, rootFontSize),
            MarginBottom = Length(declaration, "margin-bottom", fontSize, rootFontSize),
            MarginLeft = Length(declaration, "margin-left", fontSize, rootFontSize),
            PaddingTop = Length(declaration, "padding-top", fontSize, rootFontSize),
            PaddingRight = Length(declaration, "padding-right", fontSize, rootFontSize),
            PaddingBottom = Length(declaration, "padding-bottom", fontSize, rootFontSize),
            PaddingLeft = Length(declaration, "padding-left", fontSize, rootFontSize),
            BorderTop = BorderWidth(declaration, "top", fontSize, rootFontSize),
            BorderRight = BorderWidth(declaration, "right", fontSize, rootFontSize),
            BorderBottom = BorderWidth(declaration, "bottom", fontSize, rootFontSize),
            BorderLeft = BorderWidth(declaration, "left", fontSize, rootFontSize),
            BorderTopColor = BorderColor(declaration, "top", color),
            BorderRightColor = BorderColor(declaration, "right", color),
            BorderBottomColor = BorderColor(declaration, "bottom", color),
            BorderLeftColor = BorderColor(declaration, "left", color),
            Width = Length(declaration, "width", fontSize, rootFontSize, CssLength.Auto),
            Height = Length(declaration, "height", fontSize, rootFontSize, CssLength.Auto),
            MaxWidth = Length(declaration, "max-width", fontSize, rootFontSize, CssLength.None),
            MinWidth = Length(declaration, "min-width", fontSize, rootFontSize),
            BackgroundColor = CssValues.ParseColor(declaration.GetPropertyValue("background-color")),
            Color = color,
            FontFamilies = families,
            FontSize = fontSize,
            FontWeight = ParseWeight(
                declaration.GetPropertyValue("font-weight"),
                UserAgentStyles.IsBold(element.LocalName) ? 700 : parent.FontWeight),
            Italic = ParseItalic(
                declaration.GetPropertyValue("font-style"),
                UserAgentStyles.IsItalic(element.LocalName) || parent.Italic),
            LineHeight = ParseLineHeight(declaration.GetPropertyValue("line-height"), fontSize, rootFontSize),
            Underline = ParseUnderline(declaration, parent.Underline),
            ListStyle = ParseListStyle(declaration.GetPropertyValue("list-style-type"), parent.ListStyle),
            BorderSpacingX = Spacing(declaration, "border-spacing", fontSize, rootFontSize, first: true)
                             ?? parent.BorderSpacingX,
            BorderSpacingY = Spacing(declaration, "border-spacing", fontSize, rootFontSize, first: false)
                             ?? parent.BorderSpacingY,
            TableLayout = declaration.GetPropertyValue("table-layout")?.Trim().ToLowerInvariant() == "fixed"
                ? TableLayoutKind.Fixed
                : TableLayoutKind.Auto,
            VerticalAlign = ParseVerticalAlign(
                declaration.GetPropertyValue("vertical-align"),
                UserAgentStyles.DefaultVerticalAlign(element.LocalName) ?? parent.VerticalAlign),
            TextAlign = ParseTextAlign(declaration.GetPropertyValue("text-align"), parent.TextAlign),
            WhiteSpace = ParseWhiteSpace(declaration.GetPropertyValue("white-space"), parent.WhiteSpace)
        };
    }

    /// <summary>
    /// The style a text node takes: its parent's, unchanged.
    /// </summary>
    /// <remarks>
    /// Text has no element of its own to cascade against, and every property that affects how it
    /// is measured and painted — font, colour, alignment, white space — is inherited. So the
    /// parent's resolved style is not an approximation here, it is the answer.
    /// </remarks>
    public static ComputedStyle ForText(ComputedStyle parent) =>
        parent;

    static float ResolveFontSize(ICssStyleDeclaration declaration, ComputedStyle parent, float rootFontSize)
    {
        var value = declaration.GetPropertyValue("font-size");
        if (string.IsNullOrWhiteSpace(value))
        {
            return parent.FontSize;
        }

        // A relative font-size resolves against the PARENT's size, unlike every other em in the
        // same declaration, which resolves against the size being computed here.
        var length = CssValues.ParseLength(value, parent.FontSize, rootFontSize, CssLength.Zero);
        return length.Kind switch
        {
            LengthKind.Absolute => length.Value,
            LengthKind.Percent => parent.FontSize * length.Value / 100f,
            _ => parent.FontSize
        };
    }

    static CssLength Length(
        ICssStyleDeclaration declaration,
        string property,
        float fontSize,
        float rootFontSize,
        CssLength? fallback = null) =>
        CssValues.ParseLength(
            declaration.GetPropertyValue(property),
            fontSize,
            rootFontSize,
            fallback ?? CssLength.Zero);

    /// <summary>
    /// A border edge's width, which is zero whenever its style is <c>none</c> or <c>hidden</c>
    /// however wide it was declared.
    /// </summary>
    /// <remarks>
    /// Folding the style into the width here means layout never has to consult border-style at
    /// all — an edge that does not paint also does not take space, which is exactly the CSS rule.
    /// </remarks>
    static float BorderWidth(ICssStyleDeclaration declaration, string side, float fontSize, float rootFontSize)
    {
        var style = declaration.GetPropertyValue($"border-{side}-style");
        if (string.IsNullOrWhiteSpace(style) ||
            style.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            style.Equals("hidden", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var value = declaration.GetPropertyValue($"border-{side}-width");

        // The keyword widths, which CSS fixes at these pixel values.
        var keyword = value?.Trim().ToLowerInvariant();
        if (keyword is "thin")
        {
            return 1;
        }

        if (keyword is "medium")
        {
            return 3;
        }

        if (keyword is "thick")
        {
            return 5;
        }

        return CssValues.ParseLength(value, fontSize, rootFontSize, CssLength.Zero).Resolve(0);
    }

    static Color? BorderColor(ICssStyleDeclaration declaration, string side, Color inherited)
    {
        var value = declaration.GetPropertyValue($"border-{side}-color");

        // border-color defaults to currentColor, so an unset edge takes the element's own colour
        // rather than disappearing.
        if (string.IsNullOrWhiteSpace(value) ||
            value.Equals("currentcolor", StringComparison.OrdinalIgnoreCase))
        {
            return inherited;
        }

        return CssValues.ParseColor(value);
    }

    static DisplayKind ParseDisplay(string? value, string localName) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "none" => DisplayKind.None,
            "inline" => DisplayKind.Inline,
            "block" => DisplayKind.Block,
            "list-item" => DisplayKind.ListItem,
            "table" or "inline-table" => DisplayKind.Table,
            "table-caption" => DisplayKind.TableCaption,
            "table-header-group" => DisplayKind.TableHeaderGroup,
            "table-row-group" => DisplayKind.TableRowGroup,
            "table-footer-group" => DisplayKind.TableFooterGroup,
            "table-row" => DisplayKind.TableRow,
            "table-cell" => DisplayKind.TableCell,
            "table-column" or "table-column-group" => DisplayKind.TableColumn,
            // Nothing in the cascade said, so the element's own default decides. AngleSharp.Css
            // has no display for the inline elements, and treating that silence as `block` puts
            // every <b> and <span> on a line of its own.
            null or "" => UserAgentStyles.Display(localName) ?? DisplayKind.Block,
            // Everything not yet implemented — flex, grid, table, inline-block — lays out as a
            // block. A wrong box beats no box: it keeps the content on the page and shows up in
            // the corpus comparison as a geometry difference rather than as silently missing
            // content that nothing measures.
            _ => DisplayKind.Block
        };

    /// <summary>
    /// The marker a list item shows, inheriting when the cascade said nothing.
    /// </summary>
    /// <remarks>
    /// Inherited explicitly, like every other inherited property here, because the cascaded style
    /// carries no inherited values. It matters more than usual for this one: the nesting defaults
    /// are declared on <c>ul</c> and <c>ol</c> rather than on <c>li</c>, so a marker that does not
    /// inherit is a marker that never reaches the item drawing it.
    /// </remarks>
    static ListStyleKind ParseListStyle(string? value, ListStyleKind inherited) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" => inherited,
            "none" => ListStyleKind.None,
            "disc" => ListStyleKind.Disc,
            "circle" => ListStyleKind.Circle,
            "square" => ListStyleKind.Square,
            "decimal" => ListStyleKind.Decimal,
            "decimal-leading-zero" => ListStyleKind.DecimalLeadingZero,
            "lower-alpha" or "lower-latin" => ListStyleKind.LowerAlpha,
            "upper-alpha" or "upper-latin" => ListStyleKind.UpperAlpha,
            "lower-roman" => ListStyleKind.LowerRoman,
            "upper-roman" => ListStyleKind.UpperRoman,
            // An unimplemented counter style still marks its items rather than losing them, on the
            // same reasoning as an unimplemented `display`: a wrong marker is visible and a missing
            // one is not.
            _ => ListStyleKind.Disc
        };

    /// <summary>
    /// One axis of <c>border-spacing</c>, which is one or two lengths.
    /// </summary>
    /// <remarks>
    /// Null rather than zero when the property is absent, so the caller can tell "not declared"
    /// from "declared as zero" and inherit only in the first case. A single length applies to both
    /// axes, which is why the second falls back to the first rather than to zero.
    /// </remarks>
    static float? Spacing(
        ICssStyleDeclaration declaration,
        string property,
        float fontSize,
        float rootFontSize,
        bool first)
    {
        var value = declaration.GetPropertyValue(property);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var part = first || parts.Length < 2 ? parts[0] : parts[1];
        return CssValues.ParseLength(part, fontSize, rootFontSize, CssLength.Zero).Resolve(0);
    }

    /// <summary>
    /// How a cell's content sits in a taller row.
    /// </summary>
    /// <remarks>
    /// Treated as inherited, which <c>vertical-align</c> is not. The reason is the user-agent
    /// stylesheet: it gives cells <c>vertical-align: inherit</c> so that a value set on a
    /// <c>tr</c> or on the table reaches them, and inheriting here is what reproduces that without
    /// implementing <c>inherit</c> as a general keyword. It is only ever read on a cell, so the
    /// difference does not reach anything else.
    /// </remarks>
    static VerticalAlignKind ParseVerticalAlign(string? value, VerticalAlignKind inherited) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "inherit" => inherited,
            "top" => VerticalAlignKind.Top,
            "middle" => VerticalAlignKind.Middle,
            "bottom" => VerticalAlignKind.Bottom,
            // The inline-level values — sub, super, text-top and the lengths — are not implemented,
            // and land on the initial value rather than on something arbitrary.
            _ => VerticalAlignKind.Baseline
        };

    static int ParseWeight(string? value, int inherited)
    {
        var text = value?.Trim().ToLowerInvariant();

        return text switch
        {
            null or "" => inherited,
            "normal" => 400,
            "bold" => 700,
            // Relative to the parent, and clamped to the range CSS defines for the keywords.
            "bolder" => inherited < 400 ? 400 : inherited < 600 ? 700 : 900,
            "lighter" => inherited > 700 ? 700 : inherited > 500 ? 400 : 100,
            _ => CssValues.TryParseNumber(text, out var weight)
                ? Math.Clamp((int) weight, 1, 1000)
                : inherited
        };
    }

    static bool ParseItalic(string? value, bool inherited)
    {
        var text = value?.Trim().ToLowerInvariant();

        return text switch
        {
            null or "" => inherited,
            "normal" => false,
            _ => text.StartsWith("italic", StringComparison.Ordinal) ||
                 text.StartsWith("oblique", StringComparison.Ordinal)
        };
    }

    /// <summary>
    /// Line height in pixels, or null for <c>normal</c>.
    /// </summary>
    /// <remarks>
    /// A unitless number is a multiplier of the element's own font size, and the distinction from
    /// a length matters on inheritance: the number inherits and is re-multiplied by each
    /// descendant's size, whereas a length inherits as a fixed pixel value. Only the former is
    /// resolved here because AngleSharp has already inherited whichever one applies.
    /// </remarks>
    static float? ParseLineHeight(string? value, float fontSize, float rootFontSize)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text) || text.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (CssValues.TryParseNumber(text, out var multiplier))
        {
            return multiplier * fontSize;
        }

        var length = CssValues.ParseLength(text, fontSize, rootFontSize, CssLength.Zero);
        return length.Kind == LengthKind.Percent
            ? fontSize * length.Value / 100f
            : length.Resolve(fontSize);
    }

    /// <summary>
    /// Whether an underline applies, from <c>text-decoration-line</c> or the
    /// <c>text-decoration</c> shorthand.
    /// </summary>
    /// <remarks>
    /// Both are read because which one the cascade reports depends on which the author wrote,
    /// and the shorthand also carries colour and style keywords that have to be looked past.
    /// Only underline is honoured; overline and line-through are not painted, so recognising
    /// them would claim support that is not there.
    /// </remarks>
    static bool ParseUnderline(ICssStyleDeclaration declaration, bool inherited)
    {
        var value = declaration.GetPropertyValue("text-decoration-line");

        if (string.IsNullOrWhiteSpace(value))
        {
            value = declaration.GetPropertyValue("text-decoration");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return inherited;
        }

        var text = value.ToLowerInvariant();

        if (text.Contains("underline", StringComparison.Ordinal))
        {
            return true;
        }

        // An explicit `none` clears an inherited underline; anything else leaves it alone.
        return !text.Contains("none", StringComparison.Ordinal) && inherited;
    }

    static TextAlignKind ParseTextAlign(string? value, TextAlignKind inherited) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "center" => TextAlignKind.Center,
            "right" or "end" => TextAlignKind.Right,
            "justify" => TextAlignKind.Justify,
            "left" or "start" => TextAlignKind.Left,
            _ => inherited
        };

    static WhiteSpaceKind ParseWhiteSpace(string? value, WhiteSpaceKind inherited) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "pre" => WhiteSpaceKind.Pre,
            "pre-wrap" => WhiteSpaceKind.PreWrap,
            "pre-line" => WhiteSpaceKind.PreLine,
            "nowrap" => WhiteSpaceKind.NoWrap,
            "normal" => WhiteSpaceKind.Normal,
            _ => inherited
        };
}
