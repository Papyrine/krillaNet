namespace Krilla.Web.Tests.Services;

/// <summary>
/// The faces <see cref="FontStore"/> asks for have to exist where the SDK will serve them from.
/// </summary>
/// <remarks>
/// Written after they did not. The faces were linked in from another project, which reaches the
/// publish output and never becomes a static web asset — so `dotnet run` answered every font
/// request with an empty 200 and the first conversion failed with "The data is too short to be a
/// font". Nothing caught it: the Playwright tests serve the PUBLISHED output, where the files are
/// present, and the service tests read them off disk.
///
/// A file physically under wwwroot is what the SDK discovers, in every mode, so that is the
/// property asserted. It is a structural check rather than a behavioural one, which is the trade
/// for not standing up a dev server in a test.
/// </remarks>
public class FontAssetTests
{
    static string WwwrootFonts =>
        Path.Combine(AttributeReader.GetProjectDirectory(), "..", "Krilla.Web", "wwwroot", "fonts");

    [Test]
    [MethodDataSource(nameof(Faces))]
    public async Task FaceIsAStaticWebAsset(string face)
    {
        var path = Path.Combine(WwwrootFonts, face);

        await Assert.That(File.Exists(path)).IsTrue();
        // Non-empty as well as present: an empty file is exactly what the broken linking produced
        // over the wire, so "it exists" alone would not have caught it.
        await Assert.That(new FileInfo(path).Length).IsGreaterThan(1000);
    }

    // Drawn from FontStore itself rather than restated, so adding a face there without shipping it
    // fails here instead of at the first conversion.
    public static IEnumerable<Func<string>> Faces()
    {
        foreach (var face in FontStore.Faces)
        {
            yield return () => face;
        }
    }

    [Test]
    // The Liberation faces are OFL, which requires the licence to travel with them.
    public async Task LicenceShipsWithTheFaces() =>
        await Assert.That(File.Exists(Path.Combine(WwwrootFonts, "OFL.txt"))).IsTrue();
}
