//! Drawing produces real PDF content, and the push/pop discipline holds.

use std::ptr;

use krilla_capi::api::document::{
    KrillaErrorObject, krilla_document_close_page, krilla_document_finish, krilla_document_free,
    krilla_document_new, krilla_document_start_page,
};
use krilla_capi::api::error::krilla_buffer_free;
use krilla_capi::api::paint::{
    KrillaStop, krilla_paint_free, krilla_paint_new_color, krilla_paint_new_linear_gradient,
};
use krilla_capi::api::path::{
    KrillaPath, krilla_path_builder_close, krilla_path_builder_finish, krilla_path_builder_free,
    krilla_path_builder_line_to, krilla_path_builder_move_to, krilla_path_builder_new,
    krilla_path_builder_push_rect, krilla_path_free,
};
use krilla_capi::api::surface::{
    krilla_surface_draw_path, krilla_surface_pop, krilla_surface_push_opacity,
    krilla_surface_push_transform, krilla_surface_set_fill,
};
use krilla_capi::document::KrillaDocument;
use krilla_capi::status;
use krilla_capi::types::{
    COLOR_SPACE_RGB, KrillaColor, KrillaFill, KrillaPageSettings, KrillaRect, KrillaTransform,
};

fn empty_rect() -> KrillaRect {
    KrillaRect {
        left: 0.0,
        top: 0.0,
        right: 0.0,
        bottom: 0.0,
    }
}

fn page_settings(width: f32, height: f32) -> KrillaPageSettings {
    KrillaPageSettings {
        width,
        height,
        media_box: empty_rect(),
        crop_box: empty_rect(),
        bleed_box: empty_rect(),
        trim_box: empty_rect(),
        art_box: empty_rect(),
        present: 0,
        reserved: 0,
    }
}

fn identity() -> KrillaTransform {
    KrillaTransform {
        sx: 1.0,
        ky: 0.0,
        kx: 0.0,
        sy: 1.0,
        tx: 0.0,
        ty: 0.0,
    }
}

fn rgb(r: u8, g: u8, b: u8) -> KrillaColor {
    KrillaColor {
        space: COLOR_SPACE_RGB,
        components: [r, g, b, 0],
    }
}

/// Builds a closed triangle.
fn triangle() -> *mut KrillaPath {
    let mut builder = ptr::null_mut();
    assert_eq!(krilla_path_builder_new(&mut builder), status::OK);

    assert_eq!(krilla_path_builder_move_to(builder, 10.0, 10.0), status::OK);
    assert_eq!(krilla_path_builder_line_to(builder, 90.0, 10.0), status::OK);
    assert_eq!(krilla_path_builder_line_to(builder, 50.0, 80.0), status::OK);
    assert_eq!(krilla_path_builder_close(builder), status::OK);

    let mut path = ptr::null_mut();
    assert_eq!(krilla_path_builder_finish(builder, &mut path), status::OK);
    assert_eq!(krilla_path_builder_free(builder), status::OK);
    path
}

fn finish(doc: *mut KrillaDocument) -> Vec<u8> {
    let mut ptr_out = ptr::null_mut();
    let mut len_out = 0usize;
    let mut error: *mut KrillaErrorObject = ptr::null_mut();

    assert_eq!(
        krilla_document_finish(doc, &mut ptr_out, &mut len_out, &mut error),
        status::OK
    );

    // SAFETY: a successful finish wrote `len_out` readable bytes at `ptr_out`.
    let bytes = unsafe { std::slice::from_raw_parts(ptr_out, len_out) }.to_vec();
    assert_eq!(krilla_buffer_free(ptr_out, len_out), status::OK);
    bytes
}

#[test]
fn filled_triangle_produces_content() {
    let doc = {
        let mut doc = ptr::null_mut();
        assert_eq!(krilla_document_new(&mut doc), status::OK);
        doc
    };

    let mut token = 0u64;
    assert_eq!(
        krilla_document_start_page(doc, page_settings(100.0, 100.0), &mut token),
        status::OK
    );

    let mut paint = ptr::null_mut();
    assert_eq!(
        krilla_paint_new_color(rgb(220, 30, 30), &mut paint),
        status::OK
    );

    let fill = KrillaFill {
        opacity: 1.0,
        rule: 0,
    };
    assert_eq!(krilla_surface_set_fill(doc, token, paint, fill), status::OK);

    let path = triangle();
    assert_eq!(krilla_surface_draw_path(doc, token, path), status::OK);

    assert_eq!(krilla_path_free(path), status::OK);
    assert_eq!(krilla_paint_free(paint), status::OK);
    assert_eq!(krilla_document_close_page(doc, token), status::OK);

    let bytes = finish(doc);
    assert!(bytes.starts_with(b"%PDF-"));
    // An empty page serializes to well under a kilobyte; drawn content pushes it past that.
    assert!(
        bytes.len() > 800,
        "expected drawn content, got {} bytes",
        bytes.len()
    );

    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn gradient_fill_is_accepted() {
    let doc = {
        let mut doc = ptr::null_mut();
        assert_eq!(krilla_document_new(&mut doc), status::OK);
        doc
    };

    let mut token = 0u64;
    assert_eq!(
        krilla_document_start_page(doc, page_settings(100.0, 100.0), &mut token),
        status::OK
    );

    let stops = [
        KrillaStop {
            offset: 0.0,
            color: rgb(255, 0, 0),
            opacity: 1.0,
        },
        KrillaStop {
            offset: 1.0,
            color: rgb(0, 0, 255),
            opacity: 1.0,
        },
    ];

    let mut paint = ptr::null_mut();
    assert_eq!(
        krilla_paint_new_linear_gradient(
            0.0,
            0.0,
            100.0,
            0.0,
            identity(),
            0,
            true,
            stops.as_ptr(),
            stops.len(),
            &mut paint,
        ),
        status::OK
    );

    let fill = KrillaFill {
        opacity: 1.0,
        rule: 0,
    };
    assert_eq!(krilla_surface_set_fill(doc, token, paint, fill), status::OK);

    let mut builder = ptr::null_mut();
    assert_eq!(krilla_path_builder_new(&mut builder), status::OK);
    assert_eq!(
        krilla_path_builder_push_rect(
            builder,
            KrillaRect {
                left: 0.0,
                top: 0.0,
                right: 100.0,
                bottom: 100.0,
            },
        ),
        status::OK
    );

    let mut path = ptr::null_mut();
    assert_eq!(krilla_path_builder_finish(builder, &mut path), status::OK);
    assert_eq!(krilla_path_builder_free(builder), status::OK);

    assert_eq!(krilla_surface_draw_path(doc, token, path), status::OK);
    assert_eq!(krilla_path_free(path), status::OK);
    assert_eq!(krilla_paint_free(paint), status::OK);
    assert_eq!(krilla_document_close_page(doc, token), status::OK);

    let bytes = finish(doc);
    assert!(bytes.starts_with(b"%PDF-"));
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn balanced_push_and_pop_round_trips() {
    let doc = {
        let mut doc = ptr::null_mut();
        assert_eq!(krilla_document_new(&mut doc), status::OK);
        doc
    };

    let mut token = 0u64;
    assert_eq!(
        krilla_document_start_page(doc, page_settings(100.0, 100.0), &mut token),
        status::OK
    );

    assert_eq!(
        krilla_surface_push_transform(doc, token, identity()),
        status::OK
    );
    assert_eq!(krilla_surface_push_opacity(doc, token, 0.5), status::OK);

    let path = triangle();
    assert_eq!(krilla_surface_draw_path(doc, token, path), status::OK);
    assert_eq!(krilla_path_free(path), status::OK);

    assert_eq!(krilla_surface_pop(doc, token), status::OK);
    assert_eq!(krilla_surface_pop(doc, token), status::OK);

    // The stack is empty now, so a third pop must be refused rather than unwrapping `None`
    // inside krilla.
    assert_eq!(krilla_surface_pop(doc, token), status::POP_UNDERFLOW);

    assert_eq!(krilla_document_close_page(doc, token), status::OK);
    let bytes = finish(doc);
    assert!(bytes.starts_with(b"%PDF-"));
    assert_eq!(krilla_document_free(doc), status::OK);
}

/// Leaving a `push` unmatched must be reported, and must still close the page cleanly —
/// krilla's `Surface::drop` asserts an empty push stack, and a panic in `drop` aborts.
#[test]
fn unbalanced_push_is_reported_and_rebalanced() {
    let doc = {
        let mut doc = ptr::null_mut();
        assert_eq!(krilla_document_new(&mut doc), status::OK);
        doc
    };

    let mut token = 0u64;
    assert_eq!(
        krilla_document_start_page(doc, page_settings(100.0, 100.0), &mut token),
        status::OK
    );

    assert_eq!(
        krilla_surface_push_transform(doc, token, identity()),
        status::OK
    );

    // Deliberately no matching pop.
    assert_eq!(
        krilla_document_close_page(doc, token),
        status::POP_UNDERFLOW
    );

    // The document survived, and is still usable.
    let bytes = finish(doc);
    assert!(bytes.starts_with(b"%PDF-"));
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn drawing_against_a_stale_token_is_refused() {
    let doc = {
        let mut doc = ptr::null_mut();
        assert_eq!(krilla_document_new(&mut doc), status::OK);
        doc
    };

    let mut token = 0u64;
    assert_eq!(
        krilla_document_start_page(doc, page_settings(100.0, 100.0), &mut token),
        status::OK
    );
    assert_eq!(krilla_document_close_page(doc, token), status::OK);

    let path = triangle();
    assert_eq!(
        krilla_surface_draw_path(doc, token, path),
        status::NO_OPEN_PAGE
    );

    assert_eq!(krilla_path_free(path), status::OK);
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn empty_path_builder_is_refused() {
    let mut builder = ptr::null_mut();
    assert_eq!(krilla_path_builder_new(&mut builder), status::OK);

    let mut path = ptr::null_mut();
    assert_eq!(
        krilla_path_builder_finish(builder, &mut path),
        status::INVALID_GEOMETRY
    );

    assert_eq!(krilla_path_builder_free(builder), status::OK);
}

#[test]
fn finishing_a_builder_twice_is_refused() {
    let mut builder = ptr::null_mut();
    assert_eq!(krilla_path_builder_new(&mut builder), status::OK);
    assert_eq!(krilla_path_builder_move_to(builder, 0.0, 0.0), status::OK);
    assert_eq!(krilla_path_builder_line_to(builder, 10.0, 10.0), status::OK);

    let mut path = ptr::null_mut();
    assert_eq!(krilla_path_builder_finish(builder, &mut path), status::OK);

    let mut second = ptr::null_mut();
    assert_eq!(
        krilla_path_builder_finish(builder, &mut second),
        status::CONSUMED
    );

    assert_eq!(krilla_path_free(path), status::OK);
    assert_eq!(krilla_path_builder_free(builder), status::OK);
}

#[test]
fn gradient_with_no_stops_is_refused() {
    let mut paint = ptr::null_mut();
    assert_eq!(
        krilla_paint_new_linear_gradient(
            0.0,
            0.0,
            10.0,
            0.0,
            identity(),
            0,
            true,
            ptr::null(),
            0,
            &mut paint,
        ),
        status::INVALID_ARGUMENT
    );
}
