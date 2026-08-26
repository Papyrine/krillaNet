# ua/presentational_text

The presentational attributes that are not a table's: `<font>`, `<hr>`, a list's `type`, and an
image's spacing and border. Exact on all 29 boxes, at SSIM 0.9999.

The sibling scenario `ua/presentational` covers the table attributes and carries the design note
about why these have to be `ua/` scenarios at all — `flatten.css` writes author declarations that
beat a hint outright, correctly and in both engines.

What each row is for:

- The `<font>` paragraph — the size levels, which are a table of seven keywords rather than a
  length, and the two relative forms, which count from 3 rather than from the parent. Measured
  against Chrome at every one: `size="1"` is `x-small`, `size="6"` is `xx-large`, `size="+2"` is
  `x-large` and `size="-1"` is `small`. `size="7"` needed `xxx-large`, which the keyword table did
  not have — nobody writes it in a stylesheet, and this attribute is where documents ask for it.
  `color` and `face` are here too, the latter because a bare family name has to be quoted before
  CSS will take it.
- `#plain` — the control: a rule with no attributes, drawn by its 1px `inset` border.
- `#thick` — `size="9"`. A rule is a zero-height box drawn entirely by its border, so `size` asks
  for a thicker BOX and the two border pixels come out of it. Nine pixels tall, not eleven.
- `#solid` — `noshade`, which turns the carved rule into a flat one. It is a solid GREY bar,
  measured: not the element's colour, and not the shades a carved rule derives. The fill is the
  half that a reading of HTML's own wording misses — without it the rule is a nine-pixel white bar
  with a hairline around it.
- `#painted` — `color` and `width`, which is the same flat rule with the colour named.
- `#short` — `align="left"`, which is a margin pair rather than `text-align`, and `width`, which
  the border is added to: 182px for a `width="180"`.
- The four lists — `type` in both spellings and on an `<li>` rather than its list. `#mixed` is the
  one that matters: the item's own `type` has to beat the marker its list already gave it, which
  is a value the user-agent sheet supplies rather than an absent one.
- `#wrapped` — `hspace`, `vspace` and `border` on an inline image, which reach the box model an
  atomic inline was not being given at all. `image/inline_surround` measures that half in CSS.
- `#around` — `align="right"`, which FLOATS a picture where the same attribute on a paragraph
  aligns its text.

## Residuals

543 pixels differ and none of them is this feature: twelve on the rules' bevelled corners, forty on
the two list markers' antialiased circles, and the rest sub-pixel glyph positioning — most of it in
the `<font>` paragraph, which carries six sizes on three lines and so has more glyph edges than
anything else in the corpus of its size.

## What to look at when it moves

Six `<font>` runs on six separate lines is the element back to block-level: it has no `display` in
AngleSharp's sheet, and `UserAgentStyles` is what supplies one. A rule two pixels too tall is the
`size` subtraction gone; a hollow one is `noshade` no longer filling. A bullet where a letter
belongs is `type` losing to the user-agent's own `list-style-type`, which it is allowed to beat and
an author rule is not.
