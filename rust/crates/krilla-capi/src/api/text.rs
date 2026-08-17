//! Fonts, glyph runs and simple text.

use krilla::text::{Font, GlyphId, KrillaGlyph as InnerGlyph, TextDirection};

use crate::guard::{ffi, ffi_doc};
use crate::handle;
use crate::status;
use crate::types::{KrillaGlyph, KrillaPoint, location};

/// A parsed font.
///
/// krilla's `Font` is `Arc`-backed and cheap to clone, so one handle per (bytes, index) can be
/// reused across every page in a document. Creating one is comparatively expensive.
///
/// krilla has no font database: no system enumeration, no family or style matching. Callers
/// supply bytes. That is a deliberate constraint of the underlying library, not an omission
/// here.
pub struct KrillaFont {
    pub(crate) inner: Font,
}

fn direction(value: i32) -> Result<TextDirection, i32> {
    match value {
        0 => Ok(TextDirection::Auto),
        1 => Ok(TextDirection::LeftToRight),
        2 => Ok(TextDirection::RightToLeft),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

ffi! {
    /// Parses a font from OpenType/TrueType bytes.
    ///
    /// `index` selects a face within a collection (`.ttc`/`.otc`); pass 0 for a single font.
    ///
    /// The bytes are copied (rule R3), so the caller need not keep them alive or pinned.
    fn krilla_font_new(
        data_ptr: *const u8,
        data_len: usize,
        index: u32,
        out: *mut *mut KrillaFont,
    ) {
        // SAFETY: R3 — readable for the duration of the call; copied immediately below.
        let bytes = unsafe { handle::slice(data_ptr, data_len)? };

        if bytes.is_empty() {
            return Err(status::INVALID_FONT);
        }

        let font = Font::new(bytes.to_vec().into(), index).ok_or(status::INVALID_FONT)?;

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(KrillaFont { inner: font }))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases a font.
    fn krilla_font_free(font: *mut KrillaFont) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(font) };
        Ok(status::OK)
    }
}

ffi! {
    /// Writes the font's units-per-em.
    ///
    /// Needed to normalise glyph advances for `krilla_surface_draw_glyphs`. It is the only
    /// font metric krilla exposes publicly — ascent, descent, cap height and the PostScript
    /// name are all crate-private.
    fn krilla_font_units_per_em(font: *const KrillaFont, out: *mut f32) {
        // SAFETY: R1 — live handle.
        let font = unsafe { handle::as_ref(font)? };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, font.inner.units_per_em())? };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Draws a single line of text, shaping it with the bundled shaper.
    ///
    /// Convenience over `krilla_surface_draw_glyphs`, with real limitations: no bidirectional
    /// resolution, a single script only, and no font fallback. Text needing any of those must
    /// be shaped by the caller and drawn as glyphs.
    ///
    /// `outlined` draws the glyphs as filled paths rather than as text, which makes them
    /// unselectable and unsearchable but immune to font-embedding restrictions.
    #[allow(clippy::too_many_arguments)]
    fn krilla_surface_draw_text(
        doc,
        token: u64,
        start: KrillaPoint,
        font: *const KrillaFont,
        font_size: f32,
        text_ptr: *const u8,
        text_len: usize,
        outlined: bool,
        text_direction: i32,
    ) {
        // SAFETY: R1 — live handle.
        let font = unsafe { handle::as_ref(font)? };

        // SAFETY: R4 — borrowed UTF-8 for the duration of the call.
        let text = unsafe { handle::str_arg(text_ptr, text_len)? };

        let text_direction = direction(text_direction)?;

        doc.surface_mut(token)?.draw_text(
            start.into(),
            font.inner.clone(),
            font_size,
            text,
            outlined,
            text_direction,
        );

        Ok(status::OK)
    }
}

ffi_doc! {
    /// Draws a pre-shaped glyph run.
    ///
    /// The full-control path: the caller owns shaping, bidirectional resolution, font
    /// fallback and layout.
    ///
    /// Two contracts krilla does not check and would otherwise fail on:
    ///
    /// - Advances and offsets must already be divided by the font's units-per-em. Getting
    ///   this wrong yields plausible output with wrong spacing rather than an error.
    /// - Each glyph's `text_start`/`text_end` must be UTF-8 character boundaries within
    ///   `text`, because krilla slices the string directly. Validated here, since the
    ///   alternative is a panic from inside a `Drop` during unwinding.
    #[allow(clippy::too_many_arguments)]
    fn krilla_surface_draw_glyphs(
        doc,
        token: u64,
        start: KrillaPoint,
        font: *const KrillaFont,
        font_size: f32,
        text_ptr: *const u8,
        text_len: usize,
        glyph_ptr: *const KrillaGlyph,
        glyph_count: usize,
        outlined: bool,
    ) {
        // SAFETY: R1 — live handle.
        let font = unsafe { handle::as_ref(font)? };

        // SAFETY: R4 — borrowed UTF-8 for the duration of the call.
        let text = unsafe { handle::str_arg(text_ptr, text_len)? };

        if glyph_count == 0 {
            return Ok(status::OK);
        }

        if glyph_ptr.is_null() {
            return Err(status::NULL_ARGUMENT);
        }

        // SAFETY: R3 — the caller guarantees `glyph_count` readable elements for the call.
        let raw = unsafe { std::slice::from_raw_parts(glyph_ptr, glyph_count) };

        let mut glyphs = Vec::with_capacity(glyph_count);

        for glyph in raw {
            let start_index = glyph.text_start as usize;
            let end_index = glyph.text_end as usize;

            // krilla indexes `text` with this range. An out-of-bounds or mid-character index
            // panics inside the draw call, so it is rejected up front instead.
            if start_index > end_index
                || end_index > text.len()
                || !text.is_char_boundary(start_index)
                || !text.is_char_boundary(end_index)
            {
                return Err(status::INVALID_ARGUMENT);
            }

            glyphs.push(InnerGlyph::new(
                GlyphId::new(glyph.glyph_id),
                glyph.x_advance,
                glyph.x_offset,
                glyph.y_offset,
                glyph.y_advance,
                start_index..end_index,
                location(glyph.location),
            ));
        }

        doc.surface_mut(token)?.draw_glyphs(
            start.into(),
            &glyphs,
            font.inner.clone(),
            text,
            font_size,
            outlined,
        );

        Ok(status::OK)
    }
}
