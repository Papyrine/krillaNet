# block/calc

`calc()` reaches this engine verbatim. AngleSharp.Css hands it back exactly as written, the same
way it hands back a bare percentage, so evaluating it is this engine's job — and before this it
fell through to the "unparseable" fallback and the property silently took its default. For a
`width` that means the box fills its container, which is a whole-page difference that no diagnostic
reported, because a value nothing recognises is a value nothing can report.

The eight rows are chosen to separate the cases that fold from the one that cannot:

- `#absolute` is `calc(10em + 12px)`, which is 172px before layout starts. Nothing downstream ever
  sees a calc, and the twenty-odd places that decide whether a length is definite by testing for an
  absolute one keep answering correctly without knowing the syntax exists.
- `#proportion` is `calc(50% / 2)`, which folds the other way — to a plain 25%.
- `#mixed` is `calc(100% - 120px)`, the case that needs both halves at once, and `CssLength` grew a
  kind for exactly this one. It resolves against the containing block like a percentage, which is
  why the sites testing for a definite length treat it as indefinite and why that is right rather
  than an omission.
- `#nested` proves the recursion and the product with the number on the left.
- `#clamped` is `calc(50% - 400px)` in a 600px frame, so it resolves to -100px. Chrome floors the
  used width at zero rather than at the declared value, and the row is 144px tall because six words
  each wrap onto a line of their own.
- `#padded`, `#tall` and `#shifted` put a calc on the three other properties that take one, since a
  value resolved correctly for `width` and forgotten for `padding-left` is how this defect
  usually presents.

Whitespace around `+` and `-` is required, which is CSS's own rule and not a shortcut here. Without
it `calc(2e-5px)` and `calc(10px -5px)` cannot be told apart.

Geometry is exact against Chrome on all eleven boxes.

What to look at: `#mixed` at 480px. A frame-wide box there is the calc falling back to `auto`.
