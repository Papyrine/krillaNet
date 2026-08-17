//! Reusable content: sub-streams, graphics, masks and tiling patterns.
//!
//! All four are built by drawing into a *sub-surface* rather than the page. That is a second
//! nesting of the same borrow problem `document.rs` solves for pages, so the same technique
//! applies: the sub-stream owns its erased chain and is identified by a token.

use krilla::mask::{Mask, MaskType};
use krilla::paint::Pattern;
use krilla::stream::{Stream, StreamBuilder};
use krilla::surface::Surface;

use crate::api::paint::KrillaPaint;
use crate::document::KrillaDocument;
use crate::guard::{ffi, ffi_doc};
use crate::handle;
use crate::status;
use crate::types::KrillaTransform;

/// A finished sub-stream, ready to become a graphic, mask or pattern.
///
/// Carries the id of the document that produced it. krilla documents that a stream used in a
/// different document yields an invalid PDF, and cannot detect the mistake itself — the
/// output simply references objects that do not exist. The id check turns that into an error.
pub struct KrillaStream {
    pub(crate) inner: Option<Stream>,
    pub(crate) document_id: u64,
}

/// A reusable drawing, cheap to draw repeatedly: krilla emits it once and references it.
pub struct KrillaGraphic {
    pub(crate) inner: krilla::graphic::Graphic,
    pub(crate) document_id: u64,
}

impl KrillaStream {
    fn take(&mut self, document_id: u64) -> Result<Stream, i32> {
        if self.document_id != document_id {
            return Err(status::WRONG_DOCUMENT);
        }

        self.inner.take().ok_or(status::CONSUMED)
    }
}

fn mask_type(value: i32) -> Result<MaskType, i32> {
    match value {
        0 => Ok(MaskType::Luminosity),
        1 => Ok(MaskType::Alpha),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

ffi_doc! {
    /// Opens a sub-stream on the open page, writing its token to `out_token`.
    ///
    /// Drawing calls that take a page token accept a sub-stream token equally, so the whole
    /// surface API is available inside one. Close it with `krilla_stream_finish`.
    fn krilla_stream_begin(doc, token: u64, out_token: *mut u64) {
        let sub = doc.begin_stream(token)?;

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out_token, sub)? };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Closes a sub-stream and yields the finished stream.
    fn krilla_stream_finish(doc, token: u64, out: *mut *mut KrillaStream) {
        let stream = doc.finish_stream(token)?;

        let handle_value = KrillaStream {
            inner: Some(stream),
            document_id: doc.id,
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(handle_value))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases a stream.
    fn krilla_stream_free(stream: *mut KrillaStream) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(stream) };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Turns a stream into a reusable graphic, consuming the stream.
    ///
    /// `isolated` gives the graphic its own transparency group, so blending inside it does
    /// not interact with what is already on the page.
    fn krilla_graphic_new(
        doc,
        stream: *mut KrillaStream,
        isolated: bool,
        out: *mut *mut KrillaGraphic,
    ) {
        // SAFETY: R1 — live handle.
        let stream = unsafe { handle::as_mut(stream)? };
        let inner = stream.take(doc.id)?;

        let graphic = KrillaGraphic {
            inner: krilla::graphic::Graphic::new(inner, isolated),
            document_id: doc.id,
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(graphic))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases a graphic.
    fn krilla_graphic_free(graphic: *mut KrillaGraphic) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(graphic) };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Draws a graphic. Repeating this is cheap in output size.
    fn krilla_surface_draw_graphic(doc, token: u64, graphic: *const KrillaGraphic) {
        // SAFETY: R1 — live handle.
        let graphic = unsafe { handle::as_ref(graphic)? };

        if graphic.document_id != doc.id {
            return Err(status::WRONG_DOCUMENT);
        }

        let value = graphic.inner.clone();
        doc.surface_mut(token)?.draw_graphic(value);
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Pushes a soft mask built from a stream, consuming the stream. Requires a matching pop.
    ///
    /// `mask_type` is 0 for luminosity — where the mask's brightness becomes opacity — or 1
    /// for alpha.
    fn krilla_surface_push_mask(doc, token: u64, kind: i32, stream: *mut KrillaStream) {
        let kind = mask_type(kind)?;

        // SAFETY: R1 — live handle.
        let stream = unsafe { handle::as_mut(stream)? };
        let inner = stream.take(doc.id)?;

        doc.push(token)?;
        doc.surface_mut(token)?.push_mask(Mask::new(inner, kind));
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Creates a tiling-pattern paint from a stream, consuming the stream.
    ///
    /// The stream is repeated on a `width` x `height` grid, with `transform` applied to the
    /// pattern space.
    fn krilla_paint_new_pattern(
        doc,
        stream: *mut KrillaStream,
        transform: KrillaTransform,
        width: f32,
        height: f32,
        out: *mut *mut KrillaPaint,
    ) {
        // Both bounds must be finite and strictly positive; a NaN would otherwise slip past a
        // plain `<= 0.0` check.
        if !width.is_finite() || !height.is_finite() || width <= 0.0 || height <= 0.0 {
            return Err(status::INVALID_GEOMETRY);
        }

        // SAFETY: R1 — live handle.
        let stream = unsafe { handle::as_mut(stream)? };
        let inner = stream.take(doc.id)?;

        let pattern = Pattern {
            stream: inner,
            transform: transform.into(),
            width,
            height,
        };

        let paint = KrillaPaint {
            inner: pattern.into(),
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(paint))? };
        Ok(status::OK)
    }
}

/// Opens a sub-surface on `parent`, erasing lifetimes the same way `document.rs` does.
///
/// # Safety
///
/// `parent` must remain live and untouched until the returned pair is dropped, and the
/// builder must be dropped before the surface it produced.
pub(crate) unsafe fn open_sub_stream(
    parent: &mut Surface<'static>,
) -> (*mut StreamBuilder<'static>, *mut Surface<'static>) {
    let builder = parent.stream_builder();

    // SAFETY: the builder borrows `*parent`, which is boxed at a stable address and which
    // nothing touches while this sub-stream is open — `surface_mut` hands out the innermost
    // open surface, never a parent. Erasing to 'static is sound because the erased value is
    // destroyed before the surface it borrows (the same I3 drop-order rule as pages).
    let builder: StreamBuilder<'static> =
        unsafe { std::mem::transmute::<StreamBuilder<'_>, StreamBuilder<'static>>(builder) };
    let builder = Box::into_raw(Box::new(builder));

    // SAFETY: `builder` was just created above and is live; no other reference exists yet.
    let surface = unsafe { (*builder).surface() };

    // SAFETY: same argument as the builder transmute; the surface is destroyed first.
    let surface: Surface<'static> =
        unsafe { std::mem::transmute::<Surface<'_>, Surface<'static>>(surface) };

    (builder, Box::into_raw(Box::new(surface)))
}

/// Closes a sub-surface opened by [`open_sub_stream`].
///
/// # Safety
///
/// Both pointers must come from one [`open_sub_stream`] call and must not be used again.
pub(crate) unsafe fn close_sub_stream(
    builder: *mut StreamBuilder<'static>,
    surface: *mut Surface<'static>,
) -> Stream {
    // Surface before builder, mirroring I3.
    // SAFETY: caller contract — both from `open_sub_stream`, being surrendered here.
    unsafe {
        drop(Box::from_raw(surface));
        Box::from_raw(builder).finish()
    }
}

/// Re-exported so `document.rs` can name the type without importing the whole module.
pub(crate) type SubStreamBuilder = StreamBuilder<'static>;

/// Unused marker keeping `KrillaDocument` in scope for the `ffi_doc!` expansions above.
#[allow(dead_code)]
type DocumentAlias = KrillaDocument;
