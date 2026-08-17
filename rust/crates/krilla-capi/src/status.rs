//! Status codes returned by every export.
//!
//! Every `extern "C"` function in this crate returns one of these as an `i32`. Handles and
//! other results travel through out-parameters, which are written only on [`OK`].
//!
//! The numeric values are ABI. Changing one requires bumping `KRILLA_ABI_VERSION`.

/// The call succeeded. Out-parameters have been written.
pub const OK: i32 = 0;

// -- Argument validation ------------------------------------------------------------------

/// A required pointer argument was null.
pub const NULL_ARGUMENT: i32 = 1;
/// A numeric argument was outside its permitted range.
pub const INVALID_ARGUMENT: i32 = 2;
/// A string argument was not valid UTF-8.
pub const INVALID_UTF8: i32 = 3;
/// Geometry was rejected by krilla: a non-finite value, or a rectangle or size that is not
/// strictly positive. krilla models these as `Option`-returning constructors.
pub const INVALID_GEOMETRY: i32 = 4;

// -- Document and page state --------------------------------------------------------------

/// An operation needing an open page was called while no page was open.
pub const NO_OPEN_PAGE: i32 = 10;
/// A page was already open when `krilla_document_start_page` was called. Only one page may
/// be open at a time; this is krilla's own constraint, not the shim's.
pub const PAGE_ALREADY_OPEN: i32 = 11;
/// The page token passed does not match the currently open page, meaning the page it refers
/// to has since been closed.
pub const STALE_PAGE: i32 = 12;
/// The document has already been finished and can no longer be drawn into.
pub const FINISHED: i32 = 13;
/// A previous call panicked and the document was poisoned. Every operation other than
/// `krilla_document_free` will keep returning this.
pub const POISONED: i32 = 14;

// -- Surface state ------------------------------------------------------------------------

/// `pop` was called with no matching `push`.
pub const POP_UNDERFLOW: i32 = 20;
/// The push stack exceeded `MAX_PUSH_DEPTH`. Unbounded nesting would otherwise overflow the
/// stack inside krilla, which no amount of `catch_unwind` can recover from.
pub const DEPTH_LIMIT: i32 = 21;
/// A tagged content section was already open. krilla panics on a nested `start_tagged`.
pub const TAG_ALREADY_OPEN: i32 = 22;
/// `end_tagged` was called with no open tagged section.
pub const NO_OPEN_TAG: i32 = 23;

// -- Resources --------------------------------------------------------------------------

/// Font data could not be parsed, or the collection index was out of range.
pub const INVALID_FONT: i32 = 30;
/// Image data could not be decoded.
pub const INVALID_IMAGE: i32 = 31;
/// A handle was created by a different document than the one it was used with. krilla cannot
/// detect this itself and would silently emit an invalid PDF.
pub const WRONG_DOCUMENT: i32 = 32;
/// A builder handle was consumed by a previous call and can no longer be used.
pub const CONSUMED: i32 = 33;

// -- Output -----------------------------------------------------------------------------

/// `Document::finish` reported one or more errors. Retrieve them through the error object
/// written to the out-parameter.
pub const KRILLA_ERROR: i32 = 40;

// -- Catch-all --------------------------------------------------------------------------

/// A panic was caught at the ABI boundary. `krilla_last_error_message` carries the detail.
/// Always a bug, in this crate or in krilla.
pub const PANIC: i32 = 90;

/// Maximum surface push depth. krilla itself imposes no limit, and stack exhaustion is not
/// catchable on any platform, so the ceiling has to live here.
pub const MAX_PUSH_DEPTH: usize = 1024;

/// Maximum outline nesting depth, for the same reason.
pub const MAX_OUTLINE_DEPTH: usize = 128;
