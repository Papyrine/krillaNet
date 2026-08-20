public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // Snapshots the result object as strict JSON in a .verified.json rather than Verify's
        // relaxed .verified.txt dialect. Required, not cosmetic: CorpusMarkdownGenerator reads
        // those numbers back to build compare.md, and the relaxed dialect is not JSON any parser
        // will accept.
        VerifierSettings.UseStrictJson();

        VerifierSettings.InitializePlugins();

        // Pixel comparison rather than byte equality for the page baselines. Rasterisation is
        // deterministic for a pinned PDFium version, but SSIM keeps a version bump from
        // invalidating every baseline over sub-pixel anti-aliasing differences.
        //
        // No VerifyPDFium here, unlike Krilla.Tests. That plugin renders a verified PDF to PNG
        // itself, at a DPI fixed once for the process; the corpus needs the rendering under its
        // own control so it can pin the DPI to 96 and hand the same bytes to both the snapshot and
        // the reference comparison. See CorpusRunner.RenderPages.
        VerifierSettings.UseSsimForPng();

        // The corpus regenerates compare.md as a side effect of each scenario, and the aggregate
        // can only be written once every scenario in the run has produced its numbers.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            CorpusMarkdownGenerator.RegenerateAll();
    }
}
