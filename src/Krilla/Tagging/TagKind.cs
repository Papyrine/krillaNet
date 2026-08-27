namespace Krilla;

/// <summary>
/// The structural role of a tagged element.
/// </summary>
/// <remarks>
/// Headings, lists, table headers, figures and formulas carry a value krilla only accepts at
/// construction, so they are created through <see cref="Tag.Heading"/>,
/// <see cref="Tag.List"/>, <see cref="Tag.TableHeader"/>, <see cref="Tag.Figure"/> and
/// <see cref="Tag.Formula"/> rather than named here.
/// </remarks>
public enum TagKind
{
    /// <summary>A part of a document containing multiple articles or sections.</summary>
    Part = 0,
    Article = 1,
    Section = 2,
    Div = 3,
    BlockQuote = 4,
    Caption = 5,
    TableOfContents = 6,
    TableOfContentsItem = 7,
    Index = 8,
    Paragraph = 9,
    ListItem = 10,
    ListLabel = 11,
    ListBody = 12,
    Table = 13,
    TableRow = 14,
    TableCell = 15,
    TableHead = 16,
    TableBody = 17,
    TableFoot = 18,
    Span = 19,
    InlineQuote = 20,
    Note = 21,
    Reference = 22,
    BibliographyEntry = 23,
    Code = 24,
    Link = 25,
    Annotation = 26,
    Form = 27,
    NonStructural = 28,
    DateTime = 29,
    Terms = 30,
    Title = 31,
    Strong = 32,
    Emphasis = 33
}