# flex/justify

Nine distributions of the same three items along the main axis. Geometry-exact on all 38 boxes and
pixel-identical to Chrome.

All three `space-*` values are here rather than one standing in for the rest, because they differ
ONLY in what happens at the EDGES — none, half a share, a whole share — and that is precisely the
part most likely to come out subtly wrong when each is written on its own. On 480 of container and
240 of items: `space-between` puts nothing at the edges and 120 between each pair, `space-around`
puts 40 at each edge and 80 between, and `space-evenly` puts 60 everywhere.

- **`#evenly`** is also what proves the recovery from the stylesheet's own text works.
  `space-evenly` is a CSS Box Alignment keyword rather than a Flexbox one, and AngleSharp drops it
  from the cascade outright — the declaration comes back empty and is indistinguishable from one
  nobody wrote, which is the shape a value can be neither honoured NOR reported in. Without the
  recovery this row renders as `flex-start` and is byte-identical to `#start`, which is the silence
  the corpus exists to break. `start`, `end` and `normal` are dropped the same way on all
  three alignment properties.
- **`#gapped`** is a gap and a distribution together: the gap is a floor the distribution adds to
  rather than something it replaces.
- **`#pushed`** is the precedence. An auto margin absorbs ALL the free space before
  `justify-content` is consulted, so the `space-around` on this row reaches nothing and B is pushed
  to the far end. The two are written together by mistake often enough to be worth pinning.
- **`#over`** is negative free space. Every distribution puts it at the END rather than sharing it,
  so `space-between` packs to the start and the first item stays visible. Splitting the overflow —
  which is what a naive `free / 2` for `center` does on its own — pushes the first item off the
  start edge, and that reads as a centring bug in exactly the case where centring was impossible.
