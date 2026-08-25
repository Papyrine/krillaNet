public class ZDebugTests
{
    [Test]
    [Explicit]
    public async Task Dump()
    {
        const string html = """
            <!doctype html><html><head><style>
            h2 { string-set: title content(); page: chapter; color: red }
            .x { string-set: other "literal" }
            </style></head><body>
            <h2 id="a">Heading</h2>
            <p id="b" class="x">p</p>
            </body></html>
            """;

        var options = CorpusRunner.Options();
        var document = await HtmlConverter.ParseAsync(html, options);
        using var context = Krilla.Html.Styling.DocumentContext.For(document, options);
        var log = new System.Text.StringBuilder();

        foreach (var id in new[] {"a", "b"})
        {
            var element = document.GetElementById(id)!;
            log.AppendLine($"{id}: string-set='{context.Declared(element, "string-set")}' page='{context.Declared(element, "page")}'");
        }

        File.WriteAllText("dbg.txt", log.ToString());
    }
}
