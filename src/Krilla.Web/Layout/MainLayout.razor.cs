namespace Krilla.Web.Layout;

public partial class MainLayout : IDisposable
{
    ThemeType currentTheme = ThemeType.Light;
    DownloadSize? downloadSize;
    long? liveBytes;
    long? peakBytes;
    PeriodicTimer? ramPoll;

    // Read off the Krilla assembly rather than this app's, because the version a reader cares
    // about is the engine's — the shell around it has no version of its own worth showing.
    internal static string Version { get; } =
        typeof(KrillaDocument).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    protected override async Task OnInitializedAsync()
    {
        currentTheme = await ThemePreferenceService.GetSavedThemeAsync();
        await JSRuntime.InvokeVoidAsync("themeManager.applyTheme", currentTheme.ToString());
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // appInfo.downloadSize waits for the load event before totalling the boot download, so
        // resolve it off the first render rather than during init — otherwise the initial paint
        // waits behind it. It is the fixed boot payload, so unlike RAM it is sampled once.
        downloadSize = await JSRuntime.InvokeAsync<DownloadSize>("appInfo.downloadSize");

        await SampleRamAsync();
        StateHasChanged();

        // A conversion allocates a whole document tree and the PDF bytes, and the WebAssembly
        // arena the managed heap lives in grows to fit each peak and never shrinks back — so one
        // boot-time figure says little. Poll both, and repaint only when a figure moves, so the
        // poll costs nothing while the page sits idle.
        var poll = new PeriodicTimer(TimeSpan.FromSeconds(2));
        ramPoll = poll;
        _ = PollRamAsync(poll);
    }

    async Task PollRamAsync(PeriodicTimer timer)
    {
        try
        {
            while (await timer.WaitForNextTickAsync())
            {
                // Hop back onto the renderer's dispatcher: the tick resumes on a pool thread, but
                // JS interop and StateHasChanged must run on the UI thread.
                await InvokeAsync(async () =>
                {
                    var previous = (liveBytes, peakBytes);
                    await SampleRamAsync();
                    if ((liveBytes, peakBytes) != previous)
                    {
                        StateHasChanged();
                    }
                });
            }
        }
        catch (ObjectDisposedException)
        {
            // Disposed mid-poll (page torn down); stop quietly.
        }
    }

    async Task SampleRamAsync()
    {
        liveBytes = GC.GetTotalMemory(false);

        // Committed WebAssembly linear memory: the whole runtime's arena, managed heap and the
        // native krilla allocations alike. WASM memory only grows, so this is already a
        // high-water mark; take the max anyway to stay honest on the JS-heap fallback path.
        var sample = await JSRuntime.InvokeAsync<long>("appInfo.ramBytes");
        if (sample > 0)
        {
            peakBytes = peakBytes is { } previousPeak ? Math.Max(previousPeak, sample) : sample;
        }
    }

    static string FormatMb(long bytes) =>
        $"{bytes / (1024d * 1024d):0.0} MB";

    readonly record struct DownloadSize(long Zipped, long Unzipped);

    async Task HandleThemeChanged(ThemeType newTheme)
    {
        currentTheme = newTheme;
        await ThemePreferenceService.SaveThemeAsync(newTheme);
        await JSRuntime.InvokeVoidAsync("themeManager.applyTheme", newTheme.ToString());
    }

    public void Dispose() =>
        ramPoll?.Dispose();
}
