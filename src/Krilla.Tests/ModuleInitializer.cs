public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // Verify.PDFium renders each page to PNG and compares those, so a snapshot asserts
        // what a reader actually draws rather than the exact bytes krilla emitted. Without it
        // a PDF is an opaque blob whose diff tells you nothing.
        //
        // Ahead of InitializePlugins, which would otherwise initialize it at the default dpi
        // and make the explicit call throw. 72 dpi renders one pixel per point, so a
        // snapshot's dimensions match the page size the test asked for.
        if (!VerifyPDFium.Initialized)
        {
            VerifyPDFium.Initialize(dpi: 72);
        }

        VerifierSettings.InitializePlugins();

        // Pixel comparison rather than byte equality. Rasterisation is deterministic for a
        // pinned PDFium version, but SSIM keeps a version bump from invalidating every
        // baseline over sub-pixel antialiasing differences.
        VerifierSettings.UseSsimForPng();
    }
}
