/// <summary>
/// The CSS counters in scope while the box tree is built.
/// </summary>
/// <remarks>
/// <para>
/// Held by the box builder and mutated as it walks, for the same reason <see cref="ListNumbering"/>
/// is: a counter's value depends on which elements were VISITED before it, which is a property of
/// the walk rather than of the element. Deriving it from the DOM afterwards would have to redo the
/// walk, and would have to redo it identically or the numbers would disagree with the boxes.
/// </para>
/// <para>
/// Scoping is the part worth stating. A <c>counter-reset</c> creates a NEW counter that lives until
/// the declaring element's subtree ends, nested inside any counter of the same name outside it — so
/// <c>counter()</c> reads the innermost and <c>counters()</c> reads all of them, which is the whole
/// mechanism behind a numbered outline. The stack per name is what carries that, and popping it when
/// the subtree ends is what keeps a second list from continuing the first one's numbering.
/// </para>
/// <para>
/// What this does not model is the specification's rule that a sibling's reset REPLACES rather than
/// nests when the two are at the same depth. The difference shows on a document that resets the same
/// counter twice at one level and then reads it with <c>counters()</c>, where this produces one
/// extra level. Nesting by subtree is what the common structures — a list of lists, a document of
/// sections — actually want, and it is the reading that makes an outline number correctly.
/// </para>
/// </remarks>
sealed class CssCounters
{
    readonly Dictionary<string, List<int>> scopes = new(StringComparer.Ordinal);

    /// <summary>
    /// Applies an element's <c>counter-reset</c>, <c>counter-increment</c> and <c>counter-set</c>,
    /// in that order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is CSS's own and it is observable: <c>counter-reset: n 0; counter-increment: n 1</c>
    /// on one element gives 1, where the other order gives 0. An element resetting and incrementing
    /// the same counter is how a numbered heading restarts its own subsection numbering, and one
    /// that also sets it ends on the value it set rather than on the one it counted to.
    /// </para>
    /// <para>
    /// <c>counter-set</c> differs from <c>counter-reset</c> in the half that is not the value: it
    /// creates no SCOPE. Setting a counter that is already in scope changes the one that is there,
    /// where resetting it would nest a second inside it — so a document that sets a counter and then
    /// reads it with <c>counters()</c> gets one level rather than two. That is the whole of the
    /// difference, and it is why the two do not share a branch here.
    /// </para>
    /// <para>
    /// Returns the names it pushed, so the caller can pop exactly those when the subtree ends.
    /// </para>
    /// </remarks>
    public List<string>? Enter(ComputedStyle style)
    {
        List<string>? pushed = null;

        foreach (var (name, value) in style.CounterReset)
        {
            if (!scopes.TryGetValue(name, out var stack))
            {
                stack = [];
                scopes[name] = stack;
            }

            stack.Add(value);
            pushed ??= [];
            pushed.Add(name);
        }

        foreach (var (name, value) in style.CounterIncrement)
        {
            if (!scopes.TryGetValue(name, out var stack) || stack.Count == 0)
            {
                // A counter incremented without ever being reset behaves as though the root had
                // reset it to zero, which CSS requires and which is what makes
                // `li { counter-increment: item }` work with no reset anywhere.
                stack = [0];
                scopes[name] = stack;
            }

            stack[^1] += value;
        }

        foreach (var (name, value) in style.CounterSet)
        {
            if (!scopes.TryGetValue(name, out var stack) || stack.Count == 0)
            {
                // A counter set without ever being reset is created here, exactly as an
                // incremented one is — and, like it, without a scope to pop, so it lasts the rest
                // of the document rather than the rest of the subtree.
                stack = [0];
                scopes[name] = stack;
            }

            stack[^1] = value;
        }

        return pushed;
    }

    /// <summary>Pops the scopes an element's <c>counter-reset</c> created.</summary>
    public void Leave(List<string>? pushed)
    {
        if (pushed is null)
        {
            return;
        }

        foreach (var name in pushed)
        {
            if (scopes.TryGetValue(name, out var stack) && stack.Count > 0)
            {
                stack.RemoveAt(stack.Count - 1);
            }
        }
    }

    /// <summary>The innermost value of one counter, which is zero when it has none.</summary>
    public int Value(string name)
    {
        if (scopes.TryGetValue(name, out var stack) && stack.Count > 0)
        {
            return stack[^1];
        }

        return 0;
    }

    /// <summary>Every value of one counter, outermost first.</summary>
    /// <remarks>
    /// Empty when the counter does not exist, which is what makes <c>counters()</c> on an
    /// unreferenced name produce nothing rather than a bare separator.
    /// </remarks>
    public IReadOnlyList<int> Values(string name)
    {
        if (scopes.TryGetValue(name, out var stack))
        {
            return stack;
        }

        return (List<int>) [];
    }
}
