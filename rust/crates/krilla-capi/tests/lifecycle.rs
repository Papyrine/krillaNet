//! Exercises the document/page/surface pointer choreography through the real exports.
//!
//! These run under `cargo +nightly miri test`, which is the point of them. The lifetime
//! erasure in `document.rs` is the one part of this crate whose correctness rests on an
//! argument rather than on the type system, and Stacked Borrows violations there are exactly
//! the class of bug that human review does not catch. The PDFs produced are kept tiny so the
//! suite stays runnable under Miri's interpreter.

use std::ptr;

use krilla_capi::api::document::{
    KrillaErrorObject, krilla_document_close_page, krilla_document_finish, krilla_document_free,
    krilla_document_new, krilla_document_open_page, krilla_document_start_page,
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
    assert!(!doc.is_null());
    doc
}

/// Runs `finish` and returns the PDF bytes, releasing the buffer afterwards.
fn finish(doc: *mut KrillaDocument) -> Vec<u8> {
    let mut ptr_out = ptr::null_mut();
    let mut len_out = 0usize;
    let mut error: *mut KrillaErrorObject = ptr::null_mut();

    let status = krilla_document_finish(doc, &mut ptr_out, &mut len_out, &mut error);
    assert_eq!(status, status::OK, "finish failed");
    assert!(error.is_null());

    // SAFETY: a successful finish wrote `len_out` readable bytes at `ptr_out`.
    let bytes = unsafe { std::slice::from_raw_parts(ptr_out, len_out) }.to_vec();
    assert_eq!(krilla_buffer_free(ptr_out, len_out), status::OK);
    bytes
}

#[test]
fn empty_document_produces_a_pdf() {
    let doc = new_document();
    let bytes = finish(doc);

    assert!(bytes.starts_with(b"%PDF-"));
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn open_and_close_a_page() {
    let doc = new_document();

    let mut token = 0u64;
    assert_eq!(
        krilla_document_start_page(doc, page_settings(200.0, 100.0), &mut token),
        status::OK
    );
    assert_ne!(token, 0);

    let mut open = 0u64;
    assert_eq!(krilla_document_open_page(doc, &mut open), status::OK);
    assert_eq!(open, token);

    assert_eq!(krilla_document_close_page(doc, token), status::OK);

    assert_eq!(krilla_document_open_page(doc, &mut open), status::OK);
    assert_eq!(open, 0);

    let bytes = finish(doc);
    assert!(bytes.starts_with(b"%PDF-"));
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn many_pages_reuse_the_document() {
    let doc = new_document();

    for index in 0..5 {
        let mut token = 0u64;
        let width = 100.0 + index as f32;
        assert_eq!(
            krilla_document_start_page(doc, page_settings(width, 100.0), &mut token),
            status::OK
        );
        assert_eq!(krilla_document_close_page(doc, token), status::OK);
    }

    let bytes = finish(doc);
    assert!(bytes.starts_with(b"%PDF-"));
    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn tokens_are_unique_per_page() {
    let doc = new_document();
    let mut seen = Vec::new();

    for _ in 0..3 {
        let mut token = 0u64;
        assert_eq!(
            krilla_document_start_page(doc, page_settings(50.0, 50.0), &mut token),
            status::OK
        );
        assert!(!seen.contains(&token), "token {token} was reused");
        seen.push(token);
        assert_eq!(krilla_document_close_page(doc, token), status::OK);
    }

    assert_eq!(krilla_document_free(doc), status::OK);
}

/// Freeing with a page still open must release both boxes rather than leaking them, and must
/// not trip the assertions in krilla's `Surface::drop`. Miri is what actually proves the
/// first half.
#[test]
fn free_with_a_page_open_is_clean() {
    let doc = new_document();

    let mut token = 0u64;
    assert_eq!(
        krilla_document_start_page(doc, page_settings(80.0, 80.0), &mut token),
        status::OK
    );

    assert_eq!(krilla_document_free(doc), status::OK);
}

#[test]
fn free_of_null_is_a_no_op() {
    assert_eq!(krilla_document_free(ptr::null_mut()), status::OK);
}
