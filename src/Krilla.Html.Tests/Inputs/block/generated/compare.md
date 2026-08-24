# block/generated

# block/generated

Pixel-identical to Chrome. `::before` and `::after` with a `content` value, which is the property
most stylesheets reach for after the box model and which this engine read as nothing at all.

Generated content is INLINE CONTENT OF THE HOST rather than a box beside it. That is what makes a
`::before` share the host's first line, and it is why the items go into the same list the host's own
text goes into — the run-closing that turns mixed content into anonymous blocks then applies to it
without knowing it is generated.

AngleSharp gives the whole grammar back verbatim, which was the pleasant surprise: strings,
`attr()`, `counter()`, `counters()`, `url()`, the quote keywords, and concatenations of several. Two
things about its serialisation had to be worked around, and both were found here rather than
reasoned about:

- **The pseudo cascade INCLUDES the host's own declarations.** Ask a `::before` for its `display` and
  the host's comes back. So a property counts as the pseudo's only when it differs from what the
  host's own cascade says — sound, because a `::before` selector does not match the element. Without
  that test, `p { content: "x" }` — a declaration CSS ignores, `content` not applying to an ordinary
  element — generated a pseudo-element on every paragraph in the document.
- **`counters(section, ".")` comes back as `counters(section .)`**, comma gone, while
  `counter(chapter, upper-roman)` keeps its. Splitting arguments on commas alone therefore read the
  whole of the first as one argument and silently dropped every nested counter. `block/counters` is
  where that showed.

Two more things the scenario pins:

- **`#styled` gives the pseudo a background, a colour and padding**, and it gets all three, because
  a pseudo-element is a real inline box rather than text spliced into the host. The background is
  what made the painter's test wrong: it filled a run's own background only when the run had a
  SELECTOR, and generated content has no element to name one. A `Generated` flag says so instead.
  Testing the style INSTANCE against the block's — the reference identity `InlineAlign` uses — looks
  equivalent and is wrong inside an anonymous block, whose style is a fresh instance while the text
  keeps its parent's, so every anonymous run would paint its parent's background twice.
- **`#quoted` puts the content on an INLINE element**, which reaches none of this by the obvious
  route: an inline element contributes runs to the line being built rather than a box of its own, so
  it never passes through the method that generates content. `<q>` had no quotation marks at all
  until that branch got its own call — and `q::before` is exactly where a browser's quotes come from.

`#empty` declares `content: ""`, which still generates a box and still shows nothing. It is here
because the natural shortcut — treat an empty result as no pseudo — is indistinguishable until the
pseudo carries a background or a border, and then it is a missing rectangle.

What to look at: the red `note` chip in `#styled`, and the quotation marks in `#quoted`.

**Boxes**: 10 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

