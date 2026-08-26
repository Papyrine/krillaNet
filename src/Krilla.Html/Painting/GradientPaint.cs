/// <summary>
/// Turns a parsed <see cref="CssGradient"/> into a krilla <see cref="Krilla.Paint"/> for a given box.
/// </summary>
/// <remarks>
/// <para>
/// Every number here was checked against Chrome's own render rather than taken from the
/// specification's prose, and the checks are arithmetic rather than visual: a colour sampled at a
/// known pixel is a linear function of the geometry, so one sample pins the gradient line exactly.
/// </para>
/// <para>
/// The gradient's box is the PADDING box, not the border box — which is
/// <c>background-origin: padding-box</c>, the initial value. The background is then painted out to
/// the border box, so a box with a border shows the ramp continuing underneath it.
/// </para>
/// </remarks>
static class GradientPaint
{
    /// <summary>
    /// Builds the paint for <paramref name="gradient"/> filling <paramref name="box"/>.
    /// </summary>
    /// <remarks>
    /// The caller owns the result and must dispose it.
    /// </remarks>
    public static Paint Create(CssGradient gradient, Rect box, bool tiles)
    {
        var stops = Stops(gradient, Length(gradient, box));

        if (gradient.Kind == GradientKind.Linear)
        {
            return Linear(gradient, box, stops, tiles);
        }

        return Radial(gradient, box, stops);
    }

    /// <summary>
    /// A linear gradient's paint, running along the line CSS Images 3 §3.1 describes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The line runs through the box's centre at the declared angle, and is long enough that a
    /// perpendicular through each of the two corners nearest the ends meets it at 0% and 100%.
    /// That reduces to a length of <c>|W·sin A| + |H·cos A|</c>, which is the one piece of this
    /// that is not obvious and is confirmed by measurement: a 45° gradient in a 200×60 box puts
    /// its start colour exactly on the bottom-left corner and its end exactly on the top-right.
    /// </para>
    /// <para>
    /// The angle is clockwise from <c>to top</c>, so the direction vector is
    /// <c>(sin A, −cos A)</c> in a coordinate system whose y grows downward.
    /// </para>
    /// <para>
    /// The spread is <c>Repeat</c> for an axis-aligned gradient and <c>Pad</c> otherwise, and the
    /// distinction is not a hedge. <c>background-repeat</c> defaults to <c>repeat</c>, so a browser
    /// TILES the gradient's box across the area it paints — visible only where the two differ,
    /// which is under a border. An axis-aligned ramp is uniform perpendicular to its own axis, so
    /// repeating it along that axis IS the two-dimensional tiling; at any other angle the two part
    /// company and padding is the closer of the answers available here.
    /// </para>
    /// </remarks>
    static Paint Linear(
        CssGradient gradient,
        Rect box,
        IReadOnlyList<GradientStop> stops,
        bool tiles)
    {
        var angle = Resolve(gradient, box);
        var radians = angle * MathF.PI / 180f;
        var (dx, dy) = (MathF.Sin(radians), -MathF.Cos(radians));

        var half = Length(gradient, box) / 2;
        var (cx, cy) = (box.X + box.Width / 2, box.Y + box.Height / 2);

        var axisAligned = tiles && MathF.Abs(MathF.IEEERemainder(angle, 90f)) < 0.0001f;

        return Paint.LinearGradient(
            cx - dx * half,
            cy - dy * half,
            cx + dx * half,
            cy + dy * half,
            stops,
            axisAligned ? SpreadMethod.Repeat : SpreadMethod.Pad);
    }

    /// <summary>
    /// A radial gradient's paint, sized to reach the farthest corner.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>farthest-corner</c> is the initial size and the only one accepted. For a circle that is
    /// the distance from the centre to a corner. For an ellipse it is the box's own proportions
    /// scaled until the curve passes through the corner, which works out at exactly √2 times the
    /// half-width and half-height — measured, and the reason the sample at the left edge of a
    /// 200×60 box comes back at 0.7071 of the ramp rather than at 1.
    /// </para>
    /// <para>
    /// krilla draws circles, so the ellipse is a circle of the horizontal radius under a transform
    /// that scales the vertical axis about the centre.
    /// </para>
    /// </remarks>
    static Paint Radial(CssGradient gradient, Rect box, IReadOnlyList<GradientStop> stops)
    {
        var (cx, cy) = (box.X + box.Width / 2, box.Y + box.Height / 2);
        var (halfWidth, halfHeight) = (box.Width / 2, box.Height / 2);

        if (gradient.Kind == GradientKind.Circle)
        {
            var radius = MathF.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
            return Paint.RadialGradient(cx, cy, 0, cx, cy, radius, stops);
        }

        var rx = MathF.Max(0.01f, halfWidth * MathF.Sqrt(2));
        var ry = MathF.Max(0.01f, halfHeight * MathF.Sqrt(2));
        var scale = ry / rx;

        return Paint.RadialGradient(
            cx,
            cy,
            0,
            cx,
            cy,
            rx,
            stops,
            SpreadMethod.Pad,
            // The vertical axis scaled about the centre, and nothing else touched.
            Matrix3x2.CreateScale(1, scale, new(0, cy)));
    }

    /// <summary>
    /// The gradient line's length, which every stop position is measured against.
    /// </summary>
    static float Length(CssGradient gradient, Rect box)
    {
        if (gradient.Kind != GradientKind.Linear)
        {
            if (gradient.Kind == GradientKind.Circle)
            {
                return MathF.Sqrt(box.Width * box.Width + box.Height * box.Height) / 2;
            }

            return box.Width / 2 * MathF.Sqrt(2);
        }

        var radians = Resolve(gradient, box) * MathF.PI / 180f;
        return MathF.Abs(box.Width * MathF.Sin(radians)) +
               MathF.Abs(box.Height * MathF.Cos(radians));
    }

    /// <summary>
    /// The angle a linear gradient runs at, resolving a corner keyword against the box.
    /// </summary>
    /// <remarks>
    /// A corner gradient runs PERPENDICULAR to the diagonal joining the other two corners, so its
    /// angle depends on the box's proportions: <c>to top right</c> in a wide, short box is nearly
    /// <c>to top</c>, not the 45° the name suggests. The measured signature is that the other two
    /// corners both come out at exactly half way along the ramp, which is what a perpendicular
    /// through them means.
    /// </remarks>
    static float Resolve(CssGradient gradient, Rect box)
    {
        if (gradient.Corner == GradientCorner.None)
        {
            return gradient.Angle;
        }

        var diagonal = MathF.Atan2(box.Height, box.Width) * 180f / MathF.PI;

        return gradient.Corner switch
        {
            GradientCorner.TopRight => diagonal,
            GradientCorner.BottomRight => 180 - diagonal,
            GradientCorner.BottomLeft => 180 + diagonal,
            _ => 360 - diagonal
        };
    }

    /// <summary>
    /// The stops with every position settled, in the order krilla needs them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CSS Images 3 §3.4.3: the first stop defaults to 0 and the last to 1, an unpositioned stop
    /// between two positioned ones is spaced evenly between them, and a position smaller than the
    /// one before it is raised to match — which is what makes a pair of stops at the same place a
    /// hard edge rather than an error.
    /// </para>
    /// <para>
    /// A percentage is a fraction of the gradient line and a length is measured along it in pixels,
    /// which is why the line's length has to be computed before the stops rather than after.
    /// </para>
    /// </remarks>
    static IReadOnlyList<GradientStop> Stops(CssGradient gradient, float length)
    {
        var count = gradient.Stops.Count;
        var positions = new float?[count];

        for (var index = 0; index < count; index++)
        {
            positions[index] = gradient.Stops[index].Position switch
            {
                {Kind: LengthKind.Percent} percent => percent.Value / 100f,
                {Kind: LengthKind.Absolute} absolute => length <= 0 ? 0 : absolute.Value / length,
                _ => null
            };
        }

        positions[0] ??= 0f;
        positions[count - 1] ??= 1f;

        for (var index = 1; index < count; index++)
        {
            if (positions[index] is not null)
            {
                continue;
            }

            // The next stop that has one, so the run between them can be spaced evenly.
            var next = index;
            while (positions[next] is null)
            {
                next++;
            }

            var from = positions[index - 1]!.Value;
            var step = (positions[next]!.Value - from) / (next - index + 1);

            for (var gap = index; gap < next; gap++)
            {
                positions[gap] = from + step * (gap - index + 1);
            }
        }

        var stops = new GradientStop[count];
        var highest = 0f;

        for (var index = 0; index < count; index++)
        {
            var position = MathF.Max(highest, positions[index]!.Value);

            // Two stops at the same offset are a hard edge, and a PDF shading cannot express one:
            // its stitching function needs strictly increasing bounds, so a zero-width step is
            // dropped and the edge becomes a ramp across the whole gradient. Nudging the second of
            // a pair onto the next representable position keeps the edge within a sub-pixel of
            // where it was asked for.
            if (index > 0 && position <= highest)
            {
                position = MathF.BitIncrement(highest);
            }

            highest = position;
            stops[index] = new(
                Math.Clamp(position, 0f, 1f),
                gradient.Stops[index].Color,
                gradient.Stops[index].Alpha);
        }

        return stops;
    }
}
