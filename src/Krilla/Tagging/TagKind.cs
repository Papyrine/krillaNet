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

    /// <summary>A largely self-contained article.</summary>
    Article = 1,

    /// <summary>A section.</summary>
    Section = 2,

    /// <summary>A generic grouping with no stronger meaning.</summary>
    Div = 3,

    /// <summary>A paragraph-level quotation.</summary>
    BlockQuote = 4,

    /// <summary>A caption for a figure or table.</summary>
    Caption = 5,

    /// <summary>A table of contents.</summary>
    TableOfContents = 6,

    /// <summary>An entry in a table of contents.</summary>
    TableOfContentsItem = 7,

    /// <summary>An index.</summary>
    Index = 8,

    /// <summary>A paragraph.</summary>
    Paragraph = 9,

    /// <summary>A list item.</summary>
    ListItem = 10,

    /// <summary>A list item's label — its bullet or number.</summary>
    ListLabel = 11,

    /// <summary>A list item's body.</summary>
    ListBody = 12,

    /// <summary>A table.</summary>
    Table = 13,

    /// <summary>A table row.</summary>
    TableRow = 14,

    /// <summary>A table data cell.</summary>
    TableCell = 15,

    /// <summary>A table header row group.</summary>
    TableHead = 16,

    /// <summary>A table body row group.</summary>
    TableBody = 17,

    /// <summary>A table footer row group.</summary>
    TableFoot = 18,

    /// <summary>An inline span of text.</summary>
    Span = 19,

    /// <summary>An inline quotation.</summary>
    InlineQuote = 20,

    /// <summary>A footnote or endnote.</summary>
    Note = 21,

    /// <summary>A reference to elsewhere in the document.</summary>
    Reference = 22,

    /// <summary>A bibliography entry.</summary>
    BibliographyEntry = 23,

    /// <summary>A fragment of computer code.</summary>
    Code = 24,

    /// <summary>A hyperlink.</summary>
    Link = 25,

    /// <summary>An annotation.</summary>
    Annotation = 26,

    /// <summary>A form field.</summary>
    Form = 27,

    /// <summary>Content with no structural role of its own.</summary>
    NonStructural = 28,

    /// <summary>A date or time.</summary>
    DateTime = 29,

    /// <summary>A list of terms and definitions.</summary>
    Terms = 30,

    /// <summary>A title.</summary>
    Title = 31,

    /// <summary>Strongly emphasised text.</summary>
    Strong = 32,

    /// <summary>Emphasised text.</summary>
    Emphasis = 33
}