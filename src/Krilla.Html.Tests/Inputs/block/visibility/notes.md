# block/visibility

`visibility: hidden` is not `display: none`, and the difference is the whole scenario. The hidden
box is laid out, occupies its 24px line and holds `#after` down at y=48 exactly as a painted one
would. Only the ink is missing.

Getting that backwards is the shape this defect takes: treating the property as removing the box
closes the gap it left, and every box below it moves up by its height. The geometry comparison
catches it immediately, which is why the scenario leads with a plain hidden box between two visible
ones rather than with anything more interesting.

The second half is that a descendant can bring itself back. The property inherits and hiding is
nothing more than not painting, so `#child` with `visibility: visible` inside a hidden parent is
drawn while the parent's background and the text either side of it are not. That is what forces
the check to be per RUN rather than per box: one line here carries hidden and visible text at once,
and a box-level test paints all of it or none.

A link annotation is still queued for a hidden run. It carries no appearance stream, so a browser
does not hide the clickable rectangle either, and neither corpus measurement can see one — this is
recorded because it is a decision rather than an oversight.

Geometry is exact. Pixels read SSIM 1.0000 with a scattering of antialiased pixels on the one line
that does paint.

What to look at: whether `#after` sits at y=48. If it has moved to y=24 the property is being read
as `display: none`. If `#child` disappears, the check has been put on the box instead of the run.
