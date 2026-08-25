# text/ex_ch

# text/ex_ch

`ex` and `ch`, the two length units that name a glyph measurement rather than arithmetic on the
font size.

Both were approximated at half an em for as long as the value layer threaded a bare `float`: a
length parse had a size and no face, and the x-height and the advance of `0` are properties of the
glyphs. `CssFont` carries the face now, and `#half-em` is the row that says the difference is real —
it asks for `10em` against `#ex`'s `20ex`, and the two boxes are the same width only under the old
approximation.

## What the browser measures

At 20px, in the corpus's own faces:

| Row | Family | Declared | Chrome | Ratio to the em |
| --- | --- | --- | --- | --- |
| `#ex` | Liberation Sans | `20ex` | 211.33 | 0.5283 |
| `#ex-bold` | Liberation Sans Bold | `20ex` | 211.33 | 0.5283 |
| `#ex-mono` | Liberation Mono | `20ex` | 211.33 | 0.5283 |
| `#ch` | Liberation Sans | `20ch` | 222.45 | 0.5561 |
| `#ch-mono` | Liberation Mono | `20ch` | 240.03 | 0.6000 |
| `#ex-inherited` | Liberation Mono, inherited | `20ex` | 211.33 | 0.5283 |
| `#half-em` | Liberation Sans | `10em` | 200.00 | — |

Two things worth reading off that table, and the second was a surprise:

- **Neither unit is half an em**, and they are not the same quantity as each other. In one face
  `20ex` and `20ch` differ by 11px.
- **The three `ex` rows agree.** All four Liberation faces here share an x-height ratio, so `ex`
  alone cannot show that the value follows the FACE rather than the size — `ch` is what shows it,
  and `#ch-mono` is 17.6px wider than `#ch` because a monospaced advance is 0.6 of the em. So the
  rows are not redundant in the direction they look redundant: `#ex-mono` pins that a face change
  does NOT move `ex` here, and `#ch-mono` pins that it moves `ch`.

`#ex-inherited` takes its family from a wrapper rather than from its own declaration, so a
resolution that read the declaration being parsed instead of the resolved style would fall back to
the default face and be wrong by whatever that face's x-height is.

## What this cannot ask

Whether the unit resolves against the parent's face in a `font-size` declaration. `font-size: 3ex`
is the case, and its answer is the size in effect BEFORE the declaration — the same rule `em`
follows. Nothing in a box rectangle separates that from the other reading without a second element
to compare against, so it is settled in `StyleResolver` by construction instead: `ResolveFontSize`
is handed the parent's font rather than one built from a size it is still computing.

**Boxes**: 10 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

