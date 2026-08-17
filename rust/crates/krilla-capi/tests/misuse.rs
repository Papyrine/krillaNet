//! Every way a caller can misuse the API must return a status code, never abort and never UB.
//!
//! This is the counterpart to `lifecycle.rs`: that file proves the happy path is sound, this
//! one proves the unhappy paths are survivable. Both run under Miri.

use std::ptr;

use krilla_capi::api::document::{
    KrillaErrorObject, krilla_document_close_page, krilla_document_finish, krilla_document_free,
    krilla_document_new, krilla_document_start_page, krilla_error_free,
};
use krilla_capi::api::error::krilla_buffer_free;
use krilla_capi::document::KrillaDocument;
use krilla_capi::status;
use krilla_capi::types::{KrillaPageSettings, KrillaRect};

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

fn new_document() -> *mut KrillaDocument {
    let mut doc = ptr::null_mut();
    assert_eq!(krilla_document_new(&mut doc), status::OK);
    doc
}

fn start_page(doc: *mut KrillaDocument) -> u64 {
    let mut token = 0u64;
    assert_eq!(
        krilla_document_start_page(doc, page_settings(100.0, 100.0), &mut token),
        status::OK
    );
    token
}

/// Calls `finish` and returns only the status, releasing any buffer it produced.
///
/// The release matters: Miri reports an unfreed buffer as a leak, which is precisely the
/// signal wanted here — it proves the ownership contract in rule R5 is being honoured by the
/// tests themselves and not just asserted in a doc comment.
fn finish_status(doc: *mut KrillaDocument) -> i32 {
    let mut ptr_out = ptr::null_mut();
    let mut len_out = 0usize;
    let mut error: *mut KrillaErrorObject = ptr::null_mut();

    let status = krilla_document_finish(doc, &mut ptr_out, &mut len_out, &mut error);

    if !ptr_out.is_null() {
        assert_eq!(krilla_buffer_free(ptr_out, len_out), status::OK);
    }

    if !error.is_null() {
        assert_eq!(krilla_error_free(error), status::OK);
    }

    status
}

#[test]
fn second_page_while_one_is_open_is_refused() {
    let doc = new_document();
    let token = start_page(doc);

    let mut second = 0u64;
    assert_eq!(
        krilla_document_start_page(doc, page_settings(50.0, 50.0), &mut second),
        status::PAGE_ALREADY_OPEN
    );

    // The first page must still be usable; a refused call may not disturb existing state.
    assert_eq!(krilla_document_close_page(doc, token), status::OK);
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn closing_with_a_stale_token_is_refused() {
    let doc = new_document();
    let first = start_page(doc);
    assert_eq!(krilla_document_close_page(doc, first), status::OK);

    let second = start_page(doc);

    // The old token must not close the page that happens to be open now.
    assert_eq!(krilla_document_close_page(doc, first), status::STALE_PAGE);

    // ...and the page it wrongly targeted must still be open.
    assert_eq!(krilla_document_close_page(doc, second), status::OK);
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn closing_with_no_page_open_is_refused() {
    let doc = new_document();
    assert_eq!(krilla_document_close_page(doc, 1), status::NO_OPEN_PAGE);
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn double_close_is_refused() {
    let doc = new_document();
    let token = start_page(doc);

    assert_eq!(krilla_document_close_page(doc, token), status::OK);
    assert_eq!(krilla_document_close_page(doc, token), status::NO_OPEN_PAGE);

    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn finishing_with_a_page_open_is_refused() {
    let doc = new_document();
    let token = start_page(doc);

    assert_eq!(finish_status(doc), status::PAGE_ALREADY_OPEN);

    // Refusing must leave the document usable rather than half-consumed.
    assert_eq!(krilla_document_close_page(doc, token), status::OK);
    assert_eq!(finish_status(doc), status::OK);
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn use_after_finish_is_refused() {
    let doc = new_document();
    assert_eq!(finish_status(doc), status::OK);

    assert_eq!(finish_status(doc), status::FINISHED);

    let mut token = 0u64;
    assert_eq!(
        krilla_document_start_page(doc, page_settings(10.0, 10.0), &mut token),
        status::FINISHED
    );

    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn degenerate_page_geometry_is_refused() {
    let doc = new_document();
    let mut token = 0u64;

    for (width, height) in [(0.0, 100.0), (100.0, 0.0), (-5.0, 100.0), (f32::NAN, 100.0)] {
        assert_eq!(
            krilla_document_start_page(doc, page_settings(width, height), &mut token),
            status::INVALID_GEOMETRY,
            "accepted a {width} x {height} page"
        );
    }

    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn null_document_is_refused_rather_than_dereferenced() {
    let mut token = 0u64;
    assert_eq!(
        krilla_document_start_page(ptr::null_mut(), page_settings(10.0, 10.0), &mut token),
        status::NULL_ARGUMENT
    );
    assert_eq!(
        krilla_document_close_page(ptr::null_mut(), 1),
        status::NULL_ARGUMENT
    );
}

#[test]
fn null_out_parameters_are_refused() {
    let doc = new_document();

    assert_eq!(
        krilla_document_start_page(doc, page_settings(10.0, 10.0), ptr::null_mut()),
        status::NULL_ARGUMENT
    );

    assert_eq!(krilla_document_free(doc), status::OK);
}
