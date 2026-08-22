/// <summary>
/// The file mutations the generator makes inside the working tree, retried past a transient
/// sharing violation.
/// </summary>
/// <remarks>
/// <para>
/// A reference page lives in a source directory, so anything that scans one can be holding a
/// handle to it at the moment the generator wants to replace it: the search indexer, a virus
/// scanner, Explorer building a thumbnail, a git tool, an image viewer left open on the file being
/// regenerated. On Windows that is an <see cref="IOException"/> rather than a wait, and it is
/// intermittent by nature — the run that failed on <c>block/auto_width</c> succeeds on the next
/// attempt a moment later, several scenarios further on.
/// </para>
/// <para>
/// It cannot be swallowed the way <c>CorpusMarkdownGenerator</c> swallows its contended write.
/// That file is rewritten by a later reconciliation pass, and a reference is not rewritten by
/// anything at all — losing one leaves a scenario measuring against a stale render. So it is
/// retried instead, and only reported once a second of retrying has failed, by which point the
/// file is genuinely held open and the name of it is what a person needs.
/// </para>
/// </remarks>
static class RetryingFile
{
    /// <remarks>
    /// A scanner holds a file for a few tens of milliseconds, so the first retry almost always
    /// settles it. The remainder are there for the case where something opened the file to display
    /// it and is about to let go.
    /// </remarks>
    const int attempts = 10;

    const int delayMilliseconds = 100;

    public static Task DeleteAsync(string path) =>
        Run(path, () => File.Delete(path));

    public static Task WriteAllTextAsync(string path, string contents) =>
        Run(path, () => File.WriteAllText(path, contents));

    public static Task WriteAllBytesAsync(string path, byte[] bytes) =>
        Run(path, () => File.WriteAllBytes(path, bytes));

    static async Task Run(string path, Action action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception exception)
                // DirectoryNotFoundException is an IOException and is not contention: no amount of
                // waiting creates the directory, so it goes straight out.
                when (exception is
                          UnauthorizedAccessException or
                          IOException and
                          not DirectoryNotFoundException)
            {
                if (attempt == attempts)
                {
                    throw new IOException(
                        $"'{path}' is held open by another process. Gave up after {attempts} " +
                        "attempts. Close anything displaying the corpus references and run again.",
                        exception);
                }

                await Task.Delay(delayMilliseconds);
            }
        }
    }
}
