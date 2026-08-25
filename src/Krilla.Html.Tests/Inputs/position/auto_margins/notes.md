# position/auto_margins

An auto margin on an absolutely positioned box, which is the one place such a margin means anything
other than zero.

CSS 2.1 §10.3.7 states the horizontal equation:

```
left + margin-left + border-box-width + margin-right + right = containing block width
```

With all of `left`, the width and `right` given, that is over-constrained — and it is the auto
margins that absorb the difference. Both auto splits it, which is what centres a box given
`left: 0; right: 0; margin: 0 auto`, one of the two standard centring idioms. §10.6.4 says the same
thing vertically with `top`, the height and `bottom`.

It used to resolve to zero unconditionally, with a comment saying centring "is not a case this
distinguishes yet". Nothing in the corpus measured it.

## The rows

- **`#centred`** centres between the OFFSETS, not against the containing block. The offsets are 20
  and 100, so the two readings are 40px apart — a scenario with symmetric offsets would pass either.
- **`#pushed`** has one auto margin, which takes the whole remainder rather than half of it.
- **`#middled`** is the vertical case, which needs a declared height for the same reason the
  horizontal one needs a declared width.
- **`#wider`** overflows the gap its offsets leave. The slack is negative and is NOT split: the box
  stays against the start edge and hangs out of the far one. Splitting would pull it 60px left of
  the offset, which reads as a centring bug in exactly the case where centring was impossible.
- **`#filled`** and **`#left`** are the two rows where an auto margin still means zero — an auto
  width already spans the offsets so there is no slack, and a single offset does not over-constrain
  anything. Both render identically with the property implemented and without it, and they are here
  so that an over-eager reading has somewhere to fail.
- **`#stretched`** is `#filled`'s vertical mirror, and it is the row that says an auto HEIGHT spans
  `top` to `bottom`. Its auto margins are zero for the same reason: the box already fills the gap.
  Its single line of text stays at the TOP of the box rather than being stretched or centred, so
  what says the box grew is its background reaching the frame's bottom edge — a stretch that was
  not applied leaves a 24px box where a 45px one belongs.
- **`#inset`** is the same with a border and padding inside it. The gap the offsets leave is a
  MARGIN-box extent, so the surround comes OUT of the height rather than being added to it: 70px of
  frame less a 10px top offset, a 15px bottom offset and a 5px top margin leaves a 40px border box,
  and 10px of border and 15px of padding leave 15px of content. Adding the surround instead gives a
  box 25px too tall, which overflows the frame and is the mistake this row exists to catch.
