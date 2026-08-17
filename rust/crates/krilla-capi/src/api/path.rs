//! Path building.

use krilla::geom::{Path, PathBuilder, Rect};

use crate::guard::ffi;
use crate::handle;
use crate::status;
use crate::types::KrillaRect;

/// A path builder handle.
///
/// krilla's `PathBuilder::finish` consumes `self`, which a C ABI cannot express, so the
/// builder lives behind an `Option` and is taken on finish. A builder that has been finished
/// reports `CONSUMED` rather than silently starting over.
pub struct KrillaPathBuilder {
    inner: Option<PathBuilder>,
}

impl KrillaPathBuilder {
    fn get(&mut self) -> Result<&mut PathBuilder, i32> {
        self.inner.as_mut().ok_or(status::CONSUMED)
    }
}

/// A finished, immutable path.
pub struct KrillaPath {
    pub(crate) inner: Path,
}

ffi! {
    /// Creates an empty path builder.
    fn krilla_path_builder_new(out: *mut *mut KrillaPathBuilder) {
        let builder = KrillaPathBuilder {
            inner: Some(PathBuilder::new()),
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(builder))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases a path builder. Safe on a builder already consumed by `finish`.
    fn krilla_path_builder_free(builder: *mut KrillaPathBuilder) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(builder) };
        Ok(status::OK)
    }
}

ffi! {
    /// Begins a new contour at the given point.
    fn krilla_path_builder_move_to(builder: *mut KrillaPathBuilder, x: f32, y: f32) {
        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(builder)? }.get()?.move_to(x, y);
        Ok(status::OK)
    }
}

ffi! {
    /// Adds a straight segment from the current point.
    fn krilla_path_builder_line_to(builder: *mut KrillaPathBuilder, x: f32, y: f32) {
        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(builder)? }.get()?.line_to(x, y);
        Ok(status::OK)
    }
}

ffi! {
    /// Adds a quadratic bezier from the current point.
    fn krilla_path_builder_quad_to(
        builder: *mut KrillaPathBuilder,
        x1: f32,
        y1: f32,
        x: f32,
        y: f32,
    ) {
        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(builder)? }.get()?.quad_to(x1, y1, x, y);
        Ok(status::OK)
    }
}

ffi! {
    /// Adds a cubic bezier from the current point.
    fn krilla_path_builder_cubic_to(
        builder: *mut KrillaPathBuilder,
        x1: f32,
        y1: f32,
        x2: f32,
        y2: f32,
        x: f32,
        y: f32,
    ) {
        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(builder)? }
            .get()?
            .cubic_to(x1, y1, x2, y2, x, y);
        Ok(status::OK)
    }
}

ffi! {
    /// Closes the current contour.
    fn krilla_path_builder_close(builder: *mut KrillaPathBuilder) {
        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(builder)? }.get()?.close();
        Ok(status::OK)
    }
}

ffi! {
    /// Adds a complete rectangular contour.
    fn krilla_path_builder_push_rect(builder: *mut KrillaPathBuilder, rect: KrillaRect) {
        let rect = Rect::try_from(rect)?;

        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(builder)? }.get()?.push_rect(rect);
        Ok(status::OK)
    }
}

ffi! {
    /// Finishes the builder into an immutable path.
    ///
    /// Consumes the builder: further calls against it return `CONSUMED`. The builder handle
    /// must still be released with `krilla_path_builder_free`.
    ///
    /// An empty path, or one whose geometry krilla rejects, yields `INVALID_GEOMETRY`.
    fn krilla_path_builder_finish(
        builder: *mut KrillaPathBuilder,
        out: *mut *mut KrillaPath,
    ) {
        // SAFETY: R1 — live handle.
        let builder = unsafe { handle::as_mut(builder)? };
        let inner = builder.inner.take().ok_or(status::CONSUMED)?;
        let path = inner.finish().ok_or(status::INVALID_GEOMETRY)?;

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(KrillaPath { inner: path }))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases a path.
    fn krilla_path_free(path: *mut KrillaPath) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(path) };
        Ok(status::OK)
    }
}
