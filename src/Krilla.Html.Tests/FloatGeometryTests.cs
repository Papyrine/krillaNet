/// <summary>
/// Float placement, checked against geometry measured out of Chrome.
/// </summary>
/// <remarks>
/// The corpus measures the same engine against the same browser and does it at pixel level, so
/// this is not there to duplicate it. What it adds is the DERIVATION: nineteen float arrangements
/// whose expected rects were read out of headless Chrome while the rules were being worked out,
/// kept in a form that runs without a browser and names the arrangement it broke.
///
/// Several are arrangements no corpus scenario has a reason to contain. The last is the one that
/// earned its place: a line whose top clears a narrow float while its body reaches into a wider
/// one below. It is the only case that distinguishes shortening a line by the floats overlapping
/// it from shortening it by the floats under its top edge, and both readings are defensible until
/// this case is run.
/// </remarks>
public class FloatGeometryTests
{
    // Each case is the markup handed to Chrome, with the border boxes Chrome reported for it.
    // Cases are laid out one at a time so a float in one cannot reach into the next.
    static readonly (string Name, string Body, (string Id, float[] Box)[] Expected)[] cases =
    [
        ("c1 left float",
            "<div id=\"f\" style=\"float:left;width:100px;height:60px\"></div>" +
            "<p id=\"p\">alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron pi</p>",
            [("f", [0, 0, 100, 60]), ("p", [0, 0, 400, 54])]),

        ("c2 right float",
            "<div id=\"f\" style=\"float:right;width:100px;height:60px\"></div>" +
            "<p id=\"p\">alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron pi</p>",
            [("f", [300, 0, 100, 60]), ("p", [0, 0, 400, 54])]),

        ("c3 two lefts fit",
            "<div id=\"a\" style=\"float:left;width:100px;height:40px\"></div>" +
            "<div id=\"b\" style=\"float:left;width:120px;height:40px\"></div>" +
            "<p id=\"p\">alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi</p>",
            [("a", [0, 0, 100, 40]), ("b", [100, 0, 120, 40]), ("p", [0, 0, 400, 72])]),

        ("c4 two lefts overflow",
            "<div id=\"a\" style=\"float:left;width:250px;height:40px\"></div>" +
            "<div id=\"b\" style=\"float:left;width:200px;height:30px\"></div>" +
            "<p id=\"p\">alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi</p>",
            [("a", [0, 0, 250, 40]), ("b", [0, 40, 200, 30]), ("p", [0, 0, 400, 72])]),

        ("c5 float with margins",
            "<div id=\"f\" style=\"float:left;width:80px;height:50px;margin:10px 20px 15px 5px\"></div>" +
            "<p id=\"p\">alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi</p>",
            [("f", [5, 10, 80, 50]), ("p", [0, 0, 400, 36])]),

        ("c6 clear left",
            "<div id=\"f\" style=\"float:left;width:100px;height:80px\"></div>" +
            "<p id=\"p\">alpha beta gamma</p>" +
            "<p id=\"q\" style=\"clear:left\">cleared paragraph text here</p>",
            [("f", [0, 0, 100, 80]), ("p", [0, 0, 400, 18]), ("q", [0, 80, 400, 18])]),

        ("c7 shrink to fit",
            "<div id=\"f\" style=\"float:left\">shrink to fit this text</div>" +
            "<p id=\"p\">alpha beta gamma delta epsilon zeta</p>",
            [("f", [0, 0, 136.94f, 18]), ("p", [0, 0, 400, 18])]),

        ("c8 shrink to fit clamped",
            "<div id=\"f\" style=\"float:left\">a b c d e f g h i j k l m n o p q r s t u v w x y z aa bb cc dd ee ff gg hh</div>",
            [("f", [0, 0, 400, 36])]),

        ("d1 wider float starts mid-line",
            "<div id=\"a\" style=\"float:left;width:60px;height:25px\"></div>" +
            "<div id=\"b\" style=\"float:left;width:200px;height:60px\"></div>" +
            "<p id=\"p\">alpha beta gamma delta epsilon zeta eta theta iota kappa lambda</p>",
            [("a", [0, 0, 60, 25]), ("b", [60, 0, 200, 60]), ("p", [0, 0, 400, 72])]),

        ("d2 float bottom on line boundary",
            "<div id=\"f\" style=\"float:left;width:150px;height:36px\"></div>" +
            "<p id=\"p\">alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu</p>",
            [("f", [0, 0, 150, 36]), ("p", [0, 0, 400, 54])]),

        ("d6 float after block sibling",
            "<div id=\"b\" style=\"height:20px\"></div>" +
            "<div id=\"f\" style=\"float:left;width:100px;height:60px\"></div>" +
            "<p id=\"p\">alpha beta gamma delta epsilon zeta eta theta</p>",
            [("b", [0, 0, 400, 20]), ("f", [0, 20, 100, 60]), ("p", [0, 20, 400, 36])]),

        ("d7 clear right and left",
            "<div id=\"l\" style=\"float:left;width:80px;height:40px\"></div>" +
            "<div id=\"r\" style=\"float:right;width:80px;height:70px\"></div>" +
            "<p id=\"a\" style=\"clear:left\">cleared left</p>" +
            "<p id=\"b\" style=\"clear:right\">cleared right</p>",
            [("l", [0, 0, 80, 40]), ("r", [320, 0, 80, 70]), ("a", [0, 40, 400, 18]), ("b", [0, 70, 400, 18])]),

        ("d8 float clearing a float",
            "<div id=\"a\" style=\"float:left;width:80px;height:40px\"></div>" +
            "<div id=\"b\" style=\"float:left;clear:left;width:80px;height:30px\"></div>" +
            "<p id=\"p\">alpha beta gamma</p>",
            [("a", [0, 0, 80, 40]), ("b", [0, 40, 80, 30]), ("p", [0, 0, 400, 18])]),

        ("d9 shrink to fit from a block child",
            "<div id=\"f\" style=\"float:left\"><div id=\"i\" style=\"width:70px;height:30px\"></div></div>" +
            "<p id=\"p\">alpha beta gamma delta</p>",
            [("f", [0, 0, 70, 30]), ("i", [0, 0, 70, 30]), ("p", [0, 0, 400, 18])]),

        ("d11 float wider than container",
            "<div id=\"f\" style=\"float:left;width:500px;height:30px\"></div>" +
            "<p id=\"p\">alpha beta gamma</p>",
            [("f", [0, 0, 500, 30]), ("p", [0, 0, 400, 48])]),

        ("d12 two right floats",
            "<div id=\"a\" style=\"float:right;width:100px;height:40px\"></div>" +
            "<div id=\"b\" style=\"float:right;width:100px;height:40px\"></div>" +
            "<p id=\"p\">alpha beta gamma delta epsilon zeta</p>",
            [("a", [300, 0, 100, 40]), ("b", [200, 0, 100, 40]), ("p", [0, 0, 400, 36])]),

        ("d13 float in narrow parent",
            "<div id=\"w\" style=\"width:200px\">" +
            "<div id=\"f\" style=\"float:right;width:60px;height:40px\"></div>" +
            "<p id=\"p\">alpha beta gamma delta</p></div>",
            [("w", [0, 0, 200, 36]), ("f", [140, 0, 60, 40]), ("p", [0, 0, 200, 36])]),

        ("d14 blocks after a float",
            "<div id=\"f\" style=\"float:left;width:100px;height:60px\"></div>" +
            "<div id=\"b\" style=\"height:20px\"></div>" +
            "<div id=\"c\" style=\"width:50px;height:20px\"></div>",
            [("f", [0, 0, 100, 60]), ("b", [0, 0, 400, 20]), ("c", [0, 20, 50, 20])]),

        ("e1 full overlap not top sampling",
            "<div id=\"a\" style=\"float:left;width:50px;height:20px\"></div>" +
            "<div id=\"s\" style=\"height:25px\"></div>" +
            "<div id=\"b\" style=\"float:left;width:200px;height:60px\"></div>" +
            "<p id=\"p\" style=\"margin-top:-25px\">alpha beta gamma delta epsilon zeta eta theta iota</p>",
            [("a", [0, 0, 50, 20]), ("b", [0, 25, 200, 60])])
    ];

    [Test]
    public async Task MatchesChrome()
    {
        var builder = new StringBuilder();
        var failures = 0;

        foreach (var (name, body, expected) in cases)
        {
            // The element names are listed as well as the universal selector for the reason
            // Inputs/flatten.css gives: AngleSharp compares specificity across cascade origins, so
            // a bare `*` does not clear the user-agent margins on `body` and `p`. Chrome resolves
            // origin first and zeroes them from the `*` alone, so naming them changes nothing on
            // its side and makes the two engines start from the same page.
            var html =
                "<!doctype html><html><head><style>" +
                "*{margin:0;padding:0;border:none;box-sizing:content-box}" +
                "html,body,div,p{margin:0;padding:0;border:none}" +
                "html{font-family:\"Liberation Sans\";font-size:16px}" +
                "body{width:400px}" +
                "</style></head><body>" + body + "</body></html>";

            var options = CorpusRunner.Options();
            using var document = HtmlConverter.Parse(html, options);
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
