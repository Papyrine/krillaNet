/// <summary>
/// The gradient shapes <c>background-image</c> can name.
/// </summary>
enum GradientKind
{
    /// <summary>A ramp along a line.</summary>
    Linear,

    /// <summary>A ramp out from a point, circular.</summary>
    Circle,

    /// <summary>A ramp out from a point, stretched to the box's proportions.</summary>
    Ellipse
}

/// <summary>
/// Which way a linear gradient runs, before a box is known.
/// </summary>
/// <remarks>
/// <para>
/// A corner keyword cannot be reduced to an angle at parse time: the angle it names depends on the
/// box's proportions, since the gradient line has to come out perpendicular to the diagonal joining
/// the other two corners. So the keyword is carried through and resolved against the box.
/// </para>
/// <para>
/// It never arrives, and that is an AngleSharp limitation rather than dead code. The cascade
/// rewrites <c>to top right</c> as <c>45deg</c> before this engine sees it — correct only for a
/// square box, and indistinguishable from an angle the author wrote, so it cannot even be
/// reported. The resolution below is kept because it is right, cheap and needed the moment the
/// value survives the cascade.
/// </para>
/// </remarks>
enum GradientCorner
{
    /// <summary>Not a corner — <see cref="CssGradient.Angle"/> holds the direction.</summary>
    None,

    /// <summary>Toward the top-right corner.</summary>
    TopRight,

    /// <summary>Toward the bottom-right corner.</summary>
    BottomRight,

    /// <summary>Toward the bottom-left corner.</summary>
    BottomLeft,

    /// <summary>Toward the top-left corner.</summary>
    TopLeft
}

/// <summary>
/// One colour stop, with its position left as declared.
/// </summary>
/// <param name="Color">The colour.</param>
/// <param name="Alpha">Its alpha, from 0 to 1, kept apart because krilla's colours carry none.</param>
/// <param name="Position">Where it sits, or null to be spaced evenly by <see cref="CssGradient"/>.</param>
/// <remarks>
/// A percentage resolves against the gradient line and a length against its length in pixels, so
/// neither can be settled until the box is known — which is why this holds a
/// <see cref="CssLength"/> rather than a number.
/// </remarks>
readonly record struct CssGradientStop(Color Color, float Alpha, CssLength? Position);

/// <summary>
/// A parsed <c>linear-gradient()</c> or <c>radial-gradient()</c>.
/// </summary>
/// <remarks>
/// <para>
/// Parsed once during the cascade and resolved against a box at paint time, which is the same
/// split <see cref="CssLength"/> uses and for the same reason: the direction of a corner keyword,
/// the length of the gradient line and the position of a stop given in pixels all depend on the
/// box, and the box is a layout result.
/// </para>
/// <para>
/// The syntax accepted is deliberately narrower than CSS's. An explicit radial size or position,
/// <c>repeating-</c> gradients, <c>conic-gradient</c>, colour interpolation hints and
/// <c>url()</c> are all left unparsed, which is what keeps <see cref="UnsupportedCss"/> honest:
/// anything this returns null for is reported rather than silently dropped.
/// </para>
/// </remarks>
sealed record CssGradient(
    GradientKind Kind,
    float Angle,
    GradientCorner Corner,
    IReadOnlyList<CssGradientStop> Stops)
{
    /// <summary>
    /// Parses a <c>background-image</c> value, or returns null when it is not a gradient this
    /// engine draws.
    /// </summary>
    public static CssGradient? Parse(string value, float fontSize, float rootFontSize)
    {
        var text = value.Trim();

        var kind = text.StartsWith("linear-gradient(", StringComparison.OrdinalIgnoreCase)
            ? GradientKind.Linear
            : text.StartsWith("radial-gradient(", StringComparison.OrdinalIgnoreCase)
                ? GradientKind.Ellipse
                : (GradientKind?) null;

        if (kind is not {} shape || !text.EndsWith(')'))
        {
            return null;
        }

        var arguments = Split(text[(text.IndexOf('(') + 1)..^1]);
        if (arguments.Count == 0)
        {
            return null;
        }

        var angle = 180f;
        var corner = GradientCorner.None;
        var first = 0;

        if (shape == GradientKind.Linear)
        {
            if (Direction(arguments[0]) is {} direction)
            {
                (angle, corner) = direction;
                first = 1;
            }
        }
        else if (Shape(arguments[0]) is {} radial)
        {
            shape = radial;
            first = 1;
        }

        var stops = new List<CssGradientStop>();

        for (var index = first; index < arguments.Count; index++)
        {
            if (Stop(arguments[index], fontSize, rootFontSize) is not {} stop)
            {
                return null;
            }

            stops.Add(stop);
        }

        // One stop is a flat fill rather than a gradient, and CSS requires at least two.
        return stops.Count < 2 ? null : new(shape, angle, corner, stops);
    }

    /// <summary>
    /// The direction a linear gradient's first argument names, or null when it is a colour stop.
    /// </summary>
    /// <remarks>
    /// Angles are measured clockwise from <c>to top</c>, which is the specification's own frame and
    /// the reason <c>to bottom</c> — the default when nothing is given — is 180 rather than 0.
    /// </remarks>
    static (float Angle, GradientCorner Corner)? Direction(string argument)
    {
        var text = argument.Trim().ToLowerInvariant();

        if (text.StartsWith("to ", StringComparison.Ordinal))
        {
            var sides = text[3..].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return sides.Length switch
            {
                1 => sides[0] switch
                {
                    "top" => (0f, GradientCorner.None),
                    "right" => (90f, GradientCorner.None),
                    "bottom" => (180f, GradientCorner.None),
                    "left" => (270f, GradientCorner.None),
                    _ => null
                },
                2 => Both(sides[0], sides[1]) ?? Both(sides[1], sides[0]),
                _ => null
            };

            static (float, GradientCorner)? Both(string vertical, string horizontal) =>
                (vertical, horizontal) switch
                {
                    ("top", "right") => (0f, GradientCorner.TopRight),
                    ("bottom", "right") => (0f, GradientCorner.BottomRight),
                    ("bottom", "left") => (0f, GradientCorner.BottomLeft),
                    ("top", "left") => (0f, GradientCorner.TopLeft),
                    _ => null
                };
        }

        return CssValues.ParseAngle(text) is {} degrees ? (degrees, GradientCorner.None) : null;
    }

    /// <summary>
    /// A radial gradient's shape keyword, or null when the argument is a colour stop.
    /// </summary>
    /// <remarks>
    /// A bare <c>circle</c> or <c>ellipse</c> only. Anything carrying a size or an
    /// <c>at</c> position is left unparsed so it is reported: those change where the ramp starts
    /// and how far it reaches, and drawing the default instead would be wrong in a way nothing
    /// would say.
    /// </remarks>
    static GradientKind? Shape(string argument)
    {
        var words = argument.Trim().ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
        {
            return null;
        }

        var shape = words[0] switch
        {
            "circle" => GradientKind.Circle,
            "ellipse" => GradientKind.Ellipse,
            _ => (GradientKind?) null
        };

        if (shape is null)
        {
            return null;
        }

        // The cascade writes the shape back with its defaults spelled out and the size slot left
        // empty — `circle` comes back as `circle  at center`, two spaces and all. Anything beyond
        // those defaults changes where the ramp starts or how far it reaches, so it is left
        // unparsed and reported rather than drawn as though it had not been written.
        foreach (var word in words[1..])
        {
            if (word is not ("at" or "center" or "farthest-corner"))
            {
                return null;
            }
        }

        return shape;
    }

    /// <summary>
    /// One <c>&lt;color&gt; [&lt;position&gt;]</c> argument.
    /// </summary>
    static CssGradientStop? Stop(string argument, float fontSize, float rootFontSize)
    {
        var text = argument.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        // The colour may itself carry spaces inside brackets, so the position is taken from the
        // end rather than the value being split on the first space.
        var split = text.LastIndexOf(' ');
        var position = (CssLength?) null;

        if (split > 0 && !text.EndsWith(')'))
        {
            var tail = text[(split + 1)..];
            var parsed = CssValues.ParseLength(tail, fontSize, rootFontSize, CssLength.None);

            if (parsed.Kind is LengthKind.None)
            {
                return null;
            }

            position = parsed;
            text = text[..split];
        }

        return CssValues.ParseColor(text) is {} color
            ? new CssGradientStop(color, CssValues.ParseAlpha(text), position)
            : null;
    }

    /// <summary>
    /// Splits a function's arguments on top-level commas.
    /// </summary>
    /// <remarks>
    /// Bracket-aware, because a stop's colour is frequently <c>rgba(192, 64, 64, 1)</c> and a plain
    /// split on commas turns one stop into four unparseable ones.
    /// </remarks>
    static List<string> Split(string arguments)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    parts.Add(arguments[start..index]);
                    start = index + 1;
                    break;
            }
        }

        parts.Add(arguments[start..]);
        return parts;
    }
}
