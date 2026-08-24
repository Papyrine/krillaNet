namespace Krilla.Html;

/// <summary>
/// How a document is converted: the page it goes onto, and the fonts it may use.
/// </summary>
/// <remarks>
/// Every dimension is in CSS pixels, at 96 to the inch, because that is the unit the document
/// itself is written in — a stylesheet saying <c>width: 600px</c> should not have to be reconciled
/// against a page expressed in points. The conversion to points happens once, when the page is
/// painted.
/// </remarks>
public sealed class HtmlOptions
{
    /// <summary>CSS pixels per point, for callers working in PDF units.</summary>
    public const float PixelsPerPoint = 96f / 72f;

    /// <summary>Page width in CSS pixels. Defaults to US Letter.</summary>
    public float PageWidth { get; set; } = 816;

    /// <summary>Page height in CSS pixels. Defaults to US Letter.</summary>
    public float PageHeight { get; set; } = 1056;

    /// <summary>Top page margin in CSS pixels.</summary>
    public float MarginTop { get; set; }

    /// <summary>Right page margin in CSS pixels.</summary>
    public float MarginRight { get; set; }

    /// <summary>Bottom page margin in CSS pixels.</summary>
    public float MarginBottom { get; set; }

    /// <summary>Left page margin in CSS pixels.</summary>
    public float MarginLeft { get; set; }

    /// <summary>
    /// The faces available to the document.
    /// </summary>
    /// <remarks>
    /// Required. krilla has no font database, so there is no set of fonts to fall back on — a
    /// conversion with none registered throws rather than silently producing a blank page.
    /// </remarks>
    public FontSet? Fonts { get; set; }

    /// <summary>
    /// The base address relative URLs resolve against.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Turns an image source into encoded bytes, or null when it cannot be resolved. An unresolved
    /// <c>&lt;img&gt;</c> generates no box, as in a browser.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every image source in the document comes through here, not only the ones in the markup: a
    /// <c>url()</c> in <c>background-image</c> or <c>list-style-image</c> is resolved by the same
    /// route and bound by the same two policies. A stylesheet is the part of a document a reader is
    /// least likely to have read, so it would be a poor place to leave a hole.
    /// </para>
    /// <para>
    /// Unset resolves <c>data:</c> URIs and files relative to <see cref="BaseUrl"/>, and
    /// deliberately does NOT fetch over the network. Converting an untrusted document would
    /// otherwise issue requests to whatever hosts that document names, which leaks that the
    /// conversion happened and can be used to probe hosts reachable from the converting machine.
    /// </para>
    /// <para>
    /// Supplying a resolver that fetches is a decision to make explicitly, with whatever timeout,
    /// size limit and host allow-list the situation calls for.
    /// </para>
    /// </remarks>
    public Func<string, byte[]?>? ImageResolver { get; set; }

    /// <summary>
    /// Which local files images may be loaded from. Defaults to allowing any.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Narrow it with <see cref="ImagePolicy.SafeDirectories"/> when converting a document that is
    /// not trusted. Every <c>src</c> in such a document is a path chosen by whoever wrote it, and
    /// the bytes it names end up inside a PDF that is usually then sent somewhere — so an
    /// unrestricted conversion is a way to read a file off the converting machine and have it
    /// delivered.
    /// </para>
    /// <para>
    /// The default is permissive because refusing by default would break the ordinary case of
    /// converting a document alongside its own images. The narrowing is one call.
    /// </para>
    /// </remarks>
    public ImagePolicy LocalImages { get; set; } = ImagePolicy.AllowAll();

    /// <summary>
    /// Which web hosts images may be loaded from, for a resolver that fetches. Defaults to
    /// allowing any.
    /// </summary>
    /// <remarks>
    /// The default resolver never fetches, so this constrains nothing until
    /// <see cref="ImageResolver"/> is set to one that does — at which point
    /// <see cref="ImagePolicy.SafeDomains"/> is the allow-list that keeps a document from naming
    /// hosts of its own choosing.
    /// </remarks>
    public ImagePolicy WebImages { get; set; } = ImagePolicy.AllowAll();

    /// <summary>
    /// The root font size in CSS pixels, which <c>rem</c> resolves against and which an element
    /// without a <c>font-size</c> inherits. Browsers default to 16.
    /// </summary>
    public float RootFontSize { get; set; } = 16;

    /// <summary>
    /// Whether a document's <c>@page</c> rules override the page geometry set here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True by default. A document that declares <c>@page { size: A4 }</c> means it, and a
    /// converter that ignores the declaration prints an A4 report onto US Letter — a
    /// whole-document difference with nothing to explain it. Only the size, the orientation and the
    /// four margins are read; a rule declaring nothing leaves everything here alone, so the default
    /// changes nothing for a document that says nothing.
    /// </para>
    /// <para>
    /// Set it false to keep the paper a caller has already chosen, which is the right answer when
    /// the size comes from a printer or an envelope rather than from the document.
    /// </para>
    /// </remarks>
    public bool HonourPageRules { get; set; } = true;

    /// <summary>
    /// Whether <c>orphans</c> and <c>widows</c> constrain where a page breaks. Off by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Off because CHROMIUM DOES NOT IMPLEMENT THEM, and this converter's fidelity is measured
    /// against Chromium. That is not an assumption: the corpus scenario
    /// <c>page/break_between_lines</c> holds a browser reference in which a three-line paragraph is
    /// broken after its second line, leaving one line overleaf under the initial <c>widows: 2</c> —
    /// so the browser ignored the property, and honouring it by default would make this engine
    /// disagree with the reference on every document long enough to paginate.
    /// </para>
    /// <para>
    /// Turn it on for typographic quality rather than for browser fidelity. The two are genuinely
    /// in conflict here, which is why this is a switch rather than a decision taken once: a single
    /// line stranded at the foot or head of a page is the defect the properties exist to prevent,
    /// and a print engine that can prevent it should be able to.
    /// </para>
    /// </remarks>
    public bool HonourOrphansAndWidows { get; set; }

    /// <summary>Metadata written into the PDF.</summary>
    public DocumentMetadata? Metadata { get; set; }

    /// <summary>
    /// Optional sink for constructs the converter recognised and did not render the way a browser
    /// would — a declaration it laid out as something else, an attribute it could not apply, an
    /// element it generates no box for, an image that resolved to nothing. Called as each happens,
    /// on the converting thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The engine implements a subset of CSS and lays out the rest as a plain block. That keeps
    /// content on the page, which beats dropping it, but it means a document using an
    /// unimplemented construct comes out wrong with nothing to say so. Subscribing turns those
    /// into reports, so a conversion can be checked rather than eyeballed.
    /// </para>
    /// <para>
    /// The invariant worth having: a conversion that reports nothing laid out every construct in
    /// the document the way a browser would. Unrecognised CSS is deliberately NOT reported —
    /// listing every <c>cursor</c> and <c>content</c> an ordinary stylesheet carries would bury
    /// the signal and cost that invariant its meaning.
    /// </para>
    /// <para>
    /// Default is null, under which nothing is reported and the scan that finds these is skipped
    /// entirely rather than running and discarding its results.
    /// </para>
    /// </remarks>
    public Action<HtmlDiagnostic>? OnDiagnostic { get; set; }

    /// <summary>US Letter, 8.5 x 11 inches.</summary>
    public static HtmlOptions Letter => new();

    /// <summary>A4, 210 x 297 millimetres.</summary>
    public static HtmlOptions A4 =>
        new()
        {
            PageWidth = 210 / 25.4f * 96,
            PageHeight = 297 / 25.4f * 96
        };

    /// <summary>Sets all four page margins.</summary>
    /// <summary>
    /// A copy of these options with the page geometry replaced.
    /// </summary>
    /// <remarks>
    /// Used to fold a document's <c>@page</c> rules in without mutating what the caller handed
    /// over: the same options object is often reused across conversions, and one document's page
    /// size leaking into the next would be a memorable bug.
    /// </remarks>
    internal HtmlOptions WithPage(float width, float height, float top, float right, float bottom, float left) =>
        new()
        {
            PageWidth = width,
            PageHeight = height,
            MarginTop = top,
            MarginRight = right,
            MarginBottom = bottom,
            MarginLeft = left,
            Fonts = Fonts,
            BaseUrl = BaseUrl,
            ImageResolver = ImageResolver,
            LocalImages = LocalImages,
            WebImages = WebImages,
            RootFontSize = RootFontSize,
            HonourPageRules = HonourPageRules,
            HonourOrphansAndWidows = HonourOrphansAndWidows,
            Metadata = Metadata,
            OnDiagnostic = OnDiagnostic
        };

    public HtmlOptions WithMargin(float margin)
    {
        MarginTop = margin;
        MarginRight = margin;
        MarginBottom = margin;
        MarginLeft = margin;
        return this;
    }

    /// <summary>The width available to content, after page margins.</summary>
    internal float ContentWidth => Math.Max(0, PageWidth - MarginLeft - MarginRight);

    /// <summary>The height available to content, after page margins.</summary>
    internal float ContentHeight => Math.Max(0, PageHeight - MarginTop - MarginBottom);
}
