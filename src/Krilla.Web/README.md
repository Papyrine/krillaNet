# Krilla.Web

A Blazor WebAssembly single-page app that converts HTML to PDF entirely in the browser. Nothing is
uploaded anywhere. Modelled on [Morph.Web](https://github.com/Papyrine/Morph/tree/main/src/Morph.Web)
for its layout, theming, testing and deployment.

## What makes this one different

Every other Papyrine web app is managed code all the way down. This one is not: the converter is
`Krilla.Html` on top of `Krilla`, and `Krilla` is a P/Invoke layer over a Rust library. On
`browser-wasm` that library is a **static archive linked into this app's own `.wasm` module**,
because WebAssembly has no dynamic loader for a P/Invoke to search.

Three consequences worth knowing before changing anything here:

- **`WasmBuildNative` is required, not an optimisation.** It is what relinks the runtime with emcc
  and pulls the archive in. Without it the app builds, publishes, and fails on the first
  conversion.
- **The archive has to exist before the app is published.** `.github/workflows/deploy-blazor.yml`
  builds it with cargo and stages it into `src/Krilla/runtimes/browser-wasm/native/`, which is
  where the import below looks.
- **This project imports `Krilla`'s `buildTransitive/Krilla.targets` by path.** A NuGet consumer
  gets that file automatically; a `ProjectReference` does not import a referenced project's MSBuild
  assets, and a `NativeFileReference` declared by a referenced project does not reach the consuming
  app at all ([dotnet/runtime#114724](https://github.com/dotnet/runtime/issues/114724)). Importing
  the same file consumers get makes this app a real test of it rather than a special case.

The native module is about 5.8 MB against 2.9 MB for a stock Blazor app — the difference is a PDF
engine, font subsetting, image decoding and deflate.

## What lives here

| | |
|---|---|
| `Components/ConverterPanel` | The converter: source, options, preview, download, diagnostics |
| `Components/ThemeToggle` | Light/dark switch |
| `Layout/MainLayout` | Header, footer, and the version / payload-size / RAM readouts |
| `Services/FontStore` | Fetches the six faces over HTTP and builds the `FontSet` |
| `Services/ConversionService` | Wraps `HtmlConverter`, collecting diagnostics |
| `wwwroot/` | Shell markup, stylesheet, interop, sample document |

## Fonts

krilla has no font database, so a conversion with nothing registered throws rather than quietly
producing a blank page. On a desktop that is one `AddDirectory` call; in a browser there is no
directory to read, so `FontStore` fetches each face over HTTP and hands the bytes to
`FontFace.Load`.

Six faces ship, not the full twelve: Liberation Sans in four styles, plus a serif and a monospace
regular so the other two generic families resolve to something of the right shape. Each face is
another download, and bold serif is a case the samples here do not reach.

They are linked out of `Krilla.Html.Tests/Fonts` rather than copied. These are the exact files the
corpus renders with, so this app draws what the test suite measures against Chrome, and a second
copy of 2.4 MB of binaries would only be a second thing to keep in step.

## Images

An `<img>` resolves to nothing unless it carries a `data:` URI, and the absence is reported like any
other unrenderable construct. Krilla never fetches over the network on any platform — a security
default rather than a gap — and a page that converts whatever is pasted into it is the last place to
relax it. There is no local disk to read either.

## Diagnostics

The result pane lists what the engine recognised and did not render the way a browser would:
`display: flex`, a presentational attribute, an image that resolved to nothing. An empty list is the
meaningful case, and it is what the app shows first — a conversion that reports nothing laid out
every construct in the document faithfully.

## Threading

The runtime is single-threaded; `WasmEnableThreads` is deliberately not set. The multithreaded
runtime needs `SharedArrayBuffer`, so a cross-origin-isolated page, which GitHub Pages does not
serve. A conversion therefore runs on the UI thread. `Task.Yield` lets the "converting" state paint
before the compute begins, which for a page or two of HTML is a blink.

## Tests

`Krilla.Web.Tests` runs two kinds, and the split matters:

- **bUnit**, on the desktop runtime, for component behaviour and the conversion path. Fast, and
  blind to anything about WebAssembly.
- **Playwright**, against the real published output, served from `bin/<config>/blazor-publish`. This
  is the only place a conversion runs in a browser against the trimmed, relinked build with the
  native actually inside the module. A P/Invoke into an archive the linker stripped fails nowhere
  else.

The page screenshots pin every face to the Liberation Sans the app already ships, because the
stylesheet's system font stack resolves differently on a Windows machine and a Linux runner — a
different typeface rather than sub-pixel drift, which would fail only on CI.

## Deployment

`.github/workflows/deploy-blazor.yml` builds the native, builds and runs the tests, publishes, and
pushes to GitHub Pages on every push to `main`. The base href is rewritten to the project subpath;
that step is deleted if the site ever moves to a custom domain served at its root.
