using System.Reflection;
using System.Runtime.InteropServices;
using Krilla;

/// <summary>
/// Consumes the packed Krilla package the way a real project would.
/// </summary>
/// <remarks>
/// The unit suite in <c>src/Krilla.Tests</c> uses a project reference, so it proves the code
/// works but says nothing about whether the *package* does. Everything asserted here is a
/// packaging property: that the native asset for this runtime resolved out of
/// <c>runtimes/&lt;rid&gt;/native/</c>, that it loads, and that it actually runs.
///
/// This is the suite the release workflow runs on every shipped RID, which is the only way to
/// find out that, say, the linux-arm64 binary was built against too new a glibc — a failure
/// that is invisible on the machine that produced it.
/// </remarks>
public class ConsumerTests
{
    [Test]
    public async Task TheNativeLibraryResolvesAndLoads()
    {
        // Constructing a document is what forces the P/Invoke surface to bind. If the native
        // asset did not resolve, this is where a DllNotFoundException surfaces.
        using var document = new KrillaDocument();
        var pdf = document.Finish();

        await Assert.That(pdf.Length).IsGreaterThan(100);
        await Assert.That(Encoding.ASCII.GetString(pdf, 0, 5)).IsEqualTo("%PDF-");
    }

    /// <summary>
    /// The native asset is present in the output, in whichever layout the build produced.
    /// </summary>
    /// <remarks>
    /// A RID-agnostic build copies the whole <c>runtimes/</c> tree and lets the host resolve
    /// the right one through <c>deps.json</c>; a RID-specific build or publish flattens the
    /// matching file next to the assembly. Both are correct, so this accepts either — naming
    /// the expected file means a packaging regression says which one was missing rather than
    /// surfacing as a bare load failure.
    /// </remarks>
    [Test]
    public async Task TheNativeAssetIsPresentInTheOutput()
    {
        var expected = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "krilla_capi.dll"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "libkrilla_capi.dylib"
                : "libkrilla_capi.so";

        var directory = Path.GetDirectoryName(typeof(ConsumerTests).Assembly.Location)!;
        var found = Directory.GetFiles(directory, expected, SearchOption.AllDirectories);

        await Assert.That(found)
            .IsNotEmpty()
            .Because($"'{expected}' should have been copied from runtimes/{RuntimeInformation.RuntimeIdentifier}/native/ into {directory}");
    }

    [Test]
    public async Task DrawingProducesRealContent()
    {
        using var document = new KrillaDocument();
        using var paint = Paint.LinearGradient(
            0, 0, 200, 0,
            [
                new(0f, Color.Rgb(255, 0, 0)),
                new(1f, Color.Rgb(0, 0, 255))
            ]);

        using (var page = document.StartPage(PageSettings.A4))
        {
            using var path = PdfPath.Polygon(
                new Point(20, 20),
                new(180, 20),
                new(100, 160));

            page.Surface.SetFill(paint).DrawPath(path);
        }

        var pdf = document.Finish();

        // Exercises path building, gradients and compression together — the parts that pull
        // the widest slice of the statically linked dependency tree into play.
        await Assert.That(pdf.Length).IsGreaterThan(800);
    }

    [Test]
    public async Task StructureFeaturesWork()
    {
        using var document = new KrillaDocument(
            new()
            {
                EnableTagging = true
            });

        document.SetMetadata(
            new()
            {
                Title = "Package Smoke Test",
                Language = "en-GB"
            });

        TagIdentifier content;

        using (var page = document.StartPage(PageSettings.A4))
        {
            content = page.Surface.BeginText();
            page.Surface.FillRectangle(new(72, 72, 300, 120), Color.Gray(64));
            page.Surface.EndTagged();

            page.Surface.AddLink(new(72, 140, 300, 170), "https://example.com/");
        }

        using var tree = new TagTree();
        tree.WithLanguage("en-GB");
        tree.Add(TagKind.Section).Add(TagKind.Paragraph).Add(content);
        document.SetTagTree(tree);

        var text = Encoding.Latin1.GetString(document.Finish());

        await Assert.That(text).Contains("StructTreeRoot");
        await Assert.That(text).Contains("example.com");
    }

    [Test]
    public async Task ThePackageExposesNoInteropTypes()
    {
        // The ABI must not leak into consumers' code. Anything public here would become part
        // of the package's compatibility surface by accident.
        var leaked = typeof(KrillaDocument).Assembly
            .GetExportedTypes()
            .Where(_ => _.Name.StartsWith("Native", StringComparison.Ordinal) ||
                        _.Name.Contains("Interop", StringComparison.Ordinal))
            .Select(_ => _.FullName)
            .ToList();

        await Assert.That(leaked).IsEmpty();
    }

    [Test]
    public async Task ThePackageIsStrongNamed()
    {
        var name = typeof(KrillaDocument).Assembly.GetName();
        await Assert.That(name.GetPublicKeyToken()).IsNotEmpty();
    }
}
