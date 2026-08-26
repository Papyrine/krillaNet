# float/clearance

# float/clearance

`clear` in the arrangements where clearance and margin collapsing interact, and the defect one of
them found — which turned out not to be about `clear` at all.

`float/clear` measures where a cleared box lands. This measures what happens to the MARGINS around
it, which CSS 2.1 §9.5.2 and §8.3.1 settle between them.

## The rows

- **`#collapsed`** is the ordinary case: a 40px bottom margin and a 20px top margin collapse to 40,
  which would put the box 64px down, and the float's bottom at 100 wins. Clearance is the 36px
  difference and the border edge lands exactly on the float's bottom edge — the collapsed margin is
  absorbed rather than added to it.
- **`#unaided`** is the row the engine was suspected of getting wrong: a cleared box whose own
  150px top margin already carries it past the float. Clearance is then ZERO and the margin stands
  in full, so the box sits at 174 rather than being pulled back to the float's bottom at 100.
  Measured, and it already agreed — which is the useful half of the result.
- **`#through`** has a float short enough that the collapsed margin clears it unaided, so nothing is
  introduced and the box after it collapses normally.
- **`#nofloat`** is the control with nothing to clear at all.
- **`#selfcollapsing`** and **`#plain`** are the two that found something, and only the second
  mentions `clear`.

## What they found

An EMPTY box between two boxes with margins had its margins applied TWICE. With a 40px margin above
it and a 50px margin of its own, everything after it sat 90px down where 50 belongs.

CSS 2.1 §8.3.1 calls this collapsing *through*: a box with no height, no border and no padding does
not SEPARATE the margin above it from the margin below, so the two join one collapsed set that is
applied once. The box itself keeps the position the partial collapse gave it — which is what a
browser reports for it, and what `#plain`'s middle row pins at 64 — and the flow position returns to
where the margin started, so the box after it lands at 74 rather than 114.

`#selfcollapsing` is the same box carrying `clear`, which matters because **clearance takes a box
out of that rule**: §8.3.1's own wording is that two margins are adjoining only when no clearance
separates them, so a box that took any keeps its margins apart the way a box with a border does.
Here the float is short enough that no clearance is introduced, so it collapses through like any
other empty box — and an implementation that keyed the rule on the DECLARATION rather than on the
clearance actually taken would get this row wrong.

Nothing in the corpus held an empty box between two boxes with margins until this scenario did,
which is why a defect this plain survived: it needs a box with nothing in it, and every scenario
written before this one had something in every box.



## A cleared FIRST child

`#wrapper` and `#loose` are the last pair, and they measure the half of §8.3.1 this used to get
wrong: "the top margin of an in-flow block element collapses with its first in-flow block-level
child's top margin if the element has no top border, no top padding, **and the child has no
clearance**". The child's 20px margin escaped through the wrapper's top edge whatever the clearance
was, so the wrapper sat 20px low and came out 20px short. `#loose` is the same arrangement with
nothing to clear, where the collapse is correct — the two rows differ by exactly that margin, which
is what makes either of them attributable.

The padding on `.outer` is load-bearing rather than decoration. It stops the outer box's own top
margin escaping, which is what puts the float into the context BEFORE the wrapper's leading margin
is asked for — and the ordering is the whole difficulty here.

Whether a child has clearance depends on where the floats end, and a float declared in the same
parent is placed while that parent is laid out, which is after the parent's own position was
settled by the very margin this rule changes. §9.5.2 is written in terms of a *hypothetical*
position for exactly that reason. So the question is only asked where the answer cannot change
underneath it: while a margin is still escaping through the parent's top edge, a float declared at
or before the child in that same parent turns the test off, because the ancestor asked the same
question without it. Once the escaping run has ended the context is fully up to date and the full
answer is used.

The case that remains is a wrapper whose FIRST child clears a float declared in that same wrapper's
parent ahead of it. Both passes disagree about whether the float exists, and reconciling them needs
the second pass §9.5.2's hypothetical position exists to avoid.

Exact on all 34 boxes and pixel-identical.

**Boxes**: 34 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

