using TUnit.Assertions.Enums;

/// <summary>
/// Finding a face by family and style, against the directory <see cref="TestFonts"/> builds.
/// </summary>
public class FontRepositoryTests
{
    static readonly FontRepository repository = TestFonts.Repository();

    static string Name(FontFaceInfo? face)
    {
        if (face == null)
        {
            return "<none>";
        }

        var file = Path.GetFileName(face.Path);
        if (file.EndsWith(".ttc"))
        {
            return $"{file}#{face.Index}";
        }

        return file;
    }

    [Test]
    [Arguments(false, false, "LiberationSans-Regular.ttf")]
    [Arguments(true, false, "LiberationSans-Bold.ttf")]
    [Arguments(false, true, "LiberationSans-Italic.ttf")]
    [Arguments(true, true, "LiberationSans-BoldItalic.ttf")]
    public async Task FindsEachStyleOfAFamily(bool bold, bool italic, string expected)
    {
        var face = repository.FindFace("Liberation Sans", bold, italic);

        await Assert.That(Name(face)).IsEqualTo(expected);
        await Assert.That(face!.Bold).IsEqualTo(bold);
        await Assert.That(face.Italic).IsEqualTo(italic);
    }

    [Test]
    public async Task ReadsNamesAndWeight()
    {
        var face = repository.FindFace("Liberation Sans", bold: true, italic: true)!;

        await Assert.That(face.Family).IsEqualTo("Liberation Sans");
        await Assert.That(face.Subfamily).IsEqualTo("Bold Italic");
        await Assert.That(face.FullName).IsEqualTo("Liberation Sans Bold Italic");
        await Assert.That(face.PostScriptName).IsEqualTo("LiberationSans-BoldItalic");
        await Assert.That(face.Weight).IsEqualTo(700);
        await Assert.That(face.FamilyNames[0]).IsEqualTo("Liberation Sans");
    }

    [Test]
    public async Task ReadsEveryFaceInACollection()
    {
        var faces = repository.Faces
            .Where(_ => _.Path.EndsWith(".ttc"))
            .Select(_ => $"{_.Index} {_.Family} {_.Subfamily}")
            .ToList();

        await Assert.That(faces).IsEquivalentTo(
            [
                "0 Liberation Mono Regular",
                "1 Liberation Mono Bold",
                "2 Liberation Mono Italic"
            ], CollectionOrdering.Matching);
    }

    [Test]
    public async Task FindsAndLoadsAFaceInsideACollection()
    {
        var face = repository.FindFace("Liberation Mono", bold: true);
        await Assert.That(Name(face)).IsEqualTo("LiberationMono.ttc#1");

        using var font = repository.Find("Liberation Mono", bold: true);
        await Assert.That(font).IsNotNull();
        await Assert.That(font!.UnitsPerEm).IsEqualTo(2048f);
        // A monospace face: every advance is the same, whatever the letter.
        await Assert.That(font.MeasureText("iii", 10)).IsEqualTo(font.MeasureText("WWW", 10));
    }

    [Test]
    public async Task RecursesIntoSubdirectories()
    {
        var face = repository.FindFace("Liberation Serif", bold: true);

        await Assert.That(Name(face)).IsEqualTo("LiberationSerif-Bold.ttf");
        await Assert.That(Path.GetFileName(Path.GetDirectoryName(face!.Path))).IsEqualTo("serif");
    }

    [Test]
    public async Task SkipsFilesThatAreNotFonts()
    {
        // Four Sans, two Serif, three in the collection; the impostor contributes nothing.
        await Assert.That(repository.Faces.Count).IsEqualTo(9);
        await Assert.That(repository.Faces.Any(_ => _.Path.EndsWith("not-a-font.ttf"))).IsFalse();
    }

    [Test]
    public async Task MatchingIgnoresCaseAndSurroundingSpace()
    {
        var face = repository.FindFace("  liberation SANS ", bold: true);

        await Assert.That(Name(face)).IsEqualTo("LiberationSans-Bold.ttf");
    }

    [Test]
    [Arguments("Helvetica", false, false, "LiberationSans-Regular.ttf")]
    [Arguments("Helvetica-Bold", false, false, "LiberationSans-Bold.ttf")]
    [Arguments("Helvetica-Oblique", false, false, "LiberationSans-Italic.ttf")]
    [Arguments("Helvetica-BoldOblique", false, false, "LiberationSans-BoldItalic.ttf")]
    [Arguments("Helvetica", true, false, "LiberationSans-Bold.ttf")]
    [Arguments("Times", false, false, "LiberationSerif-Regular.ttf")]
    [Arguments("Times-Roman", false, false, "LiberationSerif-Regular.ttf")]
    [Arguments("Times-Bold", false, false, "LiberationSerif-Bold.ttf")]
    [Arguments("Courier", false, false, "LiberationMono.ttc#0")]
    [Arguments("Courier-Bold", false, false, "LiberationMono.ttc#1")]
    [Arguments("Courier-Oblique", false, false, "LiberationMono.ttc#2")]
    [Arguments("Arial", true, false, "LiberationSans-Bold.ttf")]
    [Arguments("Times New Roman", false, false, "LiberationSerif-Regular.ttf")]
    [Arguments("Courier New", false, true, "LiberationMono.ttc#2")]
    public async Task StandardFourteenNamesResolveThroughTheirSubstitutes(string family, bool bold, bool italic, string expected)
    {
        // This directory has no Arial, Times New Roman or Courier New, so each of these lands on
        // the metric-compatible Liberation face — which is what a Linux machine gets too.
        var face = repository.FindFace(family, bold, italic);

        await Assert.That(Name(face)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Helvetica", "Arial")]
    [Arguments("Helvetica-BoldOblique", "Arial")]
    [Arguments("Times-Roman", "Times New Roman")]
    [Arguments("Times", "Times New Roman")]
    [Arguments("Courier", "Courier New")]
    [Arguments("Arial", "Arial")]
    [Arguments("Verdana", "Verdana")]
    public async Task StandardFourteenNamesMapToTheFacesReadersSubstitute(string name, string first)
    {
        var (families, _, _) = FontAliases.Resolve(name);

        await Assert.That(families[0]).IsEqualTo(first);
    }

    [Test]
    public async Task StandardFourteenNamesImplyTheirStyle()
    {
        var (_, bold, italic) = FontAliases.Resolve("helvetica-boldoblique");

        await Assert.That(bold).IsTrue();
        await Assert.That(italic).IsTrue();
    }

    [Test]
    [Arguments("Liberation Sans Bold Italic", "LiberationSans-BoldItalic.ttf")]
    [Arguments("LiberationSerif-Bold", "LiberationSerif-Bold.ttf")]
    [Arguments("LiberationMono-Italic", "LiberationMono.ttc#2")]
    public async Task FullAndPostScriptNamesNameOneFace(string name, string expected)
    {
        // Asked for regular, and given the face the name names regardless.
        var face = repository.FindFace(name);

        await Assert.That(Name(face)).IsEqualTo(expected);
    }

    [Test]
    public async Task MissingStyleSettlesForTheClosest()
    {
        // The collection has no bold italic. Slope outranks weight, so italic wins over bold.
        var mono = repository.FindFace("Liberation Mono", bold: true, italic: true);
        await Assert.That(Name(mono)).IsEqualTo("LiberationMono.ttc#2");

        // Serif has no italic at all, so the regular face, and it says so.
        var serif = repository.FindFace("Liberation Serif", italic: true);
        await Assert.That(Name(serif)).IsEqualTo("LiberationSerif-Regular.ttf");
        await Assert.That(serif!.Italic).IsFalse();
    }

    [Test]
    public async Task UnknownFamilyReturnsNull()
    {
        await Assert.That(repository.FindFace("No Such Family")).IsNull();
        await Assert.That(repository.Find("No Such Family")).IsNull();
    }

    [Test]
    public async Task FallbackFamilyKeepsTheRequestedStyle()
    {
        var withFallback = new FontRepository(TestFonts.Directory)
        {
            FallbackFamily = "Liberation Serif"
        };

        var face = withFallback.FindFace("No Such Family", bold: true);

        await Assert.That(Name(face)).IsEqualTo("LiberationSerif-Bold.ttf");
    }

    [Test]
    public async Task FallbackFamilyIsAliasedLikeAnyOther()
    {
        var withFallback = new FontRepository(TestFonts.Directory)
        {
            FallbackFamily = "Helvetica"
        };

        var face = withFallback.FindFace("No Such Family", italic: true);

        await Assert.That(Name(face)).IsEqualTo("LiberationSans-Italic.ttf");
    }

    [Test]
    public async Task FallbackIsNotConsultedWhenTheFamilyMatches()
    {
        var withFallback = new FontRepository(TestFonts.Directory)
        {
            FallbackFamily = "Liberation Serif"
        };

        var face = withFallback.FindFace("Liberation Sans");

        await Assert.That(Name(face)).IsEqualTo("LiberationSans-Regular.ttf");
    }

    [Test]
    public async Task MissingDirectoryIsSkipped()
    {
        var empty = new FontRepository(Path.Combine(TestFonts.Directory, "does-not-exist"));

        await Assert.That(empty.Faces.Count).IsEqualTo(0);
        await Assert.That(empty.FindFace("Liberation Sans")).IsNull();
    }

    [Test]
    public async Task DirectoriesAreCombined()
    {
        // The same directory twice, and a subdirectory of it: each file is scanned once.
        var combined = new FontRepository(
            TestFonts.Directory,
            TestFonts.Directory,
            Path.Combine(TestFonts.Directory, "serif"));

        await Assert.That(combined.Faces.Count).IsEqualTo(9);
    }

    [Test]
    public async Task ScanIsSharedAcrossThreadsAndDiscardedByRefresh()
    {
        var fresh = TestFonts.Repository();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => Task.Run(() => fresh.Faces)));

        await Assert.That(results.All(_ => ReferenceEquals(_, results[0]))).IsTrue();

        fresh.Refresh();

        await Assert.That(ReferenceEquals(fresh.Faces, results[0])).IsFalse();
        await Assert.That(fresh.Faces.Count).IsEqualTo(9);
    }

    [Test]
    [NotInParallel(nameof(FontRepository.Default))]
    public async Task FontFindSearchesTheDefaultRepository()
    {
        var previous = FontRepository.Default;
        FontRepository.Default = repository;
        try
        {
            using var font = Font.Find("Helvetica", bold: true);
            await Assert.That(font).IsNotNull();
        }
        finally
        {
            FontRepository.Default = previous;
        }
    }

    [Test]
    public async Task SystemDirectoriesExist()
    {
        foreach (var directory in FontRepository.SystemDirectories)
        {
            await Assert.That(Directory.Exists(directory)).IsTrue();
        }

        // Every Windows install has Arial, so there at least the scan can be held to finding it.
        if (OperatingSystem.IsWindows())
        {
            var face = FontRepository.System.FindFace("Helvetica", bold: true);
            await Assert.That(face).IsNotNull();
            await Assert.That(face!.Family).IsEqualTo("Arial");
            await Assert.That(face.Bold).IsTrue();
        }
    }
}
