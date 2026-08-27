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
    /// Parses <c>transform</c> together with the three individual transform properties, or returns
    /// null when none of them is set or one of them is a function this engine does not apply.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three individual properties are not shorthands for <c>transform</c> and do not reach it:
    /// CSS Transforms 2 §3 composes them ahead of it, in the fixed order <c>translate</c>,
    /// <c>rotate</c>, <c>scale</c>, whatever order they were written in — so they are a PREFIX on
    /// the function list rather than a second matrix. Everything downstream, the origin included,
    /// then applies to the composite without knowing they exist.
    /// </para>
    /// <para>
    /// The three-dimensional functions and <c>perspective</c> are deliberately absent. Applying
    /// their two-dimensional shadow would be wrong in a way nothing would report, so they are left
    /// unparsed and <see cref="UnsupportedCss"/> says the transform was not applied. The individual
    /// properties have three-dimensional forms of their own — a third length on <c>translate</c>, an
    /// axis on <c>rotate</c>, a third factor on <c>scale</c> — and they are refused the same way.
    /// </para>
    /// </remarks>
    public static CssTransform? Parse(
        string transform,
        string translate,
        string rotate,
        string scale,
        string origin,
        CssFont fontSize,
        CssRoot root)
    {
        var functions = new List<TransformFunction>();

        if (!Individual(translate, rotate, scale, functions, fontSize, root))
        {
            return null;
        }

        var text = transform.Trim();

        if (text.Length > 0 && !text.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            var listed = new List<TransformFunction>();

            foreach (var (name, arguments) in Calls(text))
            {
                if (Function(name, arguments, fontSize, root) is not {} function)
                {
                    return null;
                }

                listed.Add(function);
            }

            if (listed.Count == 0)
            {
                return null;
            }

            functions.AddRange(listed);
        }

        if (functions.Count == 0)
        {
            return null;
        }

        var (x, y) = Origin(origin, fontSize, root);
        return new(functions, x, y);
    }

    /// <summary>
    /// Appends the three individual transform properties, in the order CSS composes them.
    /// </summary>
    /// <remarks>
    /// False when one of them is set to something this engine cannot apply, which drops the whole
    /// composite — the same answer a three-dimensional function in <c>transform</c> gets, and for
    /// the same reason: half a transform puts the box somewhere plausible and wrong.
    /// </remarks>
    static bool Individual(
        string translate,
        string rotate,
        string scale,
        List<TransformFunction> functions,
        CssFont fontSize,
        CssRoot root)
    {
        if (Words(translate) is {} moved)
        {
            // One value leaves the vertical alone, which is not what a one-argument
            // `translate()` function does either — both default the second to zero.
            if (moved.Length > 2)
            {
                return false;
            }

            var x = CssValues.ParseLength(moved[0], fontSize, root, CssLength.None);
            var y = moved.Length > 1
                ? CssValues.ParseLength(moved[1], fontSize, root, CssLength.None)
                : CssLength.Zero;

            if (x.Kind == LengthKind.None || y.Kind == LengthKind.None)
            {
                return false;
            }

            functions.Add(new(TransformKind.Translate, x, y, 0));
        }

        if (Words(rotate) is {} turned)
        {
            // `[ x | y | z | <number>{3} ] && <angle>`, in either order. Only the z axis has a
            // two-dimensional meaning, and naming it is the same as naming nothing.
            var angle = (float?) null;
            var axis = false;

            foreach (var word in turned)
            {
                if (CssValues.ParseAngle(word) is {} degrees && angle is null)
                {
                    angle = degrees;
                    continue;
                }

                if (!axis && word.Equals("z", StringComparison.OrdinalIgnoreCase))
                {
                    axis = true;
                    continue;
                }

                return false;
            }

            if (angle is not {} value)
            {
                return false;
            }

            functions.Add(new(TransformKind.Rotate, CssLength.Zero, CssLength.Zero, value));
        }

        if (Words(scale) is {} sized)
        {
            // One value scales both axes, where a missing `translate` component is zero. The two
            // properties differ here because the identity differs: no movement is zero and no
            // scaling is one.
            if (sized.Length > 2)
            {
                return false;
            }

            if (Factor(sized[0]) is not {} x)
            {
                return false;
            }

            var y = x;

            if (sized.Length > 1)
            {
                if (Factor(sized[1]) is not {} second)
                {
                    return false;
                }

                y = second;
            }

            functions.Add(new(TransformKind.Scale, CssLength.Zero, CssLength.Zero, x, y));
        }

        return true;

        static string[]? Words(string value)
        {
            var text = value.Trim();

            if (text.Length == 0 || text.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        // A percentage is a scale factor here, unlike everywhere else in CSS, where it is a
        // fraction of something. `scale: 150%` and `scale: 1.5` are the same declaration.
        static float? Factor(string word)
        {
            var text = word;
            var percent = text.EndsWith('%');

            if (percent)
            {
                text = text[..^1];
            }

            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            return percent ? value / 100f : value;
        }
    }

    /// <summary>
    /// The transform as an affine matrix for a box of <paramref name="border"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Composed left to right, so the RIGHTMOST function reaches a point first:
    /// <c>translate(30px) rotate(15deg)</c> rotates the box about the origin and then moves it,
    /// rather than moving it and rotating about where it started. <see cref="Matrix3x2"/> takes ROW
    /// vectors, so that is the product in REVERSE written order — which is why the loop below
    /// multiplies each function onto the LEFT of what it has so far.
    /// </para>
    /// <para>
    /// The whole thing is conjugated by the origin — moved there, applied, moved back — because
    /// every function turns about the coordinate system's zero and CSS turns them about the box's
    /// own <c>transform-origin</c>, which defaults to its centre.
    /// </para>
    /// </remarks>
    public Matrix3x2 Resolve(Rect border)
    {
        var matrix = Matrix3x2.Identity;

        foreach (var function in Functions)
        {
            matrix = Single(function, border) * matrix;
        }

        var originX = border.X + OriginX.Resolve(border.Width);
        var originY = border.Y + OriginY.Resolve(border.Height);

        // A product of row-vector matrices reads in the order a point meets them: to the origin,
        // through the functions, and back.
        return Matrix3x2.CreateTranslation(-originX, -originY) *
               matrix *
               Matrix3x2.CreateTranslation(originX, originY);
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
    public static Rect Bounds(Matrix3x2 matrix, Rect border)
    {
        var (left, top, right, bottom) = (float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

        foreach (var corner in new Vector2[]
                 {
                     new(border.X, border.Y),
                     new(border.Right, border.Y),
                     new(border.Right, border.Bottom),
                     new(border.X, border.Bottom)
                 })
        {
            var point = Vector2.Transform(corner, matrix);

            left = MathF.Min(left, point.X);
            top = MathF.Min(top, point.Y);
            right = MathF.Max(right, point.X);
            bottom = MathF.Max(bottom, point.Y);
        }

        return new(left, top, right - left, bottom - top);
    }

    /// <summary>One function's own matrix, before the origin is applied.</summary>
    static Matrix3x2 Single(TransformFunction function, Rect border)
    {
        switch (function.Kind)
        {
            case TransformKind.Translate:
                return Matrix3x2.CreateTranslation(
                    function.X.Resolve(border.Width),
                    function.Y.Resolve(border.Height));

            case TransformKind.Scale:
                return Matrix3x2.CreateScale(function.A, function.B);

            case TransformKind.Rotate:
                return Matrix3x2.CreateRotation(float.DegreesToRadians(function.A));

            case TransformKind.Skew:
                return Matrix3x2.CreateSkew(
                    float.DegreesToRadians(function.A),
                    float.DegreesToRadians(function.B));

            // A matrix given outright, whose six arguments are Matrix3x2's six components in
            // order: a and b the first row, c and d the second, e and f the translation.
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
    public static Matrix3x2 Combine(Matrix3x2 ancestor, Matrix3x2 own) =>
        own * ancestor;

    /// <summary>One function, or null when it is not one this engine applies.</summary>
    static TransformFunction? Function(
        string name,
        IReadOnlyList<string> arguments,
        CssFont fontSize,
        CssRoot root)
    {
        CssLength? Length(int index)
        {
            if (index >= arguments.Count)
            {
                return CssLength.Zero;
            }

            if (CssValues.ParseLength(arguments[index], fontSize, root, CssLength.None) is
                {Kind: not LengthKind.None} parsed)
            {
                return parsed;
            }

            return null;
        }

        float? Number(int index, float fallback)
        {
            if (index >= arguments.Count)
            {
                return fallback;
            }

            if (float.TryParse(
                    arguments[index],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                return value;
            }

            return null;
        }

        float? Angle(int index, float fallback)
        {
            if (index < arguments.Count)
            {
                return CssValues.ParseAngle(arguments[index]);
            }

            return fallback;
        }

        switch (name)
        {
            case "translate" when arguments.Count is 1 or 2:
                if (Length(0) is { } tx && Length(1) is { } ty)
                {
                    return new(TransformKind.Translate, tx, ty, 0);
                }

                return null;

            case "translatex" when arguments.Count == 1:
                if (Length(0) is { } onlyX)
                {
                    return new(TransformKind.Translate, onlyX, CssLength.Zero, 0);
                }

                return null;

            case "translatey" when arguments.Count == 1:
                if (Length(0) is { } onlyY)
                {
                    return new(TransformKind.Translate, CssLength.Zero, onlyY, 0);
                }

                return null;

            case "scale" when arguments.Count is 1 or 2:
                if (Number(0, 1) is { } sx && Number(1, sx) is { } sy)
                {
                    return new(TransformKind.Scale, CssLength.Zero, CssLength.Zero, sx, sy);
                }

                return null;

            case "scalex" when arguments.Count == 1:
                if (Number(0, 1) is { } scaleX)
                {
                    return new(TransformKind.Scale, CssLength.Zero, CssLength.Zero, scaleX, 1);
                }

                return null;

            case "scaley" when arguments.Count == 1:
                if (Number(0, 1) is { } scaleY)
                {
                    return new(TransformKind.Scale, CssLength.Zero, CssLength.Zero, 1, scaleY);
                }

                return null;

            case "rotate" when arguments.Count == 1:
                if (Angle(0, 0) is {} degrees)
                {
                    return new(TransformKind.Rotate, CssLength.Zero, CssLength.Zero, degrees);
                }

                return null;

            case "skew" when arguments.Count is 1 or 2:
                if (Angle(0, 0) is { } skewX && Angle(1, 0) is { } skewY)
                {
                    return new(TransformKind.Skew, CssLength.Zero, CssLength.Zero, skewX, skewY);
                }

                return null;

            case "skewx" when arguments.Count == 1:
                if (Angle(0, 0) is { } onlySkewX)
                {
                    return new(TransformKind.Skew, CssLength.Zero, CssLength.Zero, onlySkewX);
                }

                return null;

            case "skewy" when arguments.Count == 1:
                if (Angle(0, 0) is { } onlySkewY)
                {
                    return new(TransformKind.Skew, CssLength.Zero, CssLength.Zero, 0, onlySkewY);
                }

                return null;

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
    static (CssLength X, CssLength Y) Origin(string value, CssFont fontSize, CssRoot root)
    {
        var half = CssLength.Percentage(50);
        var parts = value.Trim().ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return (half, half);
        }

        var x = Component(parts[0]) ?? half;
        var y = parts.Length > 1 ? Component(parts[1]) ?? half : half;

        return (x, y);

        CssLength? Component(string part) =>
            part switch
            {
                "left" or "top" => CssLength.Zero,
                "center" => CssLength.Percentage(50),
                "right" or "bottom" => CssLength.Percentage(100),
                _ => CssValues.ParseLength(part, fontSize, root, CssLength.None) is
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
