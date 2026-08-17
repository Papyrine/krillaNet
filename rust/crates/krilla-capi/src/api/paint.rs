//! Paints: solid colours, the three gradient kinds, and tiling patterns.

use krilla::color::Color;
use krilla::paint::{LinearGradient, Paint, RadialGradient, Stop, SweepGradient};

use crate::guard::ffi;
use crate::handle;
use crate::status;
use crate::types::{KrillaColor, KrillaTransform, normalized, spread_method};

/// A paint handle.
///
/// krilla's `Paint` is opaque and built only through `From` conversions, so the shim offers
/// one constructor per source kind rather than a single tagged struct. Gradients carry a
/// variable-length stop list, which cannot travel in a plain-old-data struct anyway.
pub struct KrillaPaint {
    pub(crate) inner: Paint,
}

/// A colour stop, mirrored on the managed side.
#[repr(C)]
#[derive(Copy, Clone)]
#[allow(missing_docs)]
pub struct KrillaStop {
    pub offset: f32,
    pub color: KrillaColor,
    pub opacity: f32,
}

/// Converts a caller-supplied stop array.
///
/// # Safety
///
/// `stops`/`count` must describe a readable array for the duration of the call (rule R3).
unsafe fn stops(stops: *const KrillaStop, count: usize) -> Result<Vec<Stop>, i32> {
    if count == 0 {
        return Err(status::INVALID_ARGUMENT);
    }

    if stops.is_null() {
        return Err(status::NULL_ARGUMENT);
    }

    // SAFETY: R3 — caller guarantees `count` readable elements for the call.
    let raw = unsafe { std::slice::from_raw_parts(stops, count) };

    // krilla asserts that every stop shares one colour space, and an assert is a panic: it
    // would be caught by the boundary guard, but only after poisoning the document, leaving
    // the caller with an unrecoverable handle over what is really just a bad argument.
    // Rejecting it here keeps it a plain error.
    let space = raw[0].color.space;

    if raw.iter().any(|stop| stop.color.space != space) {
        crate::guard::set_last_error("every stop in a gradient must use the same colour space");
        return Err(status::INVALID_ARGUMENT);
    }

    raw.iter()
        .map(|stop| {
            Ok(Stop {
                offset: normalized(stop.offset)?,
                color: Color::try_from(stop.color)?,
                opacity: normalized(stop.opacity)?,
            })
        })
        .collect()
}

fn wrap(paint: Paint, out: *mut *mut KrillaPaint) -> Result<i32, i32> {
    // SAFETY: out-parameter contract; `write_out` null-checks.
    unsafe { handle::write_out(out, handle::into_handle(KrillaPaint { inner: paint }))? };
    Ok(status::OK)
}

ffi! {
    /// Creates a solid-colour paint.
    fn krilla_paint_new_color(color: KrillaColor, out: *mut *mut KrillaPaint) {
        let color = Color::try_from(color)?;
        wrap(color.into(), out)
    }
}

ffi! {
    /// Creates a linear gradient paint running from (`x1`, `y1`) to (`x2`, `y2`).
    ///
    /// All stops must share one colour space; krilla reports a mismatch at `finish`.
    #[allow(clippy::too_many_arguments)]
    fn krilla_paint_new_linear_gradient(
        x1: f32,
        y1: f32,
        x2: f32,
        y2: f32,
        transform: KrillaTransform,
        spread: i32,
        anti_alias: bool,
        stop_ptr: *const KrillaStop,
        stop_count: usize,
        out: *mut *mut KrillaPaint,
    ) {
        // SAFETY: R3 — forwarded to the caller's contract on the stop array.
        let stops = unsafe { stops(stop_ptr, stop_count)? };

        let gradient = LinearGradient {
            x1,
            y1,
            x2,
            y2,
            transform: transform.into(),
            spread_method: spread_method(spread)?,
            stops,
            anti_alias,
        };

        wrap(gradient.into(), out)
    }
}

ffi! {
    /// Creates a radial gradient paint between a start circle (`f*`) and an end circle (`c*`).
    ///
    /// krilla does not implement `Reflect` or `Repeat` for radial gradients and silently falls
    /// back to `Pad`.
    #[allow(clippy::too_many_arguments)]
    fn krilla_paint_new_radial_gradient(
        fx: f32,
        fy: f32,
        fr: f32,
        cx: f32,
        cy: f32,
        cr: f32,
        transform: KrillaTransform,
        spread: i32,
        anti_alias: bool,
        stop_ptr: *const KrillaStop,
        stop_count: usize,
        out: *mut *mut KrillaPaint,
    ) {
        // SAFETY: R3 — forwarded to the caller's contract on the stop array.
        let stops = unsafe { stops(stop_ptr, stop_count)? };

        let gradient = RadialGradient {
            fx,
            fy,
            fr,
            cx,
            cy,
            cr,
            transform: transform.into(),
            spread_method: spread_method(spread)?,
            stops,
            anti_alias,
        };

        wrap(gradient.into(), out)
    }
}

ffi! {
    /// Creates a sweep gradient paint about (`cx`, `cy`).
    ///
    /// Angles are in degrees, starting from the right and increasing counter-clockwise.
    #[allow(clippy::too_many_arguments)]
    fn krilla_paint_new_sweep_gradient(
        cx: f32,
        cy: f32,
        start_angle: f32,
        end_angle: f32,
        transform: KrillaTransform,
        spread: i32,
        anti_alias: bool,
        stop_ptr: *const KrillaStop,
        stop_count: usize,
        out: *mut *mut KrillaPaint,
    ) {
        // SAFETY: R3 — forwarded to the caller's contract on the stop array.
        let stops = unsafe { stops(stop_ptr, stop_count)? };

        let gradient = SweepGradient {
            cx,
            cy,
            start_angle,
            end_angle,
            transform: transform.into(),
            spread_method: spread_method(spread)?,
            stops,
            anti_alias,
        };

        wrap(gradient.into(), out)
    }
}

ffi! {
    /// Releases a paint.
    fn krilla_paint_free(paint: *mut KrillaPaint) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(paint) };
        Ok(status::OK)
    }
}
