//! SVG documents, parsed by usvg and drawn through krilla-svg.
//!
//! The only optional part of this ABI, which is why it is the only module that comes in two
//! halves. [`krilla_svg_supported`] is compiled unconditionally so a managed caller can ask
//! before it calls; everything that needs the dependency lives in [`enabled`], behind the
//! `svg` feature, and is simply absent from a library built without it.
//!
//! The alternative — stub exports returning [`status::UNSUPPORTED`] — was rejected because it
//! doubles every signature for a case that only arises when somebody deliberately builds with
//! `--no-default-features`. One probe answers the same question once.

use crate::guard::ffi;
use crate::handle;
use crate::status;

#[cfg(feature = "svg")]
mod enabled;

#[cfg(feature = "svg")]
pub use enabled::*;

ffi! {
    /// Writes 1 when the library was built with SVG support and 0 when it was not.
    ///
    /// The managed side checks this before the first `krilla_svg_*` call, so a library built
    /// without the feature reports itself rather than surfacing as a missing entry point. Same
    /// reasoning as `krilla_abi_version`: the mismatch cannot happen in a published package,
    /// where managed and native ship together, and does happen against a hand-built native.
    fn krilla_svg_supported(out: *mut u32) {
        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, u32::from(cfg!(feature = "svg")))? };
        Ok(status::OK)
    }
}
