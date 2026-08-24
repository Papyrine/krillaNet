/// <summary>
/// Positioned box geometry, checked against measurements taken out of Chrome.
/// </summary>
/// <remarks>
/// The same arrangement as <see cref="FloatGeometryTests"/> and for the same reason: these are the
/// cases the rules were derived from, kept in a form that runs without a browser and names the one
/// that broke. Four of them settle questions with more than one defensible answer — whether the
/// containing block is the padding box or the content box, whether a static ancestor in between
/// contributes anything, what an auto offset falls back to, and which dimension a percentage
/// offset resolves against.
/// </remarks>
public class PositionGeometryTests
{
    static readonly (string Name, string Body, (string Id, float[] Box)[] Expected)[] cases =
    [
        ("p1 relative moves the box and nothing else",
            "<div id=\"a\" style=\"height:20px\"></div>" +
            "<div id=\"b\" style=\"height:20px;position:relative;top:10px;left:30px\"></div>" +
            "<div id=\"c\" style=\"height:20px\"></div>",
            [("a", [0, 0, 400, 20]), ("b", [30, 30, 400, 20]), ("c", [0, 40, 400, 20])]),

        ("p2 absolute with auto offsets takes the static position",
            "<div id=\"a\" style=\"height:20px\"></div>" +
            "<div id=\"b\" style=\"position:absolute\">abs auto</div>" +
            "<div id=\"c\" style=\"height:20px\"></div>",
            [("a", [0, 0, 400, 20]), ("b", [0, 20, 61.39f, 18]), ("c", [0, 20, 400, 20])]),

        ("p3 containing block is the padding box",
            "<div id=\"rel\" style=\"position:relative;margin-left:40px;width:200px;height:80px;padding:10px;border:5px solid #000\">" +
            "<div id=\"abs\" style=\"position:absolute;top:0;left:0;width:30px;height:30px\"></div>" +
            "<div id=\"br\" style=\"position:absolute;bottom:0;right:0;width:30px;height:30px\"></div></div>",
            [("rel", [40, 0, 230, 110]), ("abs", [45, 5, 30, 30]), ("br", [235, 75, 30, 30])]),

        ("p4 absolute with auto width shrinks to fit",
            "<div id=\"rel\" style=\"position:relative;width:300px;height:60px\">" +
            "<div id=\"abs\" style=\"position:absolute;top:0;left:0\">shrink to fit this</div></div>",
            [("rel", [0, 0, 300, 60]), ("abs", [0, 0, 106.7f, 18])]),

        ("p5 left and right together stretch an auto width",
            "<div id=\"rel\" style=\"position:relative;width:300px;height:40px\">" +
            "<div id=\"abs\" style=\"position:absolute;left:20px;right:50px;height:20px\"></div></div>",
            [("rel", [0, 0, 300, 40]), ("abs", [20, 0, 230, 20])]),

        ("p6 no positioned ancestor uses the page",
            "<div id=\"abs\" style=\"position:absolute;top:5px;left:7px;width:20px;height:20px\"></div>" +
            "<div id=\"flow\" style=\"height:20px\"></div>",
            [("abs", [7, 5, 20, 20]), ("flow", [0, 0, 400, 20])]),

        ("p7 an absolute box adds nothing to its parent height",
            "<div id=\"par\" style=\"position:relative;width:200px\">" +
            "<div id=\"abs\" style=\"position:absolute;top:0;left:0;width:20px;height:200px\"></div>" +
            "<p id=\"p\">one line</p></div>",
            [("par", [0, 0, 200, 18]), ("abs", [0, 0, 20, 200]), ("p", [0, 0, 200, 18])]),

        ("p8 relative bottom and right move the other way",
            "<div id=\"a\" style=\"height:20px;position:relative;bottom:5px;right:10px\"></div>",
            [("a", [-10, -5, 400, 20])]),

        ("p9 percentage offsets resolve per axis",
            "<div id=\"rel\" style=\"position:relative;width:200px;height:100px\">" +
            "<div id=\"abs\" style=\"position:absolute;top:25%;left:50%;width:10px;height:10px\"></div></div>",
            [("rel", [0, 0, 200, 100]), ("abs", [100, 25, 10, 10])]),

        ("p10 a static ancestor in between is skipped",
            "<div id=\"rel\" style=\"position:relative;width:300px;height:80px;padding:10px\">" +
            "<div id=\"mid\" style=\"margin:20px\">" +
            "<div id=\"abs\" style=\"position:absolute;top:0;left:0;width:20px;height:20px\"></div></div></div>",
            [("rel", [0, 0, 320, 100]), ("abs", [0, 0, 20, 20])])
    ];

    [Test]
    public async Task MatchesChrome()
    {
        var builder = new StringBuilder();
        var failures = 0;

        foreach (var (name, body, expected) in cases)
        {
            var html =
                "<!doctype html><html><head><style>" +
                "*{margin:0;padding:0;border:none;box-sizing:content-box}" +
                "html,body,div,p{margin:0;padding:0;border:none}" +
                "html{font-family:\"Liberation Sans\";font-size:16px}" +
                "body{width:400px}" +
                "</style></head><body>" + body + "</body></html>";

            var options = CorpusRunner.Options();
            using var document = await HtmlConverter.ParseAsync(html, options);
            using var layout = HtmlConverter.LayoutDocument(document, options);

            var boxes = layout.Root
                .Descendants()
                .Where(_ => _.Element?.Id is {Length: > 0})
                .ToDictionary(_ => _.Element!.Id!, _ => _.BorderBox);

            builder.AppendLine($"--- {name}");

            foreach (var (id, want) in expected)
            {
                if (!boxes.TryGetValue(id, out var box))
                {
                    builder.AppendLine($"    MISSING #{id}");
                    failures++;
                    continue;
                }

                float[] got = [Round(box.X), Round(box.Y), Round(box.Width), Round(box.Height)];

                var ok = want.Zip(got).All(_ => Math.Abs(_.First - _.Second) <= 0.05f);
                if (!ok)
                {
                    failures++;
                }

                builder.AppendLine(
                    $"    {(ok ? "ok  " : "BAD ")}#{id} want [{string.Join(", ", want)}] got [{string.Join(", ", got)}]");
            }
        }

        builder.AppendLine($"FAILURES {failures}");
        await Verify(builder);
    }

    static float Round(float value) =>
        MathF.Round(value, 2);
}
