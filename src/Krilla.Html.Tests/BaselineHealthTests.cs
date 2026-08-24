/// <summary>
/// Guards against degenerate corpus baselines.
///
/// The Verify comparison each corpus scenario runs compares its rendered page against its own
/// committed <c>result#page_*.verified.png</c>. Once a broken page is promoted, that comparison is
/// the baseline against itself and stays green forever — a blindness Morph learned the hard way,
/// where four pages that had collapsed to a solid fill passed the suite for weeks because the
/// baseline WAS the broken image.
///
/// The reference comparison is the real antidote, but it has a hole of its own: when our page
/// count differs from the browser's, the per-page metrics are suppressed entirely, so a scenario
/// can have no pixel coverage at all. This catches the crudest failure in that gap.
///
/// A page that rendered nothing is a single flat expanse of background. A page that rendered
/// anything at all has at least one more colour than that.
/// </summary>
public class BaselineHealthTests
{
    /// <summary>
    /// A page baseline with at most this many distinct colours has nothing on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One, and it cannot be raised. Morph's equivalent guard uses sixteen, on the reasoning that a
    /// rendered document page essentially always carries anti-aliased text and so has hundreds of
    /// colours. That reasoning does not survive here: this corpus deliberately contains scenarios
    /// that are nothing but flat fills — <c>block/auto_margins</c> is two solid rectangles on
    /// white, three colours in total — precisely so they carry no rasterisation noise and can be
    /// compared exactly. A threshold above two would fail every one of them.
    /// </para>
    /// <para>
    /// So the guard is narrower here, and knowingly: it catches "the page came out entirely
    /// blank" and nothing subtler. It can afford to be, because this corpus has a stronger check
    /// Morph's did not — every page is also compared against a browser reference, where a page
    /// that lost its content shows up immediately as a collapsed SSIM. This is the backstop for
    /// the one case that comparison cannot cover: a page-count mismatch, which suppresses the
    /// per-page metrics entirely.
    /// </para>
    /// </remarks>
    const int degenerateColorThreshold = 1;

    /// <summary>
    /// Baselines allowed to be blank, keyed by path relative to the corpus root with forward
    /// slashes.
    /// </summary>
    /// <remarks>
    /// Entries are asserted to be STILL blank, so a page that gets fixed and regenerated forces
    /// its own removal from this list rather than rotting here.
    /// </remarks>
    static readonly HashSet<string> knownBlank = [with(StringComparer.OrdinalIgnoreCase)];

    [Test]
    public async Task PagesAreNotBlank()
    {
        var blank = new List<string>();
        var wronglyListed = new List<string>();

        foreach (var directory in CorpusLayout.Directories())
        {
            var pages = Directory.GetFiles(
                directory,
                $"{CorpusLayout.ResultName}#page_*.verified.png");

            foreach (var page in pages.Order())
            {
                var key = $"{CorpusLayout.Name(directory)}/{Path.GetFileName(page)}";
                var isBlank = PageComparison.CountColors(page) <= degenerateColorThreshold;

                if (isBlank && !knownBlank.Contains(key))
                {
                    blank.Add(key);
                }
                else if (!isBlank && knownBlank.Contains(key))
                {
                    wronglyListed.Add(key);
                }
            }
        }

        await Assert.That(blank)
            .IsEmpty()
            .Because("these page baselines have no content on them, and the Verify comparison " +
                     "cannot see that because it compares each one against itself");

        await Assert.That(wronglyListed)
            .IsEmpty()
            .Because("these are listed as known-blank but now render content, so they should be " +
                     "removed from the list");
    }

    /// <summary>
    /// Every scenario has a reference to compare against.
    /// </summary>
    /// <remarks>
    /// A scenario with no reference still runs and still snapshots, but its box and pixel
    /// comparisons are both null — so it contributes nothing to the only measurement the corpus
    /// exists for, while looking like a passing test. Failing here is what stops one being added
    /// and forgotten.
    /// </remarks>
    [Test]
    public async Task ScenariosHaveReferences()
    {
        var missing = CorpusLayout.Directories()
            .Where(_ => CorpusLayout.ReferencePages(_).Length == 0 ||
                        !File.Exists(CorpusLayout.BoxesPath(_)))
            .Select(CorpusLayout.Name)
            .ToList();

        await Assert.That(missing)
            .IsEmpty()
            .Because("run Krilla.Html.RefGen to generate the browser reference for these " +
                     "scenarios; without one they measure nothing");
    }

    /// <summary>
    /// Every element the browser laid out has a box on this side to compare against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counterpart to <see cref="ScenariosHaveReferences"/>, and it closes the same kind of
    /// hole one level down. A scenario with a reference still measures nothing about an element
    /// this engine generates no box for: the comparison counts it as unmatched and carries on
    /// green, so a construct can be added to the corpus, look measured, and be measured by nothing
    /// but pixels.
    /// </para>
    /// <para>
    /// It can be an equality rather than a budget because the count really is zero across the
    /// corpus — inline elements, inline images, inline-blocks and <c>&lt;br&gt;</c> all report
    /// their geometry. Anything that puts an element out of reach again has to be a deliberate
    /// change to this test rather than a number quietly going up.
    /// </para>
    /// </remarks>
    [Test]
    public async Task EveryElementIsMeasured()
    {
        var unmatched = new List<string>();

        foreach (var directory in CorpusLayout.Directories())
        {
            var path = CorpusLayout.BoxesPath(directory);
            if (!File.Exists(path))
            {
                continue;
            }

            var reference = JsonSerializer.Deserialize(
                                await File.ReadAllTextAsync(path),
                                CorpusJson.Default.ListBoxGeometry) ??
                            [];

            var result = BoxComparison.Compare(
                reference,
                BoxDump.Measure(CorpusLayout.Html(directory), CorpusRunner.Options(directory)));

            foreach (var selector in result.MissingFromRender.Concat(result.NotInReference))
            {
                unmatched.Add($"{CorpusLayout.Name(directory)}: {selector}");
            }
        }

        await Assert.That(unmatched)
            .IsEmpty()
            .Because("these elements are laid out by the browser and have no box here, so nothing " +
                     "compares their geometry");
    }
}
