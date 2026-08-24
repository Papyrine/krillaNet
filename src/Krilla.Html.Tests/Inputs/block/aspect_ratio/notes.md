# block/aspect_ratio

Pixel-identical to Chrome, and geometry-exact.

`aspect-ratio` supplies the height a box was not given: 200px at `4 / 1` is 50px tall. It goes
through the same `box-sizing` deflation a declared height does rather than beside it, and a declared
height wins outright — `#overridden` keeps its 30px, which is what makes the property safe to put on
a rule that some elements also size explicitly. `#proportional` shows the width may itself be
proportional, the ratio applying to the used value rather than the declared one.

Two things this scenario found that had nothing to do with the property.

**A box sized by its ratio is not self-collapsing**, which is the trap CLAUDE.md already records for
images, reached by a second route and costing the same debugging round. `IsSelfCollapsing` tests for
a zero height, and a ratio-sized box has `height: auto` and no content — so every test passed and the
box read as having nothing in it while being fifty pixels tall. Its own bottom margin then collapsed
through it and became a leading margin for the whole run, putting the entire page six pixels low.

**A block background fill has to be SNAPPED to whole pixels.** `#overridden` sits at y=786.5 and
Chrome paints rows 787 to 816, where the fractional rectangle covered 786 to 816 with half coverage
at each end. This is the third place the same construction argument has come up — after the inline
background fill and the background image — and it was the first thing in the corpus to produce a
fractional box height on purpose. Applying it improved eight existing scenarios and regressed none,
taking four of them to pixel-identical, including `inline/vertical_align`, whose documented residual
was Chrome's 1/64-pixel quantisation showing through exactly this edge.

The `#square` row is written `1 / 1` rather than `1`. AngleSharp's grammar for the property requires
both parts and DROPS a single number, so `aspect-ratio: 1` reaches the cascade as nothing at all — a
gap that cannot even be reported, the declaration being gone before the engine sees it.

What to look at: the heights — 50, 200, 400, 112.5, 30, 51 — and the top edge of the page, which is
where the self-collapsing bug showed.
