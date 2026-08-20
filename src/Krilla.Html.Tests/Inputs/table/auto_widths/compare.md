# table/auto_widths

The automatic column algorithm, which is where a table's layout is decided and where the CSS
specification stops being useful — 17.5.2 describes it as a sketch and leaves the distribution to
the user agent. Every number here was measured out of Chrome.

The four tables are the four regimes, and they are not variations on one rule:

- `#content` has no declared width, so it shrinks to fit and each column takes its max-content
  width.
- `#wide` is wider than its content wants, and the surplus goes to each column in proportion to its
  max-content width. Handing each column its maximum and giving the remainder to the last would
  also fill the row, and looks nothing like a browser.
- `#narrow` sits between the two intrinsic widths, and the rule changes: each column takes its
  min-content width plus a share of the slack proportional to how much it could grow. Applying the
  `#wide` rule here is visibly wrong.
- `#floor` declares a width narrower than the content can be broken to. The declaration loses — a
  table never renders narrower than the sum of its columns' min-content widths.

`#narrow` also measures the min-content computation itself, since the column widths depend on which
word in each cell is the longest.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

