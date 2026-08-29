/// <summary>
/// One flex item, with everything the algorithm decides about it along the way.
/// </summary>
/// <remarks>
/// <para>
/// Written in MAIN and CROSS terms rather than in horizontal and vertical ones, which is what lets
/// the four <c>flex-direction</c> values share one implementation: <see cref="FlexLayout"/> maps
/// the axes once, at the top, and nothing below it asks which way round the container is. The
/// mapping back to a rectangle happens in one place, when the item is finally translated.
/// </para>
/// <para>
/// Every size here is a BORDER-box size. That is the geometry of record everywhere else in this
/// engine, it is what <see cref="BlockLayout.Layout"/> takes as an assigned width, and it is what
/// <see cref="IntrinsicWidths.Measure"/> returns — so keeping to it means the algorithm never
/// converts, except at the two points where CSS itself asks for something else and says so.
/// </para>
/// </remarks>
sealed class FlexItem
{
    public required LayoutBox Box { get; init; }

    /// <summary>The item's own style, which is the box's.</summary>
    public ComputedStyle Style => Box.Style;

    /// <summary>Margin before the item along the main axis.</summary>
    public float MainStart { get; set; }

    /// <summary>Margin after the item along the main axis.</summary>
    public float MainEnd { get; set; }

    /// <summary>Margin before the item along the cross axis.</summary>
    public float CrossStart { get; set; }

    /// <summary>Margin after the item along the cross axis.</summary>
    public float CrossEnd { get; set; }

    /// <summary>Whether the main-start margin was <c>auto</c>.</summary>
    /// <remarks>
    /// An auto margin on a flex item absorbs free space rather than resolving to zero, which is
    /// what makes <c>margin-left: auto</c> on one item push everything after it to the far end —
    /// the idiom that replaced a float for a toolbar. It has to be remembered as a FLAG because
    /// the amount it absorbs is not known until the whole line has been measured.
    /// </remarks>
    public bool AutoMainStart { get; set; }

    /// <inheritdoc cref="AutoMainStart"/>
    public bool AutoMainEnd { get; set; }

    /// <inheritdoc cref="AutoMainStart"/>
    public bool AutoCrossStart { get; set; }

    /// <inheritdoc cref="AutoMainStart"/>
    public bool AutoCrossEnd { get; set; }

    /// <summary>Padding and border along the main axis, which a content size excludes.</summary>
    public float MainSurround { get; set; }

    /// <summary>Padding and border along the cross axis.</summary>
    public float CrossSurround { get; set; }

    /// <summary>The main size the item starts from, before growing or shrinking.</summary>
    public float Base { get; set; }

    /// <summary>The base size clamped by the item's own minimum and maximum.</summary>
    public float Hypothetical { get; set; }

    /// <summary>The smallest main size the item may take.</summary>
    /// <remarks>
    /// For an item whose <c>min-width</c> (or <c>min-height</c>) is <c>auto</c> this is the
    /// automatic minimum size of CSS Flexbox §4.5 rather than zero, which is what stops
    /// <c>flex-shrink</c> squeezing a word narrower than it can be drawn.
    /// </remarks>
    public float MinMain { get; set; }

    /// <summary>The largest main size the item may take, or positive infinity for none.</summary>
    public float MaxMain { get; set; } = float.PositiveInfinity;

    /// <summary>The main size the flexing settled on.</summary>
    public float Main { get; set; }

    /// <summary>The cross size the item ended up with.</summary>
    public float Cross { get; set; }

    /// <summary>Whether the flexible-length loop has settled this item.</summary>
    public bool Frozen { get; set; }

    /// <summary>
    /// How far the item's first baseline sits below its outer cross-start edge, when it takes part
    /// in baseline alignment.
    /// </summary>
    public float Baseline { get; set; }

    /// <summary>Where the item's border box starts along the main axis.</summary>
    public float MainPosition { get; set; }

    /// <summary>And along the cross axis.</summary>
    public float CrossPosition { get; set; }

    /// <summary>The main-axis room the item occupies on its line, margins included.</summary>
    public float OuterHypothetical => MainStart + Hypothetical + MainEnd;

    /// <summary>The same once the flexing has settled a size.</summary>
    public float OuterMain => MainStart + Main + MainEnd;

    /// <summary>The cross-axis room the item occupies in its line, margins included.</summary>
    public float OuterCross => CrossStart + Cross + CrossEnd;

    /// <summary>
    /// The base size as a CONTENT size, which is what scales a shrink factor.
    /// </summary>
    /// <remarks>
    /// The one place the algorithm leaves border boxes, and it is CSS Flexbox §9.7's own wording:
    /// the scaled flex shrink factor is the shrink factor times the INNER flex base size. Using the
    /// border box instead lets a heavily padded item give up more than its share, which shows up
    /// as two items with equal <c>flex-shrink</c> ending at different widths.
    /// </remarks>
    public float InnerBase => Math.Max(0, Base - MainSurround);
}
