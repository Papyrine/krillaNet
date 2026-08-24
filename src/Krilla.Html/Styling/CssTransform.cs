/// <summary>The transform functions this engine applies.</summary>
enum TransformKind
{
    /// <summary>Moves the box.</summary>
    Translate,

    /// <summary>Scales it about the origin.</summary>
    Scale,

    /// <summary>Rotates it about the origin.</summary>
    Rotate,

    /// <summary>Slants it about the origin.</summary>
    Skew,

    /// <summary>An affine matrix given outright.</summary>
    Matrix
}

/// <summary>
/// One transform function, with whatever its kind needs.
/// </summary>
/// <param name="Kind">Which function.</param>
/// <param name="X">Translate's x, or a matrix's <c>e</c>.</param>
/// <param name="Y">Translate's y, or a matrix's <c>f</c>.</param>
/// <param name="A">Scale's x, rotate's angle, skew's x angle, or a matrix's <c>a</c>.</param>
/// <param name="B">Scale's y, skew's y angle, or a matrix's <c>b</c>.</param>
/// <param name="C">A matrix's <c>c</c>.</param>
/// <param name="D">A matrix's <c>d</c>.</param>
/// <remarks>
/// One struct rather than a hierarchy, because the only function needing anything unusual is
/// <c>translate</c>: its arguments are lengths and may be percentages of the box's own size, which
/// is why they are <see cref="CssLength"/> where everything else is a number.
/// </remarks>
readonly record struct TransformFunction(
    TransformKind Kind,
    CssLength X,
    CssLength Y,
    float A,
    float B = 0,
    float C = 0,
    float D = 0);

/// <summary>
/// A parsed <c>transform</c>, with the origin it turns about.
/// </summary>
/// <remarks>
/// <para>
/// A transform changes PAINTING and not layout. The box keeps the space it was given, its siblings
/// sit where they would have, and nothing measured against it moves — which is the same bargain
/// <c>position: relative</c> strikes and the reason both are cheap.
/// </para>
/// <para>
/// It does create a stacking context, so a transformed box leaves its parent's paint phases and
/// goes down with the positioned content. That part is not free and is shared with
/// <c>opacity</c>.
/// </para>
/// <para>
/// Resolved against a box rather than at parse time: a percentage translation and the origin are
/// both fractions of the box's own border box, which is a layout result.
/// </para>
/// </remarks>
sealed record CssTransform(
    IReadOnlyList<TransformFunction> Functions,
    CssLength OriginX,
    CssLength OriginY)
{
    /// <summary>
    /// Parses a <c>transform</c> value, or returns null when it is <c>none</c> or holds a function
    /// this engine does not apply.
    /// </summary>
    /// <remarks>
    /// The three-dimensional functions and <c>perspective</c> are deliberately absent. Applying
    /// their two-dimensional shadow would be wrong in a way nothing would report, so they are left
    /// unparsed and <see cref="UnsupportedCss"/> says the transform was not applied.
    /// </remarks>
    public static CssTransform? Parse(
        string transform,
        string origin,
        float fontSize,
        float rootFontSize)
    {
        var text = transform.Trim();

        if (text.Length == 0 || text.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var functions = new List<TransformFunction>();

        foreach (var (name, arguments) in Calls(text))
        {
            if (Function(name, arguments, fontSize, rootFontSize) is not {} function)
            {
                return null;
            }

            functions.Add(function);
        }

        if (functions.Count == 0)
        {
            return null;
        }

        var (x, y) = Origin(origin, fontSize, rootFontSize);
        return new(functions, x, y);
    }

    /// <summary>
    /// The transform as an affine matrix for a box of <paramref name="border"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Composed left to right, which for column vectors means the product in written order and so
    /// the RIGHTMOST function reaching a point first: <c>translate(30px) rotate(15deg)</c> rotates
    /// the box about the origin and then moves it, rather than moving it and rotating about where
    /// it started.
    /// </para>
    /// <para>
    /// The whole thing is conjugated by the origin — moved there, applied, moved back — because
    /// every function turns about the coordinate system's zero and CSS turns them about the box's
    /// own <c>transform-origin</c>, which defaults to its centre.
    /// </para>
    /// </remarks>
    public Matrix Resolve(Rect border)
    {
        var matrix = Matrix.Identity;

        foreach (var function in Functions)
        {
            matrix = Multiply(matrix, Single(function, border));
        }

        var originX = border.X + OriginX.Resolve(border.Width);
        var originY = border.Y + OriginY.Resolve(border.Height);

        return Multiply(
            Multiply(Matrix.Translate(originX, originY), matrix),
            Matrix.Translate(-originX, -originY));
    }

    /// <summary>
    /// The axis-aligned bounds of <paramref name="border"/> once this transform is applied.
    /// </summary>
    /// <remarks>
    /// What a browser's <c>getBoundingClientRect()</c> reports, which is why it exists: the corpus
    /// compares against that rect, so a transformed box has to be reported transformed or every
    /// scenario using one shows a difference that is not a defect. It also makes the geometry
    /// comparison a real check of the arithmetic above rather than an exemption from it.
    /// </remarks>
    public static Rect Bounds(Matrix matrix, Rect border)
    {
        var (left, top, right, bottom) = (float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

        foreach (var (x, y) in new[]
                 {
                     (border.X, border.Y),
                     (border.Right, border.Y),
                     (border.Right, border.Bottom),
                     (border.X, border.Bottom)
                 })
        {
            var px = matrix.ScaleX * x + matrix.SkewX * y + matrix.TranslateX;
            var py = matrix.SkewY * x + matrix.ScaleY * y + matrix.TranslateY;

            left = MathF.Min(left, px);
            top = MathF.Min(top, py);
            right = MathF.Max(right, px);
            bottom = MathF.Max(bottom, py);
        }

        return new(left, top, right - left, bottom - top);
    }

    /// <summary>One function's own matrix, before the origin is applied.</summary>
    static Matrix Single(TransformFunction function, Rect border)
    {
        switch (function.Kind)
        {
            case TransformKind.Translate:
                return Matrix.Translate(
                    function.X.Resolve(border.Width),
                    function.Y.Resolve(border.Height));

            case TransformKind.Scale:
                return Matrix.Scale(function.A, function.B);

            case TransformKind.Rotate:
                return Matrix.Rotate(function.A);

            case TransformKind.Skew:
                var ax = MathF.Tan(function.A * MathF.PI / 180f);
                var ay = MathF.Tan(function.B * MathF.PI / 180f);
                return new(1, ay, ax, 1, 0, 0);

            default:
                return new(
                    function.A,
                    function.B,
                    function.C,
                    function.D,
                    function.X.Resolve(0),
                    function.Y.Resolve(0));
        }
    }

    /// <summary>
    /// The product of an ancestor's transform with a descendant's, the ancestor applied last.
    /// </summary>
    /// <remarks>
    /// A transform applies to a box AND its descendants, so a transformed box inside another one
    /// carries both. The painter gets this for free by nesting its pushes; anything walking the
    /// tree itself has to compose them, which is what this is for.
    /// </remarks>
    public static Matrix Combine(Matrix ancestor, Matrix own) =>
        Multiply(ancestor, own);

    /// <summary>The product of two affine matrices, <paramref name="left"/> applied last.</summary>
    static Matrix Multiply(Matrix left, Matrix right) =>
        new(
            left.ScaleX * right.ScaleX + left.SkewX * right.SkewY,
            left.SkewY * right.ScaleX + left.ScaleY * right.SkewY,
            left.ScaleX * right.SkewX + left.SkewX * right.ScaleY,
            left.SkewY * right.SkewX + left.ScaleY * right.ScaleY,
            left.ScaleX * right.TranslateX + left.SkewX * right.TranslateY + left.TranslateX,
            left.SkewY * right.TranslateX + left.ScaleY * right.TranslateY + left.TranslateY);

    /// <summary>One function, or null when it is not one this engine applies.</summary>
    static TransformFunction? Function(
        string name,
        IReadOnlyList<string> arguments,
        float fontSize,
        float rootFontSize)
    {
        CssLength? Length(int index) =>
            index < arguments.Count
                ? CssValues.ParseLength(arguments[index], fontSize, rootFontSize, CssLength.None) is
                  {Kind: not LengthKind.None} parsed
                    ? parsed
                    : null
                : CssLength.Zero;

        float? Number(int index, float fallback)
        {
            if (index >= arguments.Count)
            {
                return fallback;
            }

            return float.TryParse(
                arguments[index],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value)
                ? value
                : null;
        }

        float? Angle(int index, float fallback) =>
            index < arguments.Count ? CssValues.ParseAngle(arguments[index]) : fallback;

        switch (name)
        {
            case "translate" when arguments.Count is 1 or 2:
                return Length(0) is {} tx && Length(1) is {} ty
                    ? new(TransformKind.Translate, tx, ty, 0)
                    : null;

            case "translatex" when arguments.Count == 1:
                return Length(0) is {} onlyX
                    ? new(TransformKind.Translate, onlyX, CssLength.Zero, 0)
                    : null;

            case "translatey" when arguments.Count == 1:
                return Length(0) is {} onlyY
                    ? new(TransformKind.Translate, CssLength.Zero, onlyY, 0)
                    : null;

            case "scale" when arguments.Count is 1 or 2:
                return Number(0, 1) is {} sx && Number(1, sx) is {} sy
                    ? new(TransformKind.Scale, CssLength.Zero, CssLength.Zero, sx, sy)
                    : null;

            case "scalex" when arguments.Count == 1:
                return Number(0, 1) is {} scaleX
                    ? new(TransformKind.Scale, CssLength.Zero, CssLength.Zero, scaleX, 1)
                    : null;

            case "scaley" when arguments.Count == 1:
                return Number(0, 1) is {} scaleY
                    ? new(TransformKind.Scale, CssLength.Zero, CssLength.Zero, 1, scaleY)
                    : null;

            case "rotate" when arguments.Count == 1:
                return Angle(0, 0) is {} degrees
                    ? new(TransformKind.Rotate, CssLength.Zero, CssLength.Zero, degrees)
                    : null;

            case "skew" when arguments.Count is 1 or 2:
                return Angle(0, 0) is {} skewX && Angle(1, 0) is {} skewY
                    ? new(TransformKind.Skew, CssLength.Zero, CssLength.Zero, skewX, skewY)
                    : null;

            case "skewx" when arguments.Count == 1:
                return Angle(0, 0) is {} onlySkewX
                    ? new(TransformKind.Skew, CssLength.Zero, CssLength.Zero, onlySkewX, 0)
                    : null;

            case "skewy" when arguments.Count == 1:
                return Angle(0, 0) is {} onlySkewY
                    ? new(TransformKind.Skew, CssLength.Zero, CssLength.Zero, 0, onlySkewY)
                    : null;

            case "matrix" when arguments.Count == 6:
                var numbers = new float[6];

                for (var index = 0; index < 6; index++)
                {
                    if (Number(index, 0) is not {} value)
                    {
                        return null;
                    }

                    numbers[index] = value;
                }

                return new(
                    TransformKind.Matrix,
                    CssLength.Pixels(numbers[4]),
                    CssLength.Pixels(numbers[5]),
                    numbers[0],
                    numbers[1],
                    numbers[2],
                    numbers[3]);

            default:
                return null;
        }
    }

    /// <summary>
    /// The origin the functions turn about, defaulting to the box's centre.
    /// </summary>
    /// <remarks>
    /// The cascade puts the horizontal component first whatever order it was written in, so
    /// <c>top left</c> arrives as <c>left top</c> and the two can be read positionally. A single
    /// value leaves the other centred.
    /// </remarks>
    static (CssLength X, CssLength Y) Origin(string value, float fontSize, float rootFontSize)
    {
        var half = CssLength.Percentage(50);
        var parts = value.Trim().ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return (half, half);
        }

        var x = Component(parts[0], horizontal: true) ?? half;
        var y = parts.Length > 1 ? Component(parts[1], horizontal: false) ?? half : half;

        return (x, y);

        CssLength? Component(string part, bool horizontal) =>
            part switch
            {
                "left" or "top" => CssLength.Zero,
                "center" => CssLength.Percentage(50),
                "right" or "bottom" => CssLength.Percentage(100),
                _ => CssValues.ParseLength(part, fontSize, rootFontSize, CssLength.None) is
                     {Kind: not LengthKind.None} parsed
                    ? parsed
                    : null
            };
    }

    /// <summary>
    /// Splits a transform list into its function calls.
    /// </summary>
    static IEnumerable<(string Name, IReadOnlyList<string> Arguments)> Calls(string text)
    {
        var index = 0;

        while (index < text.Length)
        {
            var open = text.IndexOf('(', index);
            var close = open < 0 ? -1 : text.IndexOf(')', open);

            if (open < 0 || close < 0)
            {
                yield break;
            }

            yield return (
                text[index..open].Trim().ToLowerInvariant(),
                text[(open + 1)..close]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            index = close + 1;
        }
    }
}
