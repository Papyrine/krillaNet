/// <summary>
/// A float, and where in its container's flow it was declared.
/// </summary>
/// <param name="Box">The floated box.</param>
/// <param name="Index">
/// The number of in-flow children that precede it. A float is placed at the flow position it was
/// declared at rather than at the top of its container, so a float written after two paragraphs
/// starts below them.
/// </param>
readonly record struct FloatChild(LayoutBox Box, int Index);