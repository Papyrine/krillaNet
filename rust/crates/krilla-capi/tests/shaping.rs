//! Shaping returns glyphs, and the run it allocates round-trips back without leaking.

use std::path::PathBuf;
use std::ptr;

use krilla_capi::api::text::{
    KrillaFont, krilla_font_free, krilla_font_new, krilla_font_shape, krilla_glyphs_free,
};
use krilla_capi::handle;
use krilla_capi::status;
use krilla_capi::types::KrillaGlyph;

/// The bundled face the managed corpus renders with.
///
/// Referenced rather than copied: a second 400KB of font in the Rust tree would be one more
/// thing to keep in step, and these tests want the same file the rest of the repository
/// measures against.
fn font_path() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../../../src/Krilla.Html.Tests/Fonts/LiberationSans-Regular.ttf")
}

fn load_font() -> *mut KrillaFont {
    let bytes = std::fs::read(font_path()).expect("the bundled Liberation face should be readable");

    let mut font = ptr::null_mut();
    let code = krilla_font_new(bytes.as_ptr(), bytes.len(), 0, &mut font);

    assert_eq!(code, status::OK);
    assert!(!font.is_null());
    font
}

/// The allocation handoff, which is the only new unsafe code on this path.
///
/// Kept separate from the shaping test so it runs under Miri, where driving rustybuzz over real
/// text would cost many minutes and prove nothing extra: what Miri has to see is the
/// `Box::into_raw` / `Box::from_raw` pair, not the shaper that fills the vector.
#[test]
fn glyph_runs_round_trip() {
    let glyphs = vec![
        KrillaGlyph {
            glyph_id: 36,
            text_start: 0,
            text_end: 1,
            x_advance: 0.5,
            x_offset: 0.0,
            y_offset: 0.0,
            y_advance: 0.0,
            location: 0,
        },
        KrillaGlyph {
            glyph_id: 57,
            text_start: 1,
            text_end: 2,
            x_advance: 0.25,
            x_offset: 0.125,
            y_offset: 0.0,
            y_advance: 0.0,
            location: 0,
        },
    ];

    let mut ptr_out: *mut KrillaGlyph = ptr::null_mut();
    let mut len_out = 0usize;

    // SAFETY: both out-parameters are live locals.
    unsafe { handle::glyphs_out(glyphs, &mut ptr_out, &mut len_out) }.expect("handoff should work");

    assert_eq!(len_out, 2);
    assert!(!ptr_out.is_null());

    // Reading through the handed-out pointer is the part that would be undefined had the
    // allocation been retagged on its way out.
    // SAFETY: exactly what the call above wrote.
    let seen = unsafe { std::slice::from_raw_parts(ptr_out, len_out) };
    assert_eq!(seen[0].glyph_id, 36);
    assert_eq!(seen[1].x_offset, 0.125);

    assert_eq!(krilla_glyphs_free(ptr_out, len_out), status::OK);
}

/// Freeing nothing is not an error, since a run with no glyphs writes a null pointer.
#[test]
fn freeing_an_empty_run_is_safe() {
    assert_eq!(krilla_glyphs_free(ptr::null_mut(), 0), status::OK);
}

#[test]
fn rejects_a_null_font() {
    let text = "A";
    let mut ptr_out: *mut KrillaGlyph = ptr::null_mut();
    let mut len_out = 0usize;

    let code = krilla_font_shape(
        ptr::null(),
        text.as_ptr(),
        text.len(),
        0,
        &mut ptr_out,
        &mut len_out,
    );

    assert_eq!(code, status::NULL_ARGUMENT);
}

/// Shaping applies kerning, which is the entire reason this export exists.
///
/// Ignored under Miri: rustybuzz over real text there costs minutes, and the unsafe code on this
/// path is covered by `glyph_runs_round_trip` above.
#[test]
#[cfg_attr(miri, ignore)]
fn shaping_applies_kerning() {
    let font = load_font();

    let shape = |text: &str| {
        let mut ptr_out: *mut KrillaGlyph = ptr::null_mut();
        let mut len_out = 0usize;

        let code = krilla_font_shape(
            font,
            text.as_ptr(),
            text.len(),
            0,
            &mut ptr_out,
            &mut len_out,
        );
        assert_eq!(code, status::OK);

        // SAFETY: exactly what the call wrote.
        let glyphs = unsafe { std::slice::from_raw_parts(ptr_out, len_out) }.to_vec();
        assert_eq!(krilla_glyphs_free(ptr_out, len_out), status::OK);
        glyphs
    };

    let kerned = shape("AV");
    assert_eq!(kerned.len(), 2, "two characters should shape to two glyphs");

    // Advances arrive divided by units-per-em, so a normal glyph is well under 1.
    assert!(kerned[0].x_advance > 0.0 && kerned[0].x_advance < 1.0);

    // Text offsets are UTF-8 byte ranges into the input.
    assert_eq!(kerned[0].text_start, 0);
    assert_eq!(kerned[1].text_start, 1);

    // "AV" is the classic kerning pair: shaped, the A advances less than it does alone.
    let alone = shape("A");
    assert!(
        kerned[0].x_advance < alone[0].x_advance,
        "kerning should pull V toward A: {} vs {}",
        kerned[0].x_advance,
        alone[0].x_advance
    );

    krilla_font_free(font);
}
