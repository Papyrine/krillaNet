/// <summary>
/// Renders produced documents through PDFium and snapshots the result.
/// </summary>
/// <remarks>
/// These assert what a reader draws, not the bytes krilla emitted. That distinction matters:
/// a byte-level diff of a PDF is unreadable, and it fails on incidental changes — object
/// numbering, stream compression — that no viewer would show differently. Rendering makes a
/// regression visible as a picture.
/// </remarks>
public class SnapshotTests
{
    static byte[] Draw(Action<Surface> draw, float width = 200, float height = 200)
    {
        using var document = new KrillaDocument();

        using (var page = document.StartPage(width, height))
        {
            draw(page.Surface);
        }

        return document.Finish();
    }

    [Test]
    public Task FilledRectangle() =>
        Verify(Draw(_ => _.FillRectangle(new(40, 40, 160, 120), Color.Rgb(220, 40, 40))), "pdf")
            .Snapshot(
                """
                {
                  PageCount: 1,
                  Pages: [
                    {
                      Width: 200.0,
                      Height: 200.0
                    }
                  ]
                }
                """);

    [Test]
    public Task Triangle()
    {
        using var paint = Paint.Solid(Color.Rgb(30, 90, 200));

        return Verify(extension: "pdf", target: Draw(surface =>
            {
                using var path = PdfPath.Polygon(
                    new Point(20, 20),
                    new(180, 20),
                    new(100, 170));

                surface.SetFill(paint).DrawPath(path);
            }))
            .Snapshot(
                """
                {
                  PageCount: 1,
                  Pages: [
                    {
                      Width: 200.0,
                      Height: 200.0
                    }
                  ]
                }
                """);
    }

    [Test]
    public Task DashedStroke()
    {
        using var paint = Paint.Solid(Color.Rgb(20, 20, 20));

        return Verify(extension: "pdf", target: Draw(surface =>
            {
                using var path = PdfPath.Rectangle(new(30, 30, 170, 170));

                surface
                    .SetFill(null)
                    .SetStroke(new Stroke(paint, Width: 6, DashArray: [14, 7], LineJoin: LineJoin.Round))
                    .DrawPath(path);
            }))
            .Snapshot(
                """
                {
                  PageCount: 1,
                  Pages: [
                    {
                      Width: 200.0,
                      Height: 200.0
                    }
                  ]
                }
                """);
    }

    [Test]
    public Task LinearGradient()
    {
        using var gradient = Paint.LinearGradient(
            0, 0, 200, 0,
            [
                new(0f, Color.Rgb(255, 90, 0)),
                new(0.5f, Color.Rgb(255, 220, 0)),
                new(1f, Color.Rgb(0, 90, 255))
            ]);

        return Verify(extension: "pdf", target: Draw(surface =>
            {
                using var path = PdfPath.Rectangle(new(0, 0, 200, 200));
                surface.SetFill(gradient).DrawPath(path);
            }))
            .Snapshot(
                """
                {
                  PageCount: 1,
                  Pages: [
                    {
                      Width: 200.0,
                      Height: 200.0
                    }
                  ]
                }
                """);
    }

    [Test]
    public Task RadialGradient()
    {
        using var gradient = Paint.RadialGradient(
            100, 100, 0,
            100, 100, 90,
            [
                // Both stops RGB. Color.White would be luma, and krilla rejects a gradient
                // whose stops span colour spaces.
                new(0f, Color.Rgb(255, 255, 255)),
                new(1f, Color.Rgb(10, 40, 120))
            ]);

        return Verify(extension: "pdf", target: Draw(surface =>
            {
                using var path = PdfPath.Rectangle(new(0, 0, 200, 200));
                surface.SetFill(gradient).DrawPath(path);
            }))
            .Snapshot(
                """
                {
                  PageCount: 1,
                  Pages: [
                    {
                      Width: 200.0,
                      Height: 200.0
                    }
                  ]
                }
                """);
    }

    [Test]
    public Task SweepGradient()
    {
        using var gradient = Paint.SweepGradient(
            100, 100, 0, 360,
            [
                new(0f, Color.Rgb(255, 0, 0)),
                new(0.33f, Color.Rgb(0, 255, 0)),
                new(0.66f, Color.Rgb(0, 0, 255)),
                new(1f, Color.Rgb(255, 0, 0))
            ]);

        return Verify(extension: "pdf", target: Draw(surface =>
            {
                using var path = PdfPath.Rectangle(new(0, 0, 200, 200));
                surface.SetFill(gradient).DrawPath(path);
            }))
            .Snapshot(
                """
                {
                  PageCount: 1,
                  Pages: [
                    {
                      Width: 200.0,
                      Height: 200.0
                    }
                  ]
                }
                """);
    }

    [Test]
    public Task NestedOpacity() =>
        Verify(
            extension: "pdf",
            target: Draw(surface =>
            {
                // Two 0.5 layers compose to 0.25, so the inner square is visibly fainter.
                surface.FillRectangle(new(20, 20, 120, 120), Color.Rgb(200, 0, 0));

                using (surface.PushOpacity(0.5f))
                {
                    surface.FillRectangle(new(50, 50, 150, 150), Color.Rgb(0, 150, 0));

                    using (surface.PushOpacity(0.5f))
                    {
                        surface.FillRectangle(new(80, 80, 180, 180), Color.Rgb(0, 0, 200));
                    }
                }
            }))
            .Snapshot(
                """
                {
                  PageCount: 1,
                  Pages: [
                    {
                      Width: 200.0,
                      Height: 200.0
                    }
                  ]
                }
                """);

    [Test]
    public Task ClipPath() =>
        Verify(
            extension: "pdf",
            target: Draw(surface =>
            {
                using var clip = PdfPath.Polygon(
                    new Point(100, 20),
                    new(180, 100),
                    new(100, 180),
                    new(20, 100));

                using (surface.PushClip(clip))
                {
                    // Fills the whole page; only the diamond survives.
                    surface.FillRectangle(new(0, 0, 200, 200), Color.Rgb(200, 60, 160));
                }
            }))
            .Snapshot(
                """
                {
                  PageCount: 1,
                  Pages: [
                    {
                      Width: 200.0,
                      Height: 200.0
                    }
                  ]
                }
                """);

    [Test]
    public Task Transforms() =>
        Verify(
            extension: "pdf",
            target: Draw(surface =>
            {
                foreach (var step in Enumerable.Range(0, 6))
                {
                    using (surface.PushTransform(Matrix.Translate(100, 100)))
                    using (surface.PushTransform(Matrix.Rotate(step * 15)))
                    {
                        surface.FillRectangle(
                            new(-70, -10, 70, 10),
                            Color.Gray((byte) (40 + step * 30)));
                    }
                }
            }))
            .Snapshot(
                """
                {
                  PageCount: 1,
                  Pages: [
                    {
                      Width: 200.0,
                      Height: 200.0
                    }
                  ]
                }
                """);

    [Test]
    public Task CurvedPath()
    {
        using var paint = Paint.Solid(Color.Rgb(0, 120, 130));

        return Verify(extension: "pdf", target: Draw(surface =>
            {
                using var path = new PathBuilder()
                    .MoveTo(20, 150)
                    .CubicTo(60, 20, 140, 20, 180, 150)
                    .QuadraticTo(100, 190, 20, 150)
                    .Close()
                    .Build();

                surface.SetFill(paint).DrawPath(path);
            }))
            .Snapshot(
                """
                {
                  PageCount: 1,
                  Pages: [
                    {
                      Width: 200.0,
                      Height: 200.0
                    }
                  ]
                }
                """);
    }

    [Test]
    public Task RgbaImage()
    {
        // A 4x4 checkerboard, so scaling and orientation are both visible in the snapshot.
        var pixels = new byte[4 * 4 * 4];

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var offset = (y * 4 + x) * 4;
                var dark = (x + y) % 2 == 0;

                pixels[offset] = dark ? (byte) 30 : (byte) 230;
                pixels[offset + 1] = dark ? (byte) 90 : (byte) 230;
                pixels[offset + 2] = dark ? (byte) 160 : (byte) 230;
                pixels[offset + 3] = 255;
            }
        }

        using var image = PdfImage.FromRgba(pixels, 4, 4);

        return Verify(extension: "pdf", target: Draw(surface => surface.DrawImage(image, new(25, 25, 175, 175))))
            .Snapshot(
                """
                {
                  PageCount: 1,
                  Pages: [
                    {
                      Width: 200.0,
                      Height: 200.0
                    }
                  ]
                }
                """);
    }

    [Test]
    public Task MultiplePages()
    {
        using var document = new KrillaDocument();

        foreach (var index in Enumerable.Range(0, 3))
        {
            using var page = document.StartPage(200, 120);
            page.Surface.FillRectangle(
                new(20, 20, 20 + 50 * (index + 1), 100),
                Color.Gray((byte) (60 + index * 70)));
        }

        // Verify.PDFium renders every page, so the snapshot covers page count and ordering.
        return Verify(document.Finish(), "pdf");
    }
}
