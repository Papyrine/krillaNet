//! C ABI over [krilla](https://github.com/LaurenzV/krilla), consumed by the `Krilla` .NET
//! package.
//!
//! The contract is documented for consumers in `docs/ffi-abi.md`. In short:
//!
//! - Every export returns an `i32` from [`status`]. Results travel through out-parameters,
//!   written only on [`status::OK`].
//! - Every allocation crossing the boundary is freed by the side that made it
//!   ([`handle`], rule R2). This is not stylistic: the Windows builds link the CRT statically,
//!   so the library has its own heap.
//! - No export may panic across the boundary. [`guard`] enforces that.
//! - Handles carry no locking and are not thread safe (rule R6).
//!
//! krilla itself is `#![forbid(unsafe_code)]`. This crate cannot be, so the `unsafe` it does
//! use is confined to [`document`] (lifetime erasure) and [`handle`] (pointer helpers), and
//! every block cites the numbered invariant or ownership rule it depends on.

#![deny(unsafe_op_in_unsafe_fn)]
// The highest-value lint here: it is what stops a `String`, `&str`, `Option<T>` or Rust enum
// reaching an `extern "C"` signature.
#![deny(improper_ctypes_definitions)]
#![warn(clippy::undocumented_unsafe_blocks)]
#![warn(missing_docs)]
// Every export takes raw pointers and dereferences them; that is what a C ABI is. The lint
// exists to push Rust-facing APIs towards `unsafe fn`, which would be meaningless here — C
// callers have no notion of it, and marking the functions `unsafe` changes nothing about what
// the boundary guarantees. The obligations are carried instead by the numbered ownership
// rules in `handle`, cited by every `// SAFETY:` comment and restated for consumers in
// `docs/ffi-abi.md`.
#![allow(clippy::not_unsafe_ptr_arg_deref)]

pub mod api;
pub mod document;
pub mod guard;
pub mod handle;
pub mod status;
pub mod types;

/// ABI revision, asserted by the managed static constructor.
///
/// Bump on any change to a `#[repr(C)]` struct, to the numeric value of a status or enum
/// constant, or to an existing export's signature. Adding a new export does not require a
/// bump — a managed assembly built against an older revision still works against a newer
/// library, which is the direction that actually occurs.
///
/// The check exists because a published package can never mismatch (managed and native ship
/// together) but a stale `src/Krilla/runtimes/` folder on a developer machine can, and does.
/// Without it that surfaces as an `AccessViolationException` with no useful stack.
pub const KRILLA_ABI_VERSION: u32 = 1;

/// Returns [`KRILLA_ABI_VERSION`].
#[unsafe(no_mangle)]
pub extern "C" fn krilla_abi_version() -> u32 {
    KRILLA_ABI_VERSION
}

/// Size in bytes of a mirrored `#[repr(C)]` struct, or 0 for an unknown kind.
///
/// The managed suite loops `0..KRILLA_ABI_KIND_COUNT` asserting this against
/// `Unsafe.SizeOf<T>()`. One export and one test, covering the failure mode that would
/// otherwise be silent memory corruption.
#[unsafe(no_mangle)]
pub extern "C" fn krilla_abi_sizeof(kind: i32) -> usize {
    types::size_of_kind(kind)
}

/// Number of valid `kind` values for [`krilla_abi_sizeof`].
#[unsafe(no_mangle)]
pub extern "C" fn krilla_abi_kind_count() -> i32 {
    types::ABI_KIND_COUNT
}
