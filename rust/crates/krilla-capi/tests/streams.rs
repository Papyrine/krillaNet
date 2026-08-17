//! Sub-stream nesting: graphics, masks and patterns.
//!
//! These exist for Miri. `begin_stream` adds a second layer of lifetime erasure on top of the
//! page one — a sub-surface borrowing the page surface, which borrows the page, which borrows
//! the document — and that stack has to unwind in exactly the right order. Review does not
//! catch aliasing mistakes there; Miri does.

use std::ptr;

use krilla_capi::api::document::{
    KrillaErrorObject, krilla_document_close_page, krilla_document_finish, krilla_document_free,
    krilla_document_new, krilla_document_start_page,
};
use krilla_capi::api::error::krilla_buffer_free;
use krilla_capi::api::graphic::{
    KrillaGraphic, KrillaStream, krilla_graphic_free, krilla_graphic_new, krilla_paint_new_pattern,
    krilla_stream_begin, krilla_stream_finish, krilla_stream_free, krilla_surface_draw_graphic,
    krilla_surface_push_mask,
};
use krilla_capi::api::paint::{krilla_paint_free, krilla_paint_new_color};
use krilla_capi::api::path::{
    krilla_path_builder_finish, krilla_path_builder_free, krilla_path_builder_new,
    krilla_path_builder_push_rect, krilla_path_free,
};
use krilla_capi::api::surface::{
    krilla_surface_draw_path, krilla_surface_pop, krilla_surface_set_fill,
};
use krilla_capi::document::KrillaDocument;
use krilla_capi::status;
use krilla_capi::types::{
    COLOR_SPACE_RGB, KrillaColor, KrillaFill, KrillaPageSettings, KrillaRect,
};

fn empty_rect() -> KrillaRect {
    KrillaRect {
        left: 0.0,
        top: 0.0,
        right: 0.0,
        bottom: 0.0,
    }
}

fn page_settings() -> KrillaPageSettings {
    KrillaPageSettings {
        width: 100.0,
        height: 100.0,
        media_box: empty_rect(),
        crop_box: empty_rect(),
        bleed_box: empty_rect(),
        trim_box: empty_rect(),
        art_box: empty_rect(),
        present: 0,
        reserved: 0,
    }
}

fn new_document() -> *mut KrillaDocument {
    let mut doc = ptr::null_mut();
    assert_eq!(krilla_document_new(&mut doc), status::OK);
    doc
}

fn start_page(doc: *mut KrillaDocument) -> u64 {
    let mut token = 0u64;
    assert_eq!(
        krilla_document_start_page(doc, page_settings(), &mut token),
        status::OK
    );
    token
}

/// Draws a filled square into whichever surface `token` names.
fn draw_square(doc: *mut KrillaDocument, token: u64) {
    let mut paint = ptr::null_mut();
    assert_eq!(
        krilla_paint_new_color(
            KrillaColor {
                space: COLOR_SPACE_RGB,
                components: [10, 200, 90, 0],
            },
            &mut paint,
        ),
        status::OK
    );

    assert_eq!(
        krilla_surface_set_fill(
            doc,
            token,
            paint,
            KrillaFill {
                opacity: 1.0,
                rule: 0,
            },
        ),
        status::OK
    );

    let mut builder = ptr::null_mut();
    assert_eq!(krilla_path_builder_new(&mut builder), status::OK);
    assert_eq!(
        krilla_path_builder_push_rect(
            builder,
            KrillaRect {
                left: 10.0,
                top: 10.0,
                right: 60.0,
                bottom: 60.0,
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

fn build_graphic(doc: *mut KrillaDocument, page: u64) -> *mut KrillaGraphic {
    let mut sub = 0u64;
    assert_eq!(krilla_stream_begin(doc, page, &mut sub), status::OK);
    draw_square(doc, sub);

    let mut stream: *mut KrillaStream = ptr::null_mut();
    assert_eq!(krilla_stream_finish(doc, sub, &mut stream), status::OK);

    let mut graphic = ptr::null_mut();
    assert_eq!(
        krilla_graphic_new(doc, stream, false, &mut graphic),
        status::OK
    );
    assert_eq!(krilla_stream_free(stream), status::OK);
    graphic
}

#[test]
fn a_graphic_can_be_drawn_repeatedly() {
    let doc = new_document();
    let page = start_page(doc);

    let graphic = build_graphic(doc, page);

    // The point of a graphic: krilla emits the content once and references it.
    for _ in 0..3 {
        assert_eq!(krilla_surface_draw_graphic(doc, page, graphic), status::OK);
    }

    assert_eq!(krilla_graphic_free(graphic), status::OK);
    assert_eq!(krilla_document_close_page(doc, page), status::OK);

    let bytes = finish(doc);
    assert!(bytes.starts_with(b"%PDF-"));
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn sub_streams_nest() {
    let doc = new_document();
    let page = start_page(doc);

    let mut outer = 0u64;
    assert_eq!(krilla_stream_begin(doc, page, &mut outer), status::OK);

    let mut inner = 0u64;
    assert_eq!(krilla_stream_begin(doc, outer, &mut inner), status::OK);
    draw_square(doc, inner);

    let mut inner_stream: *mut KrillaStream = ptr::null_mut();
    assert_eq!(
        krilla_stream_finish(doc, inner, &mut inner_stream),
        status::OK
    );
    assert_eq!(krilla_stream_free(inner_stream), status::OK);

    let mut outer_stream: *mut KrillaStream = ptr::null_mut();
    assert_eq!(
        krilla_stream_finish(doc, outer, &mut outer_stream),
        status::OK
    );
    assert_eq!(krilla_stream_free(outer_stream), status::OK);

    assert_eq!(krilla_document_close_page(doc, page), status::OK);
    assert_eq!(krilla_document_free(doc), status::OK);
}

/// While a sub-stream is open it is the innermost surface, and the page surface below it is
/// borrowed. Drawing at the page token then must be refused, not served.
#[test]
fn the_page_is_unreachable_while_a_sub_stream_is_open() {
    let doc = new_document();
    let page = start_page(doc);

    let mut sub = 0u64;
    assert_eq!(krilla_stream_begin(doc, page, &mut sub), status::OK);

    let mut path = ptr::null_mut();
    let mut builder = ptr::null_mut();
    assert_eq!(krilla_path_builder_new(&mut builder), status::OK);
    assert_eq!(
        krilla_path_builder_push_rect(
            builder,
            KrillaRect {
                left: 0.0,
                top: 0.0,
                right: 10.0,
                bottom: 10.0,
            },
        ),
        status::OK
    );
    assert_eq!(krilla_path_builder_finish(builder, &mut path), status::OK);

    assert_eq!(
        krilla_surface_draw_path(doc, page, path),
        status::STALE_PAGE
    );

    assert_eq!(krilla_path_free(path), status::OK);
    assert_eq!(krilla_path_builder_free(builder), status::OK);
    assert_eq!(krilla_document_free(doc), status::OK);
}

/// Closing a page with a sub-stream still open must unwind it rather than dropping the
/// surface it borrows.
#[test]
fn closing_a_page_unwinds_open_sub_streams() {
    let doc = new_document();
    let page = start_page(doc);

    let mut sub = 0u64;
    assert_eq!(krilla_stream_begin(doc, page, &mut sub), status::OK);
    draw_square(doc, sub);

    assert_eq!(krilla_document_close_page(doc, page), status::POP_UNDERFLOW);

    let bytes = finish(doc);
    assert!(bytes.starts_with(b"%PDF-"));
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn freeing_with_a_sub_stream_open_is_clean() {
    let doc = new_document();
    let page = start_page(doc);

    let mut sub = 0u64;
    assert_eq!(krilla_stream_begin(doc, page, &mut sub), status::OK);
    draw_square(doc, sub);

    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn a_mask_consumes_its_stream() {
    let doc = new_document();
    let page = start_page(doc);

    let mut sub = 0u64;
    assert_eq!(krilla_stream_begin(doc, page, &mut sub), status::OK);
    draw_square(doc, sub);

    let mut stream: *mut KrillaStream = ptr::null_mut();
    assert_eq!(krilla_stream_finish(doc, sub, &mut stream), status::OK);

    // 0 = luminosity.
    assert_eq!(krilla_surface_push_mask(doc, page, 0, stream), status::OK);
    draw_square(doc, page);
    assert_eq!(krilla_surface_pop(doc, page), status::OK);

    // The stream was consumed, so a second use is refused.
    assert_eq!(
        krilla_surface_push_mask(doc, page, 0, stream),
        status::CONSUMED
    );

    assert_eq!(krilla_stream_free(stream), status::OK);
    assert_eq!(krilla_document_close_page(doc, page), status::OK);

    let bytes = finish(doc);
    assert!(bytes.starts_with(b"%PDF-"));
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn a_pattern_paint_tiles_a_stream() {
    let doc = new_document();
    let page = start_page(doc);

    let mut sub = 0u64;
    assert_eq!(krilla_stream_begin(doc, page, &mut sub), status::OK);
    draw_square(doc, sub);

    let mut stream: *mut KrillaStream = ptr::null_mut();
    assert_eq!(krilla_stream_finish(doc, sub, &mut stream), status::OK);

    let identity = krilla_capi::types::KrillaTransform {
        sx: 1.0,
        ky: 0.0,
        kx: 0.0,
        sy: 1.0,
        tx: 0.0,
        ty: 0.0,
    };

    let mut paint = ptr::null_mut();
    assert_eq!(
        krilla_paint_new_pattern(doc, stream, identity, 20.0, 20.0, &mut paint),
        status::OK
    );

    assert_eq!(krilla_stream_free(stream), status::OK);
    assert_eq!(krilla_paint_free(paint), status::OK);
    assert_eq!(krilla_document_close_page(doc, page), status::OK);
    assert_eq!(krilla_document_free(doc), status::OK);
}

/// A graphic belongs to the document that created it. krilla cannot detect the mistake and
/// would emit a PDF referencing objects that do not exist.
#[test]
fn a_graphic_from_another_document_is_refused() {
    let first = new_document();
    let first_page = start_page(first);
    let graphic = build_graphic(first, first_page);

    let second = new_document();
    let second_page = start_page(second);

    assert_eq!(
        krilla_surface_draw_graphic(second, second_page, graphic),
        status::WRONG_DOCUMENT
    );

    assert_eq!(krilla_graphic_free(graphic), status::OK);
    assert_eq!(krilla_document_close_page(first, first_page), status::OK);
    assert_eq!(krilla_document_close_page(second, second_page), status::OK);
    assert_eq!(krilla_document_free(first), status::OK);
    assert_eq!(krilla_document_free(second), status::OK);
}
