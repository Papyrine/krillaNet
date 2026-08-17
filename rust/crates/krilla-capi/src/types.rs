//! The `#[repr(C)]` structs mirrored by the managed `Interop/` types.
//!
//! Every struct here is paired with a compile-time layout assertion and a runtime size that
//! `krilla_abi_sizeof` reports. The managed test suite loops the kind constants and compares
//! against `Unsafe.SizeOf<T>()`, so a mismatch between the two sides fails a test rather than
//! corrupting memory.
//!
//! Handles are deliberately kept *out* of these structs. Passing an opaque pointer alongside
//! a by-value struct keeps the structs pure plain-old-data, which is what lets
//! `DisableRuntimeMarshalling` apply on the managed side.
//!
//! Field-level docs are switched off throughout. These are transparent coordinate and
//! parameter carriers whose fields are named exactly as the surrounding type documents them,
//! and krilla makes the same call on its own `Point` and `Transform`. The meaning lives on
//! the struct, where a reader will actually look for it.
#![allow(missing_docs)]

use krilla::color::{cmyk, luma, rgb};
use krilla::geom::{Point, Rect, Size, Transform};
use krilla::num::NormalizedF32;
use krilla::paint::{FillRule, LineCap, LineJoin, SpreadMethod};

use crate::status;

/// A point in surface space. The origin is the top-left corner.
#[repr(C)]
#[derive(Copy, Clone)]
pub struct KrillaPoint {
    pub x: f32,
    pub y: f32,
}

impl From<KrillaPoint> for Point {
    fn from(value: KrillaPoint) -> Self {
        Point::from_xy(value.x, value.y)
    }
}

/// A width/height pair. Both must be strictly positive.
#[repr(C)]
#[derive(Copy, Clone)]
pub struct KrillaSize {
    pub width: f32,
    pub height: f32,
}

impl TryFrom<KrillaSize> for Size {
    type Error = i32;

    fn try_from(value: KrillaSize) -> Result<Self, Self::Error> {
        Size::from_wh(value.width, value.height).ok_or(status::INVALID_GEOMETRY)
    }
}

/// A rectangle. `right` must exceed `left` and `bottom` must exceed `top`.
#[repr(C)]
#[derive(Copy, Clone)]
pub struct KrillaRect {
    pub left: f32,
    pub top: f32,
    pub right: f32,
    pub bottom: f32,
}

impl TryFrom<KrillaRect> for Rect {
    type Error = i32;

    fn try_from(value: KrillaRect) -> Result<Self, Self::Error> {
        Rect::from_ltrb(value.left, value.top, value.right, value.bottom)
            .ok_or(status::INVALID_GEOMETRY)
    }
}

impl From<Rect> for KrillaRect {
    fn from(value: Rect) -> Self {
        Self {
            left: value.left(),
            top: value.top(),
            right: value.right(),
            bottom: value.bottom(),
        }
    }
}

/// An affine transform, in krilla's row order.
///
/// Unlike the other geometry types this one is never rejected: krilla inherits Skia's quirk
/// of accepting degenerate and non-finite matrices.
#[repr(C)]
#[derive(Copy, Clone)]
pub struct KrillaTransform {
    pub sx: f32,
    pub ky: f32,
    pub kx: f32,
    pub sy: f32,
    pub tx: f32,
    pub ty: f32,
}

impl From<KrillaTransform> for Transform {
    fn from(value: KrillaTransform) -> Self {
        Transform::from_row(value.sx, value.ky, value.kx, value.sy, value.tx, value.ty)
    }
}

impl From<Transform> for KrillaTransform {
    fn from(value: Transform) -> Self {
        Self {
            sx: value.sx(),
            ky: value.ky(),
            kx: value.kx(),
            sy: value.sy(),
            tx: value.tx(),
            ty: value.ty(),
        }
    }
}

/// Colour space discriminants for [`KrillaColor::space`].
pub const COLOR_SPACE_RGB: i32 = 0;
pub const COLOR_SPACE_LUMA: i32 = 1;
pub const COLOR_SPACE_CMYK: i32 = 2;

/// A device colour.
///
/// Components are 8-bit because that is krilla's own constructor signature for all three
/// spaces. Unused components are ignored rather than validated: `luma` reads `components[0]`,
/// `rgb` reads the first three, `cmyk` reads all four.
///
/// Separation (spot) colours are not representable here — they carry a colorant name and a
/// fallback colour, so they go through an opaque paint handle instead.
#[repr(C)]
#[derive(Copy, Clone)]
pub struct KrillaColor {
    pub space: i32,
    pub components: [u8; 4],
}

impl TryFrom<KrillaColor> for krilla::color::Color {
    type Error = i32;

    fn try_from(value: KrillaColor) -> Result<Self, Self::Error> {
        let [c0, c1, c2, c3] = value.components;
        match value.space {
            COLOR_SPACE_RGB => Ok(rgb::Color::new(c0, c1, c2).into()),
            COLOR_SPACE_LUMA => Ok(luma::Color::new(c0).into()),
            COLOR_SPACE_CMYK => Ok(cmyk::Color::new(c0, c1, c2, c3).into()),
            _ => Err(status::INVALID_ARGUMENT),
        }
    }
}

/// Fill parameters. The paint travels separately as an opaque handle.
#[repr(C)]
#[derive(Copy, Clone)]
pub struct KrillaFill {
    pub opacity: f32,
    /// 0 = non-zero, 1 = even-odd.
    pub rule: i32,
}

/// Stroke parameters. The paint travels separately as an opaque handle.
///
/// `dash_array` is borrowed for the duration of the call (rule R3); the shim copies it into
/// the `StrokeDash` it builds.
#[repr(C)]
#[derive(Copy, Clone)]
pub struct KrillaStroke {
    pub width: f32,
    pub miter_limit: f32,
    /// 0 = butt, 1 = round, 2 = square.
    pub line_cap: i32,
    /// 0 = miter, 1 = round, 2 = bevel.
    pub line_join: i32,
    pub opacity: f32,
    pub dash_offset: f32,
    pub dash_array: *const f32,
    pub dash_len: usize,
}

/// One positioned glyph.
///
/// `x_advance`, `x_offset`, `y_offset` and `y_advance` must already be divided by the font's
/// units-per-em. krilla requires this and does not check it; getting it wrong produces
/// plausible-looking output with the wrong spacing. The managed wrapper normalises on the
/// caller's behalf so this trap is not reachable from C#.
///
/// `text_start`/`text_end` are byte offsets into the `text` argument of the same draw call.
/// They must fall on UTF-8 character boundaries — the shim validates this, because krilla
/// slices the string directly and would otherwise panic.
#[repr(C)]
#[derive(Copy, Clone)]
pub struct KrillaGlyph {
    pub glyph_id: u32,
    pub text_start: u32,
    pub text_end: u32,
    pub x_advance: f32,
    pub x_offset: f32,
    pub y_offset: f32,
    pub y_advance: f32,
    /// Caller-chosen tag echoed back in validation errors. 0 means none.
    pub location: u64,
}

/// Bit flags for [`KrillaPageSettings::present`].
pub const PAGE_BOX_MEDIA: u32 = 1 << 0;
pub const PAGE_BOX_CROP: u32 = 1 << 1;
pub const PAGE_BOX_BLEED: u32 = 1 << 2;
pub const PAGE_BOX_TRIM: u32 = 1 << 3;
pub const PAGE_BOX_ART: u32 = 1 << 4;

/// Page geometry.
///
/// The optional boxes are always present in the struct; `present` says which ones carry
/// meaning. A sentinel value would not work here because any float is a legal box edge.
///
/// Page labels are set separately: they carry a string prefix, which cannot live in a
/// plain-old-data struct.
#[repr(C)]
#[derive(Copy, Clone)]
pub struct KrillaPageSettings {
    pub width: f32,
    pub height: f32,
    pub media_box: KrillaRect,
    pub crop_box: KrillaRect,
    pub bleed_box: KrillaRect,
    pub trim_box: KrillaRect,
    pub art_box: KrillaRect,
    pub present: u32,
    pub reserved: u32,
}

/// Converts a caller-supplied opacity into krilla's clamped 0..=1 type.
pub fn normalized(value: f32) -> Result<NormalizedF32, i32> {
    NormalizedF32::new(value).ok_or(status::INVALID_ARGUMENT)
}

/// Maps a `Location` tag, treating 0 as absent. krilla models these as `NonZeroU64`, so the
/// sentinel is free.
pub fn location(value: u64) -> Option<krilla::surface::Location> {
    krilla::surface::Location::new(value)
}

pub fn fill_rule(value: i32) -> Result<FillRule, i32> {
    match value {
        0 => Ok(FillRule::NonZero),
        1 => Ok(FillRule::EvenOdd),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

pub fn line_cap(value: i32) -> Result<LineCap, i32> {
    match value {
        0 => Ok(LineCap::Butt),
        1 => Ok(LineCap::Round),
        2 => Ok(LineCap::Square),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

pub fn line_join(value: i32) -> Result<LineJoin, i32> {
    match value {
        0 => Ok(LineJoin::Miter),
        1 => Ok(LineJoin::Round),
        2 => Ok(LineJoin::Bevel),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

pub fn spread_method(value: i32) -> Result<SpreadMethod, i32> {
    match value {
        0 => Ok(SpreadMethod::Pad),
        1 => Ok(SpreadMethod::Reflect),
        2 => Ok(SpreadMethod::Repeat),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

/// Kind discriminants for `krilla_abi_sizeof`. Kept dense so the managed test can loop them.
pub const ABI_KIND_POINT: i32 = 0;
pub const ABI_KIND_SIZE: i32 = 1;
pub const ABI_KIND_RECT: i32 = 2;
pub const ABI_KIND_TRANSFORM: i32 = 3;
pub const ABI_KIND_COLOR: i32 = 4;
pub const ABI_KIND_FILL: i32 = 5;
pub const ABI_KIND_STROKE: i32 = 6;
pub const ABI_KIND_GLYPH: i32 = 7;
pub const ABI_KIND_PAGE_SETTINGS: i32 = 8;
pub const ABI_KIND_STOP: i32 = 9;
pub const ABI_KIND_DATE_TIME: i32 = 10;
pub const ABI_KIND_DOCUMENT_OPTIONS: i32 = 11;
/// One past the last valid kind.
pub const ABI_KIND_COUNT: i32 = 12;

/// Size in bytes of the mirrored struct for `kind`, or 0 for an unknown kind.
pub fn size_of_kind(kind: i32) -> usize {
    use std::mem::size_of;

    match kind {
        ABI_KIND_POINT => size_of::<KrillaPoint>(),
        ABI_KIND_SIZE => size_of::<KrillaSize>(),
        ABI_KIND_RECT => size_of::<KrillaRect>(),
        ABI_KIND_TRANSFORM => size_of::<KrillaTransform>(),
        ABI_KIND_COLOR => size_of::<KrillaColor>(),
        ABI_KIND_FILL => size_of::<KrillaFill>(),
        ABI_KIND_STROKE => size_of::<KrillaStroke>(),
        ABI_KIND_GLYPH => size_of::<KrillaGlyph>(),
        ABI_KIND_PAGE_SETTINGS => size_of::<KrillaPageSettings>(),
        ABI_KIND_STOP => size_of::<crate::api::paint::KrillaStop>(),
        ABI_KIND_DATE_TIME => size_of::<crate::api::metadata::KrillaDateTime>(),
        ABI_KIND_DOCUMENT_OPTIONS => size_of::<crate::api::metadata::KrillaDocumentOptions>(),
        _ => 0,
    }
}

// Layout is ABI. These assertions turn an accidental field reorder or type change into a
// compile error, which is considerably cheaper than the managed-side memory corruption it
// would otherwise cause. The pointer-bearing structs are asserted for 64-bit only, since
// that is the whole shipped RID matrix.
const _: () = {
    use std::mem::{align_of, offset_of, size_of};

    assert!(size_of::<KrillaPoint>() == 8 && align_of::<KrillaPoint>() == 4);
    assert!(size_of::<KrillaSize>() == 8 && align_of::<KrillaSize>() == 4);
    assert!(size_of::<KrillaRect>() == 16 && align_of::<KrillaRect>() == 4);
    assert!(size_of::<KrillaTransform>() == 24 && align_of::<KrillaTransform>() == 4);

    assert!(size_of::<KrillaColor>() == 8 && align_of::<KrillaColor>() == 4);
    assert!(offset_of!(KrillaColor, components) == 4);

    assert!(size_of::<KrillaFill>() == 8 && align_of::<KrillaFill>() == 4);

    assert!(size_of::<KrillaGlyph>() == 40 && align_of::<KrillaGlyph>() == 8);
    // The u64 forces four bytes of tail padding after `y_advance`; pinning the offset keeps
    // that explicit rather than incidental.
    assert!(offset_of!(KrillaGlyph, location) == 32);

    assert!(size_of::<KrillaPageSettings>() == 96 && align_of::<KrillaPageSettings>() == 4);
    assert!(offset_of!(KrillaPageSettings, media_box) == 8);
    assert!(offset_of!(KrillaPageSettings, present) == 88);

    assert!(size_of::<usize>() == 8, "only 64-bit targets are shipped");
    assert!(size_of::<KrillaStroke>() == 40 && align_of::<KrillaStroke>() == 8);
    assert!(offset_of!(KrillaStroke, dash_array) == 24);
};
