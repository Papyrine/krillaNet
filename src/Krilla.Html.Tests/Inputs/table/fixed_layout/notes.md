`table-layout: fixed` beside the automatic algorithm on the same markup, which is the only way to
see that it is a different algorithm rather than a tuning of one.

`#fixed` and `#auto` differ by one declaration and by nothing else. Fixed layout reads the first row
and stops: the pinned column takes its declared width and the two automatic columns split what is
left equally, whatever the second row contains. The automatic table reads every cell, so its second
row — the long one — decides the first column's width and the three columns come out unequal.

`#percent` measures a percentage column, which is where the two algorithms genuinely disagree about
what a percentage means. Under the automatic algorithm the percentage is the whole column, border
and padding included, because it has to compete with content widths that are measured that way.
Under fixed layout there is no content to compete with, so it is the cell's `width` under ordinary
content-box sizing and the padding is added on top. The difference is exactly the cell's padding,
which is small enough to look like a rounding error and is not one.
