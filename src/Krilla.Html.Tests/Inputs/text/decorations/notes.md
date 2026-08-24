# text/decorations

The three rules, alone and together. Underline was the only one painted; overline and line-through
were recognised and dropped, which is the worst of the three states — a strike that vanishes turns
deleted text back into text that still reads as current.

Every position comes from the font's own tables rather than a fraction of the size, and each was
checked against Chrome's own render before being written. At 16px Liberation Sans:

- **underline** at baseline + 1, from `post.underlinePosition` and `underlineThickness`.
- **line-through** at baseline − 5, from `OS/2.yStrikeoutPosition` and `yStrikeoutSize`.
- **overline** at baseline − 15, which is the only one with no metric of its own: it sits on the
  top of the em box, the rounded ASCENT above the baseline, with the underline's thickness.

All three land on the row the browser puts them on, and the page is pixel-identical.

The rounding is what makes that work. This font puts the strike 4.14px above the baseline and makes
it 0.797px thick; rounded, that is one whole pixel row at baseline − 5, which is what Chrome draws.
An unrounded rule straddles two rows at partial coverage and reads as a grey smear rather than a
line — the same lesson `ListMarkers` records about integer arithmetic being the point rather than a
shortcut.

`#nested` records a limit rather than a success. CSS propagates a decoration ACROSS descendants
from the element that declared it, keeping that element's colour; this engine inherits it instead,
so the strike over the red inner span is drawn red where a browser draws it black. The two models
agree everywhere else, and the difference needs a descendant that sets its own colour — which is
why the row is here at all. It costs nothing in this scenario because both engines put ink in the
same places; a scenario with a coloured strike would report it.

What to look at: whether `#all` carries three rules and `#through` one. A missing overline is the
rule with no font metric behind it.
