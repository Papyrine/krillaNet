//! Drawing onto the open page's surface.
//!
//! Every export here takes the page token issued by `krilla_document_start_page`, so a call
//! aimed at a page that has since been closed is refused rather than misdirected.

use krilla::paint::{Fill, Stroke, StrokeDash};

use crate::api::paint::KrillaPaint;
use crate::api::path::KrillaPath;
use crate::guard::ffi_doc;
use crate::handle;
use crate::status;
use crate::types::{
    KrillaFill, KrillaPoint, KrillaStroke, KrillaTransform, fill_rule, line_cap, line_join,
    location, normalized,
};

ffi_doc! {
    /// Sets the fill used by subsequent drawing operations.
    ///
    /// Passing a null paint clears the fill. With neither fill nor stroke set, krilla falls
    /// back to filling black rather than drawing nothing.
    fn krilla_surface_set_fill(doc, token: u64, paint: *const KrillaPaint, fill: KrillaFill) {
        let value = if paint.is_null() {
            None
        } else {
            // SAFETY: R1 — non-null and live, checked immediately above.
            let paint = unsafe { handle::as_ref(paint)? };

            Some(Fill {
                paint: paint.inner.clone(),
                opacity: normalized(fill.opacity)?,
                rule: fill_rule(fill.rule)?,
            })
        };

        doc.surface_mut(token)?.set_fill(value);
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Sets the stroke used by subsequent drawing operations.
    ///
    /// Passing a null paint clears the stroke.
    fn krilla_surface_set_stroke(doc, token: u64, paint: *const KrillaPaint, stroke: KrillaStroke) {
        let value = if paint.is_null() {
            None
        } else {
            // SAFETY: R1 — non-null and live, checked immediately above.
            let paint = unsafe { handle::as_ref(paint)? };

            let dash = if stroke.dash_len == 0 {
                None
            } else {
                if stroke.dash_array.is_null() {
                    return Err(status::NULL_ARGUMENT);
                }

                // SAFETY: R3 — the caller guarantees `dash_len` readable floats for the
                // duration of the call. Copied into the owned `StrokeDash` below, so nothing
                // outlives the borrow.
                let array =
                    unsafe { std::slice::from_raw_parts(stroke.dash_array, stroke.dash_len) };

                Some(StrokeDash {
                    array: array.to_vec(),
                    offset: stroke.dash_offset,
                })
            };

            Some(Stroke {
                paint: paint.inner.clone(),
                width: stroke.width,
                miter_limit: stroke.miter_limit,
                line_cap: line_cap(stroke.line_cap)?,
                line_join: line_join(stroke.line_join)?,
                opacity: normalized(stroke.opacity)?,
                dash,
            })
        };

        doc.surface_mut(token)?.set_stroke(value);
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Draws a path with the currently active fill and/or stroke.
    fn krilla_surface_draw_path(doc, token: u64, path: *const KrillaPath) {
        // SAFETY: R1 — live handle.
        let path = unsafe { handle::as_ref(path)? };
        doc.surface_mut(token)?.draw_path(&path.inner);
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Concatenates a transform onto the current matrix. Requires a matching `pop`.
    fn krilla_surface_push_transform(doc, token: u64, transform: KrillaTransform) {
        doc.push(token)?;
        doc.surface_mut(token)?.push_transform(&transform.into());
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Intersects the drawing area with a clip path. Requires a matching `pop`.
    fn krilla_surface_push_clip_path(doc, token: u64, path: *const KrillaPath, rule: i32) {
        // SAFETY: R1 — live handle.
        let path = unsafe { handle::as_ref(path)? };
        let rule = fill_rule(rule)?;

        doc.push(token)?;
        doc.surface_mut(token)?.push_clip_path(&path.inner, &rule);
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Applies a base opacity to subsequent objects. Requires a matching `pop`.
    ///
    /// Stacking multiplies: pushing 0.5 twice yields 0.25.
    fn krilla_surface_push_opacity(doc, token: u64, opacity: f32) {
        let opacity = normalized(opacity)?;

        doc.push(token)?;
        doc.surface_mut(token)?.push_opacity(opacity);
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Starts an isolated transparency group. Requires a matching `pop`.
    fn krilla_surface_push_isolated(doc, token: u64) {
        doc.push(token)?;
        doc.surface_mut(token)?.push_isolated();
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Reverts the most recent `push`.
    ///
    /// Returns `POP_UNDERFLOW` rather than calling through: krilla's own `pop` unwraps an
    /// empty stack, and the resulting panic would land inside a `Drop` during unwinding.
    fn krilla_surface_pop(doc, token: u64) {
        doc.pop(token)?;
        doc.surface_mut(token)?.pop();
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Writes the surface's current transformation matrix.
    fn krilla_surface_current_transform(doc, token: u64, out: *mut KrillaTransform) {
        let transform = doc.surface_mut(token)?.cur_transform();

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, transform.into())? };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Tags subsequent operations with a caller-chosen location, echoed back in validation
    /// errors so a failure can be traced to a place in the caller's own document.
    ///
    /// A location of 0 resets instead of setting, since krilla models these as `NonZeroU64`.
    fn krilla_surface_set_location(doc, token: u64, value: u64) {
        let surface = doc.surface_mut(token)?;

        match location(value) {
            Some(value) => surface.set_location(value),
            None => surface.reset_location(),
        }

        Ok(status::OK)
    }
}

ffi_doc! {
    /// Draws a point-sized marker; used only to keep `KrillaPoint` exercised in the ABI test
    /// suite until text drawing lands. Not part of the published surface.
    #[doc(hidden)]
    fn krilla_surface_noop_point(doc, token: u64, point: KrillaPoint) {
        let _ = doc.surface_mut(token)?;
        let _: krilla::geom::Point = point.into();
        Ok(status::OK)
    }
}
