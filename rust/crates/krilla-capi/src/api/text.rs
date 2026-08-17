//! Fonts, glyph runs and simple text.

use std::sync::Arc;

use krilla::text::{Font, GlyphId, KrillaGlyph as InnerGlyph, TextDirection};
use rustybuzz::{Direction, GlyphInfo, UnicodeBuffer};

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
    /// The bytes the font was parsed from.
    ///
    /// Kept because krilla's `Font` will not give them back: `font_data`, `index` and
    /// `variation_coordinates` are all `pub(crate)`, and `krilla_font_shape` needs the bytes to
    /// build a rustybuzz face. The same `Arc` is handed to krilla, so this shares one allocation
    /// rather than holding a second copy of every font file.
    data: Arc<Vec<u8>>,
    index: u32,
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

        // One allocation, shared: krilla's `Data` is `Arc`-backed, so handing it a clone costs a
        // refcount rather than a second copy of the file.
        let data = Arc::new(bytes.to_vec());
        let font = Font::new(data.clone().into(), index).ok_or(status::INVALID_FONT)?;

        // SAFETY: out-parameter contract.
        unsafe {
            handle::write_out(
                out,
                handle::into_handle(KrillaFont {
                    inner: font,
                    data,
                    index,
                }),
            )?
        };
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

ffi! {
    /// Shapes text with the bundled shaper and returns the glyphs, without drawing anything.
    ///
    /// This is what `krilla_surface_draw_text` does internally, stopped one step earlier so the
    /// caller can measure the result. A layout engine has to know how wide a word is before it
    /// can decide where a line breaks, and summing `hmtx` advances is not that width: it misses
    /// kerning and every ligature, so text laid out that way is a little too wide and breaks in
    /// the wrong places.
    ///
    /// Advances and offsets come back already divided by units-per-em, which is the form
    /// `krilla_surface_draw_glyphs` expects, so a shaped run can be measured and then drawn
    /// without touching the numbers in between. `text_start` and `text_end` are UTF-8 byte
    /// offsets into `text`.
    ///
    /// Rule R5: the run is allocated here and must be released with `krilla_glyphs_free`.
    ///
    /// Same limits as `krilla_surface_draw_text`: one font, one script, no bidirectional
    /// resolution and no fallback.
    fn krilla_font_shape(
        font: *const KrillaFont,
        text_ptr: *const u8,
        text_len: usize,
        text_direction: i32,
        out_ptr: *mut *mut KrillaGlyph,
        out_len: *mut usize,
    ) {
        // SAFETY: R1 - live handle.
        let font = unsafe { handle::as_ref(font)? };

        // SAFETY: R4 - borrowed UTF-8 for the duration of the call.
        let text = unsafe { handle::str_arg(text_ptr, text_len)? };

        let glyphs = shape(font, text, direction(text_direction)?);

        // SAFETY: out-parameter contract; `glyphs_out` null-checks both pointers.
        unsafe { handle::glyphs_out(glyphs, out_ptr, out_len)? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases a glyph run written by `krilla_font_shape`.
    ///
    /// Rule R2: the native library allocated it, so the native library frees it. A glyph is
    /// wider than a byte and more strictly aligned, so this cannot go through
    /// `krilla_buffer_free`.
    fn krilla_glyphs_free(ptr: *mut KrillaGlyph, len: usize) {
        // SAFETY: R5 - the caller passes back exactly what a successful call wrote out.
        unsafe { handle::free_glyphs(ptr, len) };
        Ok(status::OK)
    }
}

/// Shapes `text` with `font`, mirroring what krilla does inside `draw_text`.
///
/// krilla's shaping is crate-private, so this drives the same rustybuzz that krilla's
/// `simple-text` feature already pulls in. Using the same shaper is the point: a second one
/// would eventually disagree, and a measurement that disagrees with the drawing is worse than
/// no measurement at all.
///
/// Variable-font coordinates are not applied, because nothing in this API can set them.
fn shape(font: &KrillaFont, text: &str, text_direction: TextDirection) -> Vec<KrillaGlyph> {
    // An unparseable face yields no glyphs rather than an error. The bytes already parsed once,
    // in `krilla_font_new`, so reaching this would mean the two parsers disagree, and dropping
    // one run is a better outcome than failing a whole document over it.
    let Some(face) = rustybuzz::Face::from_slice(font.data.as_ref(), font.index) else {
        return Vec::new();
    };

    let mut buffer = UnicodeBuffer::new();
    buffer.push_str(text);
    buffer.guess_segment_properties();

    match text_direction {
        TextDirection::LeftToRight => buffer.set_direction(Direction::LeftToRight),
        TextDirection::RightToLeft => buffer.set_direction(Direction::RightToLeft),
        // Auto keeps whatever `guess_segment_properties` inferred from the script.
        _ => {}
    }

    let forward = matches!(
        buffer.direction(),
        Direction::LeftToRight | Direction::TopToBottom
    );

    let output = rustybuzz::shape(&face, &[], buffer);
    let positions = output.glyph_positions();
    let infos = output.glyph_infos();
    let units_per_em = font.inner.units_per_em();

    let mut glyphs = Vec::with_capacity(output.len());

    for index in 0..output.len() {
        let info = infos[index];
        let position = positions[index];
        let start = info.cluster as usize;

        let end = cluster_end(infos, index, info.cluster, forward)
            .map_or(text.len(), |last| infos[last].cluster as usize);

        glyphs.push(KrillaGlyph {
            glyph_id: info.glyph_id,
            text_start: start as u32,
            text_end: end as u32,
            x_advance: position.x_advance as f32 / units_per_em,
            x_offset: position.x_offset as f32 / units_per_em,
            y_offset: position.y_offset as f32 / units_per_em,
            y_advance: position.y_advance as f32 / units_per_em,
            location: 0,
        });
    }

    glyphs
}

/// The index of the first glyph past `index`'s cluster, or `None` at the end of the run.
///
/// Shaping can map several characters onto one glyph and one character onto several, so a
/// glyph's text range is not its own index: it runs to the next glyph carrying a different
/// cluster. A right-to-left run is laid out in reverse, so the scan runs backwards there.
fn cluster_end(infos: &[GlyphInfo], index: usize, cluster: u32, forward: bool) -> Option<usize> {
    let step = |at: usize| {
        if forward {
            at.checked_add(1)
        } else {
            at.checked_sub(1)
        }
    };

    let mut at = step(index);

    while let Some(current) = at {
        match infos.get(current) {
            Some(info) if info.cluster == cluster => at = step(current),
            _ => break,
        }
    }

    at.filter(|&index| index < infos.len())
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
