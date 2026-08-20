# table/spans

Cells covering more than one slot, which is what makes a table a grid rather than rows of boxes.

- `#cols` has a spanning cell wider than the columns beneath it. The shortfall is shared in
  proportion to what those columns already wanted, so the wide cell does not put its extra width
  into a column with almost nothing in it. Sharing equally is the obvious alternative and is wrong.
- `#rows` covers two rows with one cell, which is the case that breaks naive column assignment: the
  second row's first cell belongs in the SECOND column, because the first is already taken. Getting
  that wrong shears every row below a span sideways, and it would still look plausible.
- `#stretch` forces the spanning cell taller than its rows need, and the extra is shared equally
  between them — the opposite of how a column shortfall is shared, and measured rather than assumed.
- `#mixed` combines both in one grid, so a row is entered with some columns already occupied and
  some cells spanning onward from it.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

