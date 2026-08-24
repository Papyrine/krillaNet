/// <summary>
/// The bijective base-26 marker text — <c>a</c> to <c>z</c>, then <c>aa</c> — measured against the
/// <see cref="StringBuilder"/> it replaced.
/// </summary>
/// <remarks>
/// <para>
/// The baseline is the implementation this repository shipped before the rewrite. It lives here
/// rather than in the engine because a comparison against deleted code is the only way to say what
/// the rewrite bought, and there is nowhere else for it to live.
/// </para>
/// <para>
/// The digits arrive least significant first, which is what the builder's <c>Insert(0, …)</c> was
/// paying for: every character memmoved the whole buffer. Writing backwards into a stack buffer
/// gets the same order for nothing, so the two allocations — the builder's chunk and the string it
/// copies out — become one.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class AlphabeticMarkers
{
    /// <summary>
    /// One, two and three digits: <c>d</c>, <c>aa</c> and <c>aaa</c>.
    /// </summary>
    /// <remarks>
    /// The digit count is the whole of what varies here, since the cost the rewrite removed grows
    /// with it — a one-digit marker never shuffles the buffer at all, so the shortest is the case
    /// that flatters the baseline most.
    /// </remarks>
    [Params(4, 27, 703)]
    public int Ordinal { get; set; }

    /// <summary>
    /// Fails the run if the two implementations disagree, before either is timed.
    /// </summary>
    /// <remarks>
    /// Two implementations that produce different strings cannot be compared, and a benchmark is
    /// not a place that notices on its own.
    /// </remarks>
    [GlobalSetup]
    public void Verify()
    {
        var expected = Builder(Ordinal, 'a');
        var actual = ListMarkers.Counter(ListStyleKind.LowerAlpha, Ordinal);

        if (expected != actual)
        {
            throw new($"Implementations disagree at {Ordinal}: {expected} against {actual}.");
        }
    }

    [Benchmark(Baseline = true)]
    public string StringBuilder() => Builder(Ordinal, 'a');

    [Benchmark]
    public string StackBuffer() => ListMarkers.Counter(ListStyleKind.LowerAlpha, Ordinal);

    /// <summary>The pre-rewrite implementation, verbatim.</summary>
    static string Builder(int ordinal, char first)
    {
        var builder = new StringBuilder();

        for (var value = ordinal; value > 0; value = (value - 1) / 26)
        {
            builder.Insert(0, (char) (first + (value - 1) % 26));
        }

        return builder.ToString();
    }
}
