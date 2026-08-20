`clear`, in the three forms that behave differently.

- `#sides` has floats of different heights on either side. `#cl` clears only the left one, so it
  drops to 40px and still has the right float beside it, shortening its line. `#cr` clears the
  right one, which is lower, so it drops further. Treating `clear` as "below every float" would put
  both at the same place and pass a scenario with only one float in it.
- `#chain` puts `clear: left` on a FLOAT. `#f2` has room beside `#f1` and descends anyway, because
  clearance applies before the sideways search rather than instead of it.
- `#margins` clears a float whose bottom margin is larger than its height. Clearance measures to the
  margin box, so `#cm` lands 40px below the visible box rather than against it — which is also the
  check that the float context stores margin boxes rather than border boxes.

What is deliberately NOT measured here is clearance interacting with a large collapsed margin.
CSS 2.1 §9.5.2 makes clearance a separate quantity that also stops the margin collapsing through,
and the engine applies clearance after the collapsed margin instead. The difference appears only
when the cleared box has a margin big enough to clear the float unaided; `src/todo.md` records it.
