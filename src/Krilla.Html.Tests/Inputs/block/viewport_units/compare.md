# block/viewport_units

# block/viewport_units

The four viewport units, which were parsed as unrecognised and fell back to the property's default.

In paged media the viewport is the page's CONTENT box rather than a window, so `100vh` is one page
tall. That is what a browser printing to PDF resolves against, and it is what makes this scenario
possible at all: the corpus uses zero page margins, so the page content box and the browser's
screen viewport are the same 816 by 1056 rectangle and both sides of the comparison mean the same
thing by `vw`.

Unlike a percentage, a viewport unit does NOT depend on the containing block — `50vw` is half the
page wherever the box sits — so it resolves during parsing and never reaches layout as anything but
an absolute length.

`#least` and `#most` are the rows worth having. `10vmin` and `10vmax` are 81.59 and 105.59 rather
than 81.6 and 105.6, because Chrome holds every layout length on a 1/64 pixel grid and truncates
onto it. Both sides agree at two decimals regardless, which is what the comparison reads.

`#mixed` puts a viewport unit inside a `calc()`, since the two features were written together and a
unit table consulted by only one of the two parsers is the obvious way for them to disagree.

Geometry is exact against Chrome.

What to look at: `#half` at 408px. Anything else means the viewport is not the page.

**Boxes**: 7 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

