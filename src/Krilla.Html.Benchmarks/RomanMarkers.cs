/// <summary>
/// The Roman numeral marker text, measured against the <see cref="StringBuilder"/> it replaced.
/// </summary>
/// <remarks>
/// <para>
/// The baseline is the pre-rewrite implementation, kept here for the same reason
/// <see cref="AlphabeticMarkers"/> keeps its own.
/// </para>
/// <para>
/// Nothing here shuffled a buffer — the numeral is built front to back — so this measures only
/// what the builder itself costs, which is the more conservative of the two comparisons and the
/// one worth having.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class RomanMarkers
{
    /// <summary>
    /// <c>III</c>, <c>XXXVIII</c>, and <c>MMMDCCCLXXXVIII</c> — the longest numeral the style can
    /// express, which is the value the engine's fifteen-character buffer is sized for.
    /// </summary>
    [Params(3, 38, 3888)]
    public int Ordinal { get; set; }

    /// <inheritdoc cref="AlphabeticMarkers.Verify"/>
    [GlobalSetup]
    public void Verify()
    {
        var expected = Builder(Ordinal);
        var actual = ListMarkers.Counter(ListStyleKind.UpperRoman, Ordinal);

        if (expected != actual)
        {
            throw new($"Implementations disagree at {Ordinal}: {expected} against {actual}.");
        }
    }

    [Benchmark(Baseline = true)]
    public string StringBuilder() => Builder(Ordinal);

    /// <remarks>
    /// <c>UpperRoman</c> rather than <c>LowerRoman</c>, which case-folds the result afterwards and
    /// so allocates a second string neither implementation is being measured for.
    /// </remarks>
    [Benchmark]
    public string StackBuffer() => ListMarkers.Counter(ListStyleKind.UpperRoman, Ordinal);

    /// <summary>The pre-rewrite implementation, verbatim.</summary>
    static string Builder(int ordinal)
    {
        ReadOnlySpan<int> values = [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];
        ReadOnlySpan<string> numerals =
            ["M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I"];

        var builder = new StringBuilder();
        var remaining = ordinal;

        for (var index = 0; index < values.Length; index++)
        {
            while (remaining >= values[index])
            {
                builder.Append(numerals[index]);
                remaining -= values[index];
            }
        }

        return builder.ToString();
    }
}
