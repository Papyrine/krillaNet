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
        var root = context.Root;

        var fontSize = ResolveFontSize(declaration, parent, root);
        var color = CssValues.ParseColor(declaration.GetPropertyValue("color")) ?? parent.Color;

        var families = CssValues.ParseFamilies(declaration.GetPropertyValue("font-family"));
        if (families.Count == 0)
        {
            families = [.. parent.FontFamilies];
        }

        var lineHeight = ParseLineHeight(
            declaration.GetPropertyValue("line-height"),
            fontSize,
            root,
            parent);

        var style = new ComputedStyle
        {
            Display = ParseDisplay(declaration.GetPropertyValue("display"), element.LocalName),
            MarginTop = Length(declaration, "margin-top", fontSize, root),
            MarginRight = Length(declaration, "margin-right", fontSize, root),
            MarginBottom = Length(declaration, "margin-bottom", fontSize, root),
            MarginLeft = Length(declaration, "margin-left", fontSize, root),
            PaddingTop = Length(declaration, "padding-top", fontSize, root),
            PaddingRight = Length(declaration, "padding-right", fontSize, root),
            PaddingBottom = Length(declaration, "padding-bottom", fontSize, root),
            PaddingLeft = Length(declaration, "padding-left", fontSize, root),
            BorderTop = BorderWidth(declaration, "top", fontSize, root),
            BorderRight = BorderWidth(declaration, "right", fontSize, root),
            BorderBottom = BorderWidth(declaration, "bottom", fontSize, root),
            BorderLeft = BorderWidth(declaration, "left", fontSize, root),
            RadiusTopLeft = Radius(declaration, "top-left", fontSize, root),
            RadiusTopRight = Radius(declaration, "top-right", fontSize, root),
            RadiusBottomRight = Radius(declaration, "bottom-right", fontSize, root),
            RadiusBottomLeft = Radius(declaration, "bottom-left", fontSize, root),
            OutlineWidth = OutlineWidth(declaration, fontSize, root),
            OutlineColor = CssValues.ParseColor(declaration.GetPropertyValue("outline-color")) ?? color,
            OutlineOffset = Length(declaration, "outline-offset", fontSize, root).Resolve(0),
            BorderCollapse = declaration.GetPropertyValue("border-collapse")
                .Trim()
                .Equals("collapse", StringComparison.OrdinalIgnoreCase)
                ? BorderCollapseKind.Collapse
                : parent.BorderCollapse,
            CaptionSide = declaration.GetPropertyValue("caption-side")
                .Trim()
                .Equals("bottom", StringComparison.OrdinalIgnoreCase)
                ? CaptionSideKind.Bottom
                : CaptionSideKind.Top,
            ListStylePosition = ParseListPosition(
                declaration.GetPropertyValue("list-style-position"),
                parent.ListStylePosition),
            ObjectFit = ParseObjectFit(declaration.GetPropertyValue("object-fit")),
            ObjectPositionX = Position(declaration, "object-position", fontSize, root, horizontal: true),
            ObjectPositionY = Position(declaration, "object-position", fontSize, root, horizontal: false),
            HideEmptyCells = EmptyCells(declaration, parent.HideEmptyCells),
            BorderTopStyle = BorderStyle(declaration, "top"),
            BorderRightStyle = BorderStyle(declaration, "right"),
            BorderBottomStyle = BorderStyle(declaration, "bottom"),
            BorderLeftStyle = BorderStyle(declaration, "left"),
            BorderTopColor = BorderColor(declaration, "top", color),
            BorderRightColor = BorderColor(declaration, "right", color),
            BorderBottomColor = BorderColor(declaration, "bottom", color),
            BorderLeftColor = BorderColor(declaration, "left", color),
            BoxSizing = ParseBoxSizing(declaration.GetPropertyValue("box-sizing")),
            Width = Length(declaration, "width", fontSize, root, CssLength.Auto),
            Height = Length(declaration, "height", fontSize, root, CssLength.Auto),
            MaxWidth = Length(declaration, "max-width", fontSize, root, CssLength.None),
            MinWidth = Length(declaration, "min-width", fontSize, root),
            MaxHeight = Length(declaration, "max-height", fontSize, root, CssLength.None),
            MinHeight = Length(declaration, "min-height", fontSize, root),
            // Inherited, so an absent declaration takes the parent's rather than zero.
            TextIndent = Length(declaration, "text-indent", fontSize, root, parent.TextIndent),
            BackgroundColor = CssValues.ParseColor(declaration.GetPropertyValue("background-color")),
            BackgroundImage = CssGradient.Parse(
                declaration.GetPropertyValue("background-image"),
                fontSize,
                root),
            BackgroundPicture = Picture(declaration, context, "background-image"),
            MarkerImage = Picture(declaration, context, "list-style-image") ?? parent.MarkerImage,
            BackgroundClip = Area(declaration, "background-clip", BoxArea.Border),
            BackgroundOrigin = Area(declaration, "background-origin", BoxArea.Padding),
            BackgroundRepeatX = Repeats(declaration, horizontal: true),
            BackgroundRepeatY = Repeats(declaration, horizontal: false),
            BackgroundPositionX = Position(declaration, "background-position", fontSize, root, horizontal: true),
            BackgroundPositionY = Position(declaration, "background-position", fontSize, root, horizontal: false),
            BackgroundSize = Sizing(declaration),
            BackgroundSizeX = SizeComponent(declaration, fontSize, root, first: true),
            BackgroundSizeY = SizeComponent(declaration, fontSize, root, first: false),
            Color = color,
            FontFamilies = families,
            FontSize = fontSize,
            FontWeight = ParseWeight(
                declaration.GetPropertyValue("font-weight"),
                UserAgentStyles.IsBold(element.LocalName) ? 700 : parent.FontWeight),
            Italic = ParseItalic(
                declaration.GetPropertyValue("font-style"),
                UserAgentStyles.IsItalic(element.LocalName) || parent.Italic),
            LineHeight = lineHeight.Absolute,
            LineHeightScale = lineHeight.Scale,
            TabSize = ParseTabSize(declaration.GetPropertyValue("tab-size"), parent.TabSize),
            WordBreaking = ParseWordBreaking(declaration, parent.WordBreaking),
            Decorations = ParseDecorations(declaration, parent.Decorations),
            DecorationColor = DecorationColour(declaration, parent, color),
            DecorationStyle = DecorationRule(declaration, parent),
            ListStyle = ParseListStyle(declaration.GetPropertyValue("list-style-type"), parent.ListStyle),
            BorderSpacingX = Spacing(declaration, "border-spacing", fontSize, root, first: true)
                             ?? parent.BorderSpacingX,
            BorderSpacingY = Spacing(declaration, "border-spacing", fontSize, root, first: false)
                             ?? parent.BorderSpacingY,
            TableLayout = ParseTableLayout(declaration.GetPropertyValue("table-layout")),
            VerticalAlign = ParseVerticalAlign(
                declaration.GetPropertyValue("vertical-align"),
                UserAgentStyles.DefaultVerticalAlign(element.LocalName) ?? parent.VerticalAlign,
                fontSize,
                root),
            VerticalAlignOffset = CssValues.ParseLength(
                declaration.GetPropertyValue("vertical-align"),
                fontSize,
                root,
                CssLength.Zero),
            VerticalAlignDeclared =
                !string.IsNullOrWhiteSpace(declaration.GetPropertyValue("vertical-align")),
            TextAlign = ParseTextAlign(declaration.GetPropertyValue("text-align"), parent.TextAlign),
            WhiteSpace = ParseWhiteSpace(declaration.GetPropertyValue("white-space"), parent.WhiteSpace),
            Float = ParseFloat(declaration.GetPropertyValue("float")),
            Clear = ParseClear(declaration.GetPropertyValue("clear")),
            BreakBefore = ParseBreak(declaration, "break-before"),
            BreakAfter = ParseBreak(declaration, "break-after"),
            BreakInside = ParseBreak(declaration, "break-inside"),
            Visibility = ParseVisibility(declaration.GetPropertyValue("visibility"), parent.Visibility),
            Opacity = ParseOpacity(declaration.GetPropertyValue("opacity")),
            Transform = CssTransform.Parse(
                declaration.GetPropertyValue("transform"),
                declaration.GetPropertyValue("transform-origin"),
                fontSize,
                root),
            TextTransform = ParseTextTransform(
                declaration.GetPropertyValue("text-transform"),
                parent.TextTransform),
            LetterSpacing = Advance(declaration, "letter-spacing", fontSize, root)
                            ?? parent.LetterSpacing,
            WordSpacing = Advance(declaration, "word-spacing", fontSize, root)
                          ?? parent.WordSpacing,
            Overflow = ParseOverflow(declaration),
            Position = ParsePosition(declaration.GetPropertyValue("position")),
            Top = Length(declaration, "top", fontSize, root, CssLength.Auto),
            Right = Length(declaration, "right", fontSize, root, CssLength.Auto),
            Bottom = Length(declaration, "bottom", fontSize, root, CssLength.Auto),
            Left = Length(declaration, "left", fontSize, root, CssLength.Auto)
        };

        // After the style, not before: the scan reports against what the element resolved to, and
        // a table cell has to be recognised as one before its vertical-align can be judged.
        if (context.Reports)
        {
            UnsupportedCss.Report(element, declaration, style, context.OnDiagnostic);
        }

        return style;
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

    static float ResolveFontSize(ICssStyleDeclaration declaration, ComputedStyle parent, CssRoot root)
    {
        var value = declaration.GetPropertyValue("font-size");
        if (string.IsNullOrWhiteSpace(value))
        {
            return parent.FontSize;
        }

        // A relative font-size resolves against the PARENT's size, unlike every other em in the
        // same declaration, which resolves against the size being computed here.
        //
        // The fallback is None rather than Zero so that the `_` branch below can catch an
        // unparseable value. Zero is an ABSOLUTE length, so it took the first branch instead and
        // returned a font size of 0 — which is not a smaller size, it is an invisible one. Every
        // keyword lands here (`medium`, `large`, `smaller`, `inherit`: none is a length AngleSharp
        // resolves), so `font-size: large` deleted the text of the element it was written on.
        if (Keyword(value.Trim().ToLowerInvariant(), parent.FontSize) is {} keyword)
        {
            return keyword;
        }

        var length = CssValues.ParseLength(value, parent.FontSize, root, CssLength.None);
        return length.Kind switch
        {
            LengthKind.Absolute => length.Value,
            LengthKind.Percent => parent.FontSize * length.Value / 100f,
            _ => parent.FontSize
        };
    }

    /// <summary>
    /// The size a <c>font-size</c> keyword names, or null when the value is not one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The absolute keywords come from a table anchored on <c>medium</c>, and the values are
    /// measured out of Chrome rather than derived: 9, 10, 13, 16, 18, 24 and 32 pixels. They are
    /// not a geometric series and no single ratio reproduces them — the steps between the small
    /// end are one pixel and the steps at the large end are eight.
    /// </para>
    /// <para>
    /// Anchored on a constant rather than on the root element's size, which is what CSS asks for
    /// and is measurable in a browser: the table follows the reader's preferred size, a setting
    /// this engine has no equivalent of, so <c>font-size: large</c> is 18px whatever the document
    /// declares on <c>html</c>. Anchoring on the root would make a document that sets
    /// <c>html { font-size: 20px }</c> report <c>large</c> as 22.5px where a browser still says 18.
    /// </para>
    /// <para>
    /// The two relative keywords are the parent's size scaled by 1.2, which is measured as well
    /// and holds at every size: 16px gives 13.333 and 19.2, and inside an 18px parent
    /// <c>smaller</c> gives 15.
    /// </para>
    /// </remarks>
    static float? Keyword(string value, float parentSize) =>
        value switch
        {
            "xx-small" => defaultFontSize * 9 / 16f,
            "x-small" => defaultFontSize * 10 / 16f,
            "small" => defaultFontSize * 13 / 16f,
            // `initial` is the property's initial value, which is `medium`. It arrives more
            // often than anyone writes it, since a shorthand that omits a component sets that
            // component to it.
            "medium" or "initial" => defaultFontSize,
            "large" => defaultFontSize * 18 / 16f,
            "x-large" => defaultFontSize * 24 / 16f,
            "xx-large" => defaultFontSize * 32 / 16f,
            "smaller" => parentSize / 1.2f,
            "larger" => parentSize * 1.2f,
            _ => null
        };

    /// <summary>
    /// The size <c>medium</c> names, and the anchor for the whole absolute table.
    /// </summary>
    /// <remarks>
    /// A browser takes this from the reader's preferred font size and defaults it to 16px. Nothing
    /// here has a reader, so the default is the value.
    /// </remarks>
    const float defaultFontSize = 16f;

    static CssLength Length(
        ICssStyleDeclaration declaration,
        string property,
        float fontSize,
        CssRoot root,
        CssLength? fallback = null) =>
        CssValues.ParseLength(
            declaration.GetPropertyValue(property),
            fontSize,
            root,
            fallback ?? CssLength.Zero);

    /// <summary>
    /// A border edge's width, which is zero whenever its style is <c>none</c> or <c>hidden</c>
    /// however wide it was declared.
    /// </summary>
    /// <remarks>
    /// Folding the style into the width here means layout never has to consult border-style at
    /// all — an edge that does not paint also does not take space, which is exactly the CSS rule.
    /// </remarks>
    static float BorderWidth(ICssStyleDeclaration declaration, string side, float fontSize, CssRoot root)
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
        var keyword = value.Trim().ToLowerInvariant();
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

        return CssValues.ParseLength(value, fontSize, root, CssLength.Zero).Resolve(0);
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

    static DisplayKind ParseDisplay(string value, string localName) =>
        value.Trim().ToLowerInvariant() switch
        {
            "none" => DisplayKind.None,
            "inline" => DisplayKind.Inline,
            "inline-block" => DisplayKind.InlineBlock,
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
            // Everything not yet implemented — flex, grid, and the rest — lays out as a block. A
            // wrong box beats no box: it keeps the content on the page and shows up in the corpus
            // comparison as a geometry difference rather than as silently missing content that
            // nothing measures.
            _ => DisplayKind.Block
        };

    /// <summary>
    /// How a box is positioned. Not inherited.
    /// </summary>
    /// <remarks>
    /// <c>sticky</c> resolves to <c>relative</c>: it behaves as one until a scroll position it can
    /// never reach on paper, so that is not an approximation but what it computes to in print.
    /// </remarks>
    static PositionKind ParsePosition(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "relative" or "sticky" => PositionKind.Relative,
            "absolute" => PositionKind.Absolute,
            "fixed" => PositionKind.Fixed,
            _ => PositionKind.Static
        };

    /// <summary>
    /// What a box asks of the page break at one of its edges, or inside it. Not inherited.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both spellings are read, modern first. The cascade does not alias them — a
    /// <c>page-break-after</c> declaration comes back under that name and nothing comes back under
    /// <c>break-after</c> — so reading only one silently ignores half the documents that ask. The
    /// legacy spelling is the one that matters most in practice: it is what the reporting tools
    /// and mail merges this converter sees most of emit, and the only one older authoring tools
    /// know.
    /// </para>
    /// <para>
    /// Not inherited, which is worth stating because it would be a plausible mistake in the other
    /// direction: a <c>page-break-before</c> that inherited would start a page at the top of every
    /// descendant of the box that declared it.
    /// </para>
    /// </remarks>
    static BreakKind ParseBreak(ICssStyleDeclaration declaration, string property)
    {
        var modern = declaration.GetPropertyValue(property);
        var value = string.IsNullOrWhiteSpace(modern)
            ? declaration.GetPropertyValue($"page-{property}")
            : modern;

        return value.Trim().ToLowerInvariant() switch
        {
            // The four that name a side ask for a break AND for the page it lands on to be a
            // particular sheet. The break is the half this engine can honour.
            "always" or "page" or "left" or "right" or "recto" or "verso" => BreakKind.Always,
            "avoid" or "avoid-page" => BreakKind.Avoid,
            _ => BreakKind.Auto
        };
    }

    /// <summary>
    /// One corner's two radii. Not inherited.
    /// </summary>
    /// <remarks>
    /// Read from the four longhands rather than from the <c>border-radius</c> shorthand, because
    /// the cascade expands the shorthand into them — and the shorthand's own syntax, up to eight
    /// values split by a slash, is a parse this does not have to write as a result. A corner given
    /// one value is circular, so the vertical radius repeats the horizontal one.
    /// </remarks>
    static (CssLength X, CssLength Y) Radius(
        ICssStyleDeclaration declaration,
        string corner,
        float fontSize,
        CssRoot root)
    {
        var value = declaration.GetPropertyValue($"border-{corner}-radius");

        if (string.IsNullOrWhiteSpace(value))
        {
            return (CssLength.Zero, CssLength.Zero);
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return (CssLength.Zero, CssLength.Zero);
        }

        var horizontal = CssValues.ParseLength(parts[0], fontSize, root, CssLength.Zero);
        var vertical = parts.Length > 1
            ? CssValues.ParseLength(parts[1], fontSize, root, CssLength.Zero)
            : horizontal;

        return (horizontal, vertical);
    }

    /// <summary>
    /// The raster image a <c>background-image: url(...)</c> names, or null.
    /// </summary>
    /// <remarks>
    /// Routed through the document's <see cref="ImageStore"/> rather than loaded here, which is
    /// what puts a stylesheet URL under the same policy an <c>&lt;img src&gt;</c> is under and what
    /// makes two elements naming one file share a decode. A source that is refused or does not
    /// resolve comes back null and is reported by <see cref="UnsupportedCss"/> like any other
    /// background this engine cannot paint.
    /// </remarks>
    static ImageData? Picture(ICssStyleDeclaration declaration, DocumentContext context, string property)
    {
        var value = declaration.GetPropertyValue(property).AsSpan().Trim();

        if (!value.StartsWith("url(", StringComparison.OrdinalIgnoreCase) || !value.EndsWith(")"))
        {
            return null;
        }

        var source = value[4..^1].Trim();

        if (source.Length >= 2 &&
            ((source[0] == '"' && source[^1] == '"') || (source[0] == '\'' && source[^1] == '\'')))
        {
            source = source[1..^1];
        }

        return source.IsEmpty ? null : context.Images.Resolve(source.ToString(), out _);
    }

    /// <summary>Which of a box's three rectangles a background property names.</summary>
    static BoxArea Area(ICssStyleDeclaration declaration, string property, BoxArea fallback) =>
        declaration.GetPropertyValue(property).Trim().ToLowerInvariant() switch
        {
            "border-box" => BoxArea.Border,
            "padding-box" => BoxArea.Padding,
            "content-box" => BoxArea.Content,
            _ => fallback
        };

    /// <summary>Whether the background repeats along one axis.</summary>
    /// <remarks>
    /// The initial value is <c>repeat</c> on both, which is why a background image reaches past the
    /// element that declared it far more often than authors expect.
    /// </remarks>
    static bool Repeats(ICssStyleDeclaration declaration, bool horizontal) =>
        declaration.GetPropertyValue("background-repeat").Trim().ToLowerInvariant() switch
        {
            "no-repeat" => false,
            "repeat-x" => horizontal,
            "repeat-y" => !horizontal,
            _ => true
        };

    /// <summary>
    /// One component of <c>background-position</c>, with the keywords folded onto the percentages
    /// they stand for.
    /// </summary>
    /// <remarks>
    /// <c>center</c> is <c>50%</c> and the edge keywords are <c>0%</c> and <c>100%</c>, which is
    /// exact rather than approximate: a percentage aligns that fraction of the image with the same
    /// fraction of the box, so <c>100%</c> puts the image's right edge on the box's right edge and
    /// is what <c>right</c> means.
    /// </remarks>
    static CssLength Position(
        ICssStyleDeclaration declaration,
        string property,
        float fontSize,
        CssRoot root,
        bool horizontal)
    {
        var value = declaration.GetPropertyValue(property).Trim().ToLowerInvariant();

        if (value.Length == 0)
        {
            // The two properties have different initial values — a background starts at the near
            // edge and a replaced element's content is centred — so the fallback follows the one
            // being read rather than a shared default.
            return property == "object-position" ? CssLength.Percentage(50) : CssLength.Zero;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // A single value sets the named axis and centres the other.
        var part = parts.Length == 1
            ? horizontal ? parts[0] : "center"
            : parts[horizontal ? 0 : 1];

        return part switch
        {
            "left" or "top" => CssLength.Zero,
            "center" => CssLength.Percentage(50),
            "right" or "bottom" => CssLength.Percentage(100),
            _ => CssValues.ParseLength(part, fontSize, root, CssLength.Zero)
        };
    }

    /// <summary>Whether an empty table cell paints nothing. Inherited.</summary>
    static bool EmptyCells(ICssStyleDeclaration declaration, bool inherited) =>
        declaration.GetPropertyValue("empty-cells").Trim().ToLowerInvariant() switch
        {
            "hide" => true,
            "show" => false,
            _ => inherited
        };

    /// <summary>How the background image is scaled before tiling.</summary>
    static BackgroundSizing Sizing(ICssStyleDeclaration declaration) =>
        declaration.GetPropertyValue("background-size").Trim().ToLowerInvariant() switch
        {
            "" or "auto" or "auto auto" => BackgroundSizing.Auto,
            "cover" => BackgroundSizing.Cover,
            "contain" => BackgroundSizing.Contain,
            _ => BackgroundSizing.Explicit
        };

    /// <summary>One component of an explicit <c>background-size</c>.</summary>
    static CssLength SizeComponent(
        ICssStyleDeclaration declaration,
        float fontSize,
        CssRoot root,
        bool first)
    {
        var parts = declaration
            .GetPropertyValue("background-size")
            .Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // One length sizes that axis and leaves the other to the image's own proportions.
        if (parts.Length == 0 || (!first && parts.Length == 1))
        {
            return CssLength.Auto;
        }

        return CssValues.ParseLength(parts[first ? 0 : 1], fontSize, root, CssLength.Auto);
    }

    /// <summary>
    /// The outline's width, zeroed when its style says it is not drawn.
    /// </summary>
    /// <remarks>
    /// Folded the same way <see cref="BorderWidth"/> folds a border's, so nothing downstream has to
    /// consult <c>outline-style</c> — an outline that is not drawn is one of zero width. Only
    /// <c>solid</c> is honoured; the rest are reported, so they zero the width too rather than
    /// painting solid and claiming to be right.
    /// </remarks>
    static float OutlineWidth(ICssStyleDeclaration declaration, float fontSize, CssRoot root)
    {
        var style = declaration.GetPropertyValue("outline-style").Trim().ToLowerInvariant();

        if (style is not "solid")
        {
            return 0;
        }

        var value = declaration.GetPropertyValue("outline-width").Trim().ToLowerInvariant();

        return value switch
        {
            "" or "medium" => 3,
            "thin" => 1,
            "thick" => 5,
            _ => CssValues.ParseLength(value, fontSize, root, CssLength.Zero).Resolve(0)
        };
    }

    /// <summary>
    /// Where a list marker sits, inheriting when the cascade said nothing.
    /// </summary>
    static ListStylePositionKind ParseListPosition(string value, ListStylePositionKind inherited) =>
        value.Trim().ToLowerInvariant() switch
        {
            "inside" => ListStylePositionKind.Inside,
            "outside" => ListStylePositionKind.Outside,
            _ => inherited
        };

    /// <summary>
    /// How a replaced element's content fills its box. Not inherited.
    /// </summary>
    static ObjectFitKind ParseObjectFit(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "contain" => ObjectFitKind.Contain,
            "cover" => ObjectFitKind.Cover,
            "none" => ObjectFitKind.None,
            "scale-down" => ObjectFitKind.ScaleDown,
            _ => ObjectFitKind.Fill
        };

    /// <summary>
    /// How one border edge is drawn. Not inherited.
    /// </summary>
    /// <remarks>
    /// <c>none</c> and <c>hidden</c> do not appear because <see cref="BorderWidth"/> has already
    /// turned them into a zero width, so an edge carrying either is never painted whatever this
    /// returns. The four shaded styles fall through to solid, which is what
    /// <see cref="UnsupportedCss"/> keeps reporting.
    /// </remarks>
    static BorderStyleKind BorderStyle(ICssStyleDeclaration declaration, string side) =>
        declaration.GetPropertyValue($"border-{side}-style").Trim().ToLowerInvariant() switch
        {
            "dashed" => BorderStyleKind.Dashed,
            "dotted" => BorderStyleKind.Dotted,
            "double" => BorderStyleKind.Double,
            _ => BorderStyleKind.Solid
        };

    /// <summary>
    /// An extra advance in CSS pixels, inheriting when the cascade said nothing.
    /// </summary>
    /// <remarks>
    /// <c>normal</c> is zero rather than unparseable, which is what it means for both properties
    /// that use this: no extra advance beyond what the font asks for. Returning null for it would
    /// inherit the parent's instead, so a child resetting to <c>normal</c> would keep the spacing
    /// it was trying to clear.
    /// </remarks>
    static float? Advance(
        ICssStyleDeclaration declaration,
        string property,
        float fontSize,
        CssRoot root)
    {
        var value = declaration.GetPropertyValue(property);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Trim().Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return CssValues.ParseLength(value, fontSize, root, CssLength.Zero).Resolve(0);
    }

    /// <summary>
    /// How opaque a box is, clamped to 0..1. Not inherited.
    /// </summary>
    /// <remarks>
    /// A percentage is accepted as well as a number, which CSS added later and which authoring
    /// tools emit. Out-of-range values clamp rather than being rejected, as the specification
    /// requires — <c>opacity: 2</c> is fully opaque and <c>opacity: -1</c> is invisible.
    /// </remarks>
    static float ParseOpacity(string value)
    {
        var text = value.Trim();

        if (text.Length == 0)
        {
            return 1f;
        }

        var percent = text.EndsWith('%');
        var number = percent ? text[..^1] : text;

        if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return 1f;
        }

        return Math.Clamp(percent ? parsed / 100f : parsed, 0f, 1f);
    }

    /// <summary>
    /// Whether a box is painted, inheriting when the cascade said nothing.
    /// </summary>
    /// <remarks>
    /// <c>collapse</c> folds into <c>hidden</c>, which is what it means everywhere except on a
    /// table row or column — where it removes the track rather than merely blanking it. Neither is
    /// implemented, and hiding is the closer of the two available answers.
    /// </remarks>
    static VisibilityKind ParseVisibility(string value, VisibilityKind inherited) =>
        value.Trim().ToLowerInvariant() switch
        {
            "hidden" or "collapse" => VisibilityKind.Hidden,
            "visible" => VisibilityKind.Visible,
            _ => inherited
        };

    /// <summary>
    /// How text is cased, inheriting when the cascade said nothing.
    /// </summary>
    static TextTransformKind ParseTextTransform(string value, TextTransformKind inherited) =>
        value.Trim().ToLowerInvariant() switch
        {
            "uppercase" => TextTransformKind.Uppercase,
            "lowercase" => TextTransformKind.Lowercase,
            "capitalize" => TextTransformKind.Capitalize,
            "none" => TextTransformKind.None,
            _ => inherited
        };

    /// <summary>
    /// Whether a box clips its overflow. Not inherited.
    /// </summary>
    /// <remarks>
    /// The shorthand and both axes are read, and any of them asking to clip clips both. CSS
    /// forbids one axis clipping while the other stays visible — a <c>visible</c> paired with a
    /// clipping value computes to <c>auto</c>, which clips — so a single flag is the right shape
    /// rather than an approximation. <c>scroll</c> and <c>auto</c> clip too: paper does not
    /// scroll, so what falls outside the box is simply not there.
    /// </remarks>
    static OverflowKind ParseOverflow(ICssStyleDeclaration declaration)
    {
        foreach (var property in overflowProperties)
        {
            if (Clips(declaration.GetPropertyValue(property)))
            {
                return OverflowKind.Hidden;
            }
        }

        return OverflowKind.Visible;

        static bool Clips(string value) =>
            value.Trim().ToLowerInvariant() is "hidden" or "clip" or "scroll" or "auto";
    }

    static readonly string[] overflowProperties = ["overflow", "overflow-x", "overflow-y"];

    /// <summary>
    /// Which side a box floats to. Not inherited.
    /// </summary>
    static FloatKind ParseFloat(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "left" => FloatKind.Left,
            "right" => FloatKind.Right,
            // `inline-start` and `inline-end` resolve against the writing direction, which is
            // always left-to-right here, so they are their physical equivalents.
            "inline-start" => FloatKind.Left,
            "inline-end" => FloatKind.Right,
            _ => FloatKind.None
        };

    /// <summary>
    /// Which floats a box must clear. Not inherited.
    /// </summary>
    static ClearKind ParseClear(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "left" or "inline-start" => ClearKind.Left,
            "right" or "inline-end" => ClearKind.Right,
            "both" => ClearKind.Both,
            _ => ClearKind.None
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
    static ListStyleKind ParseListStyle(string value, ListStyleKind inherited) =>
        value.Trim().ToLowerInvariant() switch
        {
            "" => inherited,
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
        CssRoot root,
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
        return CssValues.ParseLength(part, fontSize, root, CssLength.Zero).Resolve(0);
    }

    /// <summary>
    /// Which column algorithm a table uses. Anything but <c>fixed</c> is the automatic one.
    /// </summary>
    /// <summary>
    /// What a declared <c>width</c> or <c>height</c> measures. Not inherited.
    /// </summary>
    /// <remarks>
    /// The default is <c>content-box</c>, which is CSS's initial value — but a table arrives here
    /// with <c>border-box</c> already declared, because the user-agent stylesheet says so. That is
    /// where a table's declared width including its border comes from; it is not a rule of table
    /// layout, and an author who writes <c>content-box</c> on a table gets the other behaviour.
    /// </remarks>
    static BoxSizingKind ParseBoxSizing(string value) =>
        value.AsSpan().Trim().Equals("border-box", StringComparison.OrdinalIgnoreCase)
            ? BoxSizingKind.BorderBox
            : BoxSizingKind.ContentBox;

    static TableLayoutKind ParseTableLayout(string value) =>
        value.AsSpan().Trim().Equals("fixed", StringComparison.OrdinalIgnoreCase)
            ? TableLayoutKind.Fixed
            : TableLayoutKind.Auto;

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
    static VerticalAlignKind ParseVerticalAlign(
        string value,
        VerticalAlignKind inherited,
        float fontSize,
        CssRoot root)
    {
        var text = value.Trim().ToLowerInvariant();

        switch (text)
        {
            case null or "" or "inherit":
                return inherited;
            case "top":
                return VerticalAlignKind.Top;
            case "middle":
                return VerticalAlignKind.Middle;
            case "bottom":
                return VerticalAlignKind.Bottom;
            case "super":
                return VerticalAlignKind.Super;
            case "sub":
                return VerticalAlignKind.Sub;
            case "text-top":
                return VerticalAlignKind.TextTop;
            case "text-bottom":
                return VerticalAlignKind.TextBottom;
            case "baseline":
                return VerticalAlignKind.Baseline;
        }

        // Anything left is a length or a percentage, which raises the box by that much. The
        // fallback is None rather than Zero so that an unparseable value is distinguishable from a
        // deliberate `vertical-align: 0`, which really is the baseline — the same trap
        // ResolveFontSize records, where an absolute-kind fallback silently took the branch meant
        // for real lengths.
        var offset = CssValues.ParseLength(text, fontSize, root, CssLength.None);

        return offset.IsNone
            ? VerticalAlignKind.Baseline
            : VerticalAlignKind.Length;
    }

    static int ParseWeight(string value, int inherited)
    {
        var text = value.Trim().ToLowerInvariant();

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

    static bool ParseItalic(string value, bool inherited)
    {
        var text = value.Trim().ToLowerInvariant();

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
    /// <summary>
    /// The line height, inheriting when the cascade said nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inherited explicitly, like every other inherited property here, because the cascaded style
    /// carries no inherited values. Missing it left <c>line-height</c> applying to the one element
    /// that declared it and to nothing inside — so <c>body { line-height: 1.6 }</c>, which is how
    /// nearly every stylesheet sets line spacing, did nothing at all.
    /// </para>
    /// <para>
    /// A unitless value is carried down AS THE NUMBER rather than as the pixels it resolved to
    /// here, since CSS re-resolves it against each descendant's own font size. Inheriting the
    /// pixels would give 32px text the spacing computed for its 16px ancestor.
    /// </para>
    /// <para>
    /// An explicit <c>normal</c> is not the same as saying nothing: it stops the inheritance and
    /// returns to the font's own metrics, so the two cases are separated here rather than both
    /// falling through.
    /// </para>
    /// </remarks>
    static (float? Absolute, float? Scale) ParseLineHeight(
        string value,
        float fontSize,
        CssRoot root,
        ComputedStyle parent)
    {
        var text = value.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return (parent.LineHeight, parent.LineHeightScale);
        }

        if (text.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        if (CssValues.TryParseNumber(text, out var multiplier))
        {
            return (null, multiplier);
        }

        var length = CssValues.ParseLength(text, fontSize, root, CssLength.Zero);
        return (
            length.Kind == LengthKind.Percent
                ? fontSize * length.Value / 100f
                : length.Resolve(fontSize),
            null);
    }

    /// <summary>
    /// Which rules apply, from <c>text-decoration-line</c> or the <c>text-decoration</c>
    /// shorthand.
    /// </summary>
    /// <remarks>
    /// Both are read because which one the cascade reports depends on which the author wrote, and
    /// the shorthand also carries colour and style keywords that have to be looked past. The value
    /// is scanned for each line name rather than split, for the same reason: the shorthand
    /// interleaves them with words this does not care about.
    /// </remarks>
    /// <summary>
    /// The colour a decoration is drawn in: declared, else this element's own where it declared the
    /// decoration, else whatever was inherited.
    /// </summary>
    /// <remarks>
    /// The middle case is what stops a decoration declared here from picking up an ancestor's
    /// colour. An element that starts its own underline starts its own colour with it, and only an
    /// element merely INHERITING an ancestor's rule keeps the ancestor's colour.
    /// </remarks>
    static Color? DecorationColour(ICssStyleDeclaration declaration, ComputedStyle parent, Color color)
    {
        if (CssValues.ParseColor(declaration.GetPropertyValue("text-decoration-color")) is {} declared)
        {
            return declared;
        }

        return Declares(declaration) ? color : parent.DecorationColor;
    }

    /// <summary>
    /// How the rule is drawn, inheriting for the same reason the colour does.
    /// </summary>
    /// <remarks>
    /// <c>wavy</c> maps to <see cref="BorderStyleKind.Solid"/> and is reported: a wave needs a path
    /// this engine has no shape for, and a solid rule is the closer of the two answers available.
    /// </remarks>
    static BorderStyleKind DecorationRule(ICssStyleDeclaration declaration, ComputedStyle parent)
    {
        var value = declaration.GetPropertyValue("text-decoration-style").Trim().ToLowerInvariant();

        return value switch
        {
            "dashed" => BorderStyleKind.Dashed,
            "dotted" => BorderStyleKind.Dotted,
            "double" => BorderStyleKind.Double,
            "solid" or "wavy" => BorderStyleKind.Solid,
            _ => Declares(declaration) ? BorderStyleKind.Solid : parent.DecorationStyle
        };
    }

    /// <summary>Whether this element started a decoration of its own.</summary>
    static bool Declares(ICssStyleDeclaration declaration) =>
        !string.IsNullOrWhiteSpace(declaration.GetPropertyValue("text-decoration-line")) ||
        !string.IsNullOrWhiteSpace(declaration.GetPropertyValue("text-decoration"));

    /// <summary>
    /// The tab stop spacing, in space advances. Inherited, as the property is.
    /// </summary>
    /// <remarks>
    /// Negative and zero values are rejected rather than clamped: a zero would make every tab
    /// advance nothing and put the text on top of what precedes it, which is worse than ignoring
    /// the declaration. A length-valued <c>tab-size</c> falls through to the inherited number and
    /// is reported.
    /// </remarks>
    static float ParseTabSize(string value, float inherited) =>
        CssValues.TryParseNumber(value.Trim(), out var stops) && stops > 0
            ? stops
            : inherited;

    /// <summary>
    /// Whether a line may break inside a word, from either of the two properties that say so.
    /// </summary>
    /// <remarks>
    /// <c>word-break</c> is read first because <c>break-all</c> is the stronger permission: it
    /// breaks whether or not the word would overflow, where <c>overflow-wrap: break-word</c> breaks
    /// only a word that fits on no line at all. The values not listed — <c>keep-all</c> and
    /// <c>break-word</c> as a <c>word-break</c> value — are reported rather than approximated.
    /// </remarks>
    static WordBreaking ParseWordBreaking(ICssStyleDeclaration declaration, WordBreaking inherited)
    {
        if (declaration.GetPropertyValue("word-break").Trim().ToLowerInvariant() is "break-all")
        {
            return WordBreaking.Always;
        }

        return declaration.GetPropertyValue("overflow-wrap").Trim().ToLowerInvariant() switch
        {
            "anywhere" => WordBreaking.Always,
            "break-word" => WordBreaking.OnOverflow,
            "normal" => WordBreaking.Normal,
            _ => inherited
        };
    }

    static TextDecorations ParseDecorations(ICssStyleDeclaration declaration, TextDecorations inherited)
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
        var lines = TextDecorations.None;

        if (text.Contains("underline", StringComparison.Ordinal))
        {
            lines |= TextDecorations.Underline;
        }

        if (text.Contains("overline", StringComparison.Ordinal))
        {
            lines |= TextDecorations.Overline;
        }

        if (text.Contains("line-through", StringComparison.Ordinal))
        {
            lines |= TextDecorations.LineThrough;
        }

        if (lines != TextDecorations.None)
        {
            return lines;
        }

        // An explicit `none` clears inherited rules; anything else — a colour or a style on its
        // own — leaves them alone.
        return text.Contains("none", StringComparison.Ordinal) ? TextDecorations.None : inherited;
    }

    static TextAlignKind ParseTextAlign(string value, TextAlignKind inherited) =>
        value.Trim().ToLowerInvariant() switch
        {
            "center" => TextAlignKind.Center,
            "right" or "end" => TextAlignKind.Right,
            "justify" => TextAlignKind.Justify,
            "left" or "start" => TextAlignKind.Left,
            _ => inherited
        };

    static WhiteSpaceKind ParseWhiteSpace(string value, WhiteSpaceKind inherited) =>
        value.Trim().ToLowerInvariant() switch
        {
            "pre" => WhiteSpaceKind.Pre,
            "pre-wrap" => WhiteSpaceKind.PreWrap,
            "pre-line" => WhiteSpaceKind.PreLine,
            "nowrap" => WhiteSpaceKind.NoWrap,
            "normal" => WhiteSpaceKind.Normal,
            _ => inherited
        };
}
