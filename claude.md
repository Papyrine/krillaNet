# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Krilla is a .NET wrapper over [krilla](https://github.com/LaurenzV/krilla), the Rust library that writes PDFs for typst. Unlike every other project in Papyrine, this one contains a native component it builds itself: krilla has no C API, so `rust/crates/krilla-capi` is a `cdylib` shim written here, and `src/Krilla` P/Invokes it. The public API lives in the `Krilla` namespace with `KrillaDocument` as the entry point.

## Build & Test Commands

Tests use **TUnit**, not VSTest. `dotnet test` is unsupported on the .NET 10 SDK and will error. Use `dotnet run`, and TUnit's `--treenode-filter` (not `--filter`) to narrow:

```bash
# Managed build. Shells out to cargo automatically when the native is missing or stale.
dotnet build src --configuration Release

# Managed tests
dotnet run --project src/Krilla.Tests --configuration Release

# One class
dotnet run --project src/Krilla.Tests --configuration Release -- --treenode-filter "/*/*/DocumentTests/*"

# Rust tests
cargo test --manifest-path rust/Cargo.toml

# Rust tests under Miri. This is the check that matters for src/document.rs; run it after
# any change to the lifetime-erasure code.
cargo +nightly miri test --manifest-path rust/Cargo.toml
```

Unlike the other Papyrine repositories, this one does not reference SponsorCheck, so a `Release` build needs no GitHub token and packs without extra arguments.

Two MSBuild switches exist for CI and are worth knowing locally:

- `-p:KrillaSkipCargo=true` — the native already exists; do not shell out to cargo. Used wherever the binary was built separately with specific `RUSTFLAGS` that a rebuild would drop.
- `-p:KrillaRequireAllNatives=true` — a missing RID becomes an error instead of a warning. Set only in the release pack job, so a partial matrix can never produce a publishable package.

## CI

Four pipelines, each with a distinct job:

- **`.github/workflows/rust.yml`** — fmt, clippy (warnings are errors), tests, Miri, `cargo deny`, and a check that `THIRD-PARTY-NOTICES.md` is current. Runs on every push touching `rust/`. Also greps for `#[unsafe(no_mangle)]` outside `guard.rs`: a hand-written export would bypass the `catch_unwind` boundary and silently reintroduce process-abort-on-panic.
- **`.github/workflows/native.yml`** — the eight-RID cross-compilation matrix, `workflow_call` only. Each leg statically verifies its own output; see the header comment for why each check exists.
- **`.github/workflows/publish-nuget.yml`** — calls `native.yml`, packs all eight natives into one package, runs `IntegrationTests` against the real nupkg on four runners plus Alpine and Debian 12 containers, then publishes via nuget.org Trusted Publishing (OIDC). Needs the `NUGET_USER` variable and a trusted-publishing policy registered before the first tag.
- **`src/appveyor.yml`** — fast Windows feedback. Builds `win-x64` itself so it stays self-contained.

`IntegrationTests/` consumes the *packed* package rather than a project reference, which is the only way to test that `runtimes/<rid>/native/` resolves at all. Its `nuget.config` pins `Krilla` to the local `../nugets` feed via `packageSourceMapping` — without that, NuGet sees the same version in both feeds and reliably picks nuget.org on CI, testing the last release instead of the current build.

## Licensing

The package is a binary distribution of ~94 statically linked crates, so their licences are an obligation on everyone who ships it. `rust/deny.toml` gates what may enter the tree; `rust/about.toml` + `about.hbs` generate `src/Krilla/THIRD-PARTY-NOTICES.md`, which is committed and CI-checked for drift.

Two `RUSTSEC` advisories are ignored in `deny.toml`, both *unmaintained* notices rather than vulnerabilities, on crates krilla chose (`ttf-parser`, `rustybuzz`) with no upgrade available. They are ignored individually rather than by disabling the advisories check, so a real vulnerability anywhere still fails the build.

## Architecture

### The native shim (`rust/crates/krilla-capi`)

- **`document.rs`** is the only module containing unsafe code, and the only one whose correctness rests on an argument rather than the type system. krilla models PDF construction as nested mutable borrows (`Document -> Page<'a> -> Surface<'a>`), which C cannot express, so one handle owns the whole chain with the lifetimes erased. Invariants I1–I4 are stated in the module header. **Verify changes here with Miri**, not review: Stacked Borrows violations are invisible to reading.
- **`guard.rs`** owns the ABI boundary. Every export goes through the `ffi!`/`ffi_doc!` macros, which wrap the body in `catch_unwind` and poison the document on a panic. `#[unsafe(no_mangle)]` appears *only* in this file's macro definitions; a hand-written export would silently reintroduce process-abort-on-panic.
- **`handle.rs`** carries the ownership rules R1–R6 that the rest of the crate's `// SAFETY:` comments cite by number.
- **`types.rs`** holds the `#[repr(C)]` structs, each with a `const` layout assertion. `api/` holds the exports, kept separate so the unsafe core stays small.

### Traps in krilla that the shim has to work around

These are not documented upstream and were found by reading the source. Do not remove the guards:

- **`Surface`'s `Drop` asserts** an empty push stack, no remaining sub-builders, and no open marked-content section. A panic inside `drop` during unwinding aborts the process regardless of `catch_unwind`, so `close_page` tracks push depth and tagged state itself and rebalances before dropping.
- **`Surface::pop`** unwraps an empty stack. `document.pop()` checks first.
- **`draw_glyphs`** slices the run text by byte range and panics off a UTF-8 character boundary. Validated in `api/text.rs`.
- **`Image::from_rgba8`** panics when the buffer length is not `width * height * 4`. Checked in `api/image.rs`.
- **Glyph advances must be pre-divided by units-per-em.** krilla does not check this and produces plausible but mis-spaced output. `Surface.DrawGlyphs` normalises, so the trap is unreachable from C#.

### The managed side (`src/Krilla`)

Mirrors Morph.PDFium: `KrillaNative.*.cs` partials of one `static partial class` in the global namespace, `[LibraryImport]` throughout, public types in the `Krilla` namespace, blittable mirrors in `Interop/`. The assembly sets `DisableRuntimeMarshalling`, so every ABI struct must stay plain-old-data.

`AbiTests` loops `krilla_abi_sizeof` over every mirrored struct. A layout disagreement between Rust and C# is the one bug class here that corrupts memory rather than throwing, so that test earns its keep.

## Things that will surprise you

- **The RID table is duplicated in three places** and all three must stay in sync: `src/Krilla.Native.props` (the MSBuild table), `.github/workflows/native.yml` (the build matrix), and `readme.md` (documented support).
- **Every allocation crossing the ABI is freed by the side that made it.** The Windows natives build with `+crt-static` — required, or the DLL imports `VCRUNTIME140.dll`, which is not present on a clean Windows install — and that gives the library its own heap. Freeing a Rust allocation from managed code corrupts it.
- **`.editorconfig` at the repo root is generated**, overwritten by ProjectDefaults on every build. Never hand-edit it. `rust/.editorconfig` sets `root = true` to keep it out of the Rust tree.
- **`Krilla.Native.targets` must never be packed** into `build/` or `buildTransitive/`. It shells out to cargo, and packing it would make every consumer's restore require rustup.
- **`%(Identity)` is illegal in a project-scope ItemGroup condition** (MSB4190), which is why `KrillaResolveHostNative` is a target rather than plain evaluation.
- **AppVeyor produces a win-x64-only package** and must never publish it. The release natives come from the GitHub Actions matrix.
- **SBOM under-reports.** `Microsoft.Sbom.Targets` sees no dependencies while ~110 Rust crates are statically linked; a separate Rust SBOM ships alongside.

## Package Management

Central Package Management; versions live in `src/Directory.Packages.props`. Rust dependencies are pinned exactly (`=0.8.2`) because the test suite compares PDF bytes, so a patch bump is a deliberate, baseline-regenerating change.
