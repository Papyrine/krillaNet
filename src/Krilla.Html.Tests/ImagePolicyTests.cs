/// <summary>
/// Checks on <see cref="HtmlOptions.LocalImages"/> and <see cref="HtmlOptions.WebImages"/>.
///
/// The policy is checked before the resolver runs, so these also pin that it constrains a
/// caller-supplied resolver and not only the built-in one — which is the half that matters, since
/// a resolver that fetches is the one with something to constrain.
/// </summary>
public class ImagePolicyTests
{
    // A 1x1 transparent PNG, so that "the image loaded" is distinguishable from "it did not"
    // without a file on disk being involved.
    const string pixel =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42m" +
        "NkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    [Test]
    public async Task DataUrisAreNeverGated()
    {
        // Their bytes are already in the document, so loading one reaches nothing new. Denying
        // both policies must not stop them.
        var boxes = Measure(
            $"<img src=\"{pixel}\">",
            ImagePolicy.Deny(),
            ImagePolicy.Deny(),
            resolver: null);

        await Assert.That(boxes).Contains(_ => _.Selector.EndsWith("img:nth-child(1)"));
    }

    [Test]
    public async Task DenyRefusesAWebSourceBeforeTheResolverRuns()
    {
        var asked = new List<string>();

        var reports = Convert(
            "<img src=\"https://example.com/a.png\">",
            ImagePolicy.AllowAll(),
            ImagePolicy.Deny(),
            source =>
            {
                asked.Add(source);
                return null;
            });

        // The point of gating ahead of the resolver: a refused source is never even requested, so
        // no connection is opened to a host the document chose.
        await Assert.That(asked).IsEmpty();
        await Assert.That(reports.Single().Reason).Contains("refused by the image policy");
    }

    [Test]
    public async Task SafeDomainsAllowsTheHostAndItsSubdomains()
    {
        var policy = ImagePolicy.SafeDomains("example.com");

        await Assert.That(Allows(policy, "https://example.com/a.png")).IsTrue();
        await Assert.That(Allows(policy, "https://cdn.example.com/a.png")).IsTrue();
        await Assert.That(Allows(policy, "https://example.com.evil.test/a.png")).IsFalse();
        await Assert.That(Allows(policy, "https://notexample.com/a.png")).IsFalse();
        await Assert.That(Allows(policy, "not a url")).IsFalse();
    }

    [Test]
    public async Task SafeDirectoriesRefusesAPathClimbingOutOfIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "krilla-policy");
        var policy = ImagePolicy.SafeDirectories(root);

        await Assert.That(Allows(policy, Path.Combine(root, "a.png"))).IsTrue();
        await Assert.That(Allows(policy, Path.Combine(root, "nested", "a.png"))).IsTrue();

        // The check that makes this worth having: full paths are compared, so a traversal is
        // refused rather than followed.
        await Assert.That(Allows(policy, Path.Combine(root, "..", "a.png"))).IsFalse();

        // A sibling directory whose name merely starts with the allowed one.
        await Assert.That(Allows(policy, root + "-other" + Path.DirectorySeparatorChar + "a.png")).IsFalse();
    }

    [Test]
    public async Task FilterDecides()
    {
        var policy = ImagePolicy.Filter(_ => _.EndsWith(".png"));

        await Assert.That(Allows(policy, "/tmp/a.png")).IsTrue();
        await Assert.That(Allows(policy, "/tmp/a.gif")).IsFalse();
    }

    [Test]
    public async Task ARefusalIsDistinguishableFromAFailure()
    {
        // Both leave the same gap on the page, and the difference between them is the whole
        // question a reader has: one means the document asked for something it was not allowed,
        // the other means the file was not there.
        var reports = Convert(
            "<img src=\"missing.png\"><img src=\"https://example.com/a.png\">",
            ImagePolicy.AllowAll(),
            ImagePolicy.Deny(),
            _ => null);

        await Assert.That(reports.Count).IsEqualTo(2);
        await Assert.That(reports[0].Kind).IsEqualTo(HtmlDiagnosticKind.UnresolvedImage);
        await Assert.That(reports[0].Reason).Contains("did not resolve");
        await Assert.That(reports[1].Reason).Contains("refused by the image policy");
    }

    [Test]
    public async Task TheDefaultAllowsTheOrdinaryCase()
    {
        var options = CorpusRunner.Options();

        await Assert.That(options.LocalImages.IsAllowed("/anywhere/a.png")).IsTrue();
        await Assert.That(options.WebImages.IsAllowed("https://example.com/a.png")).IsTrue();
    }

    static bool Allows(ImagePolicy policy, string source) =>
        policy.IsAllowed(source);

    static IReadOnlyList<BoxGeometry> Measure(
        string body,
        ImagePolicy local,
        ImagePolicy web,
        Func<string, byte[]?>? resolver)
    {
        var options = Options(local, web, resolver);
        return BoxDump.Measure(Page(body), options);
    }

    static List<HtmlDiagnostic> Convert(
        string body,
        ImagePolicy local,
        ImagePolicy web,
        Func<string, byte[]?>? resolver)
    {
        var reports = new List<HtmlDiagnostic>();
        var options = Options(local, web, resolver);
        options.OnDiagnostic = reports.Add;
        HtmlConverter.Convert(Page(body), options);
        return reports;
    }

    static HtmlOptions Options(ImagePolicy local, ImagePolicy web, Func<string, byte[]?>? resolver)
    {
        var options = CorpusRunner.Options();
        options.LocalImages = local;
        options.WebImages = web;
        options.ImageResolver = resolver;
        return options;
    }

    static string Page(string body) =>
        $"<!doctype html><html><head></head><body>{body}</body></html>";
}
