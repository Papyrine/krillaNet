//! Document and page lifecycle exports.

use std::sync::atomic::{AtomicU64, Ordering};

use krilla::Document;
use krilla::geom::Rect;
use krilla::page::PageSettings;

use crate::document::KrillaDocument;
use crate::guard::{ffi, ffi_doc};
use crate::handle;
use crate::status;
use crate::types::{
    KrillaPageSettings, PAGE_BOX_ART, PAGE_BOX_BLEED, PAGE_BOX_CROP, PAGE_BOX_MEDIA, PAGE_BOX_TRIM,
};

/// Source of the per-document identity used for cross-document handle checks.
///
/// Atomic rather than a plain counter because documents may be created on different threads,
/// even though a single document is not thread safe (rule R6).
static NEXT_DOCUMENT_ID: AtomicU64 = AtomicU64::new(1);

pub(crate) fn next_document_id() -> u64 {
    NEXT_DOCUMENT_ID.fetch_add(1, Ordering::Relaxed)
}

/// Reads one optional page box out of the flags.
fn optional_box(
    settings: &KrillaPageSettings,
    flag: u32,
    value: crate::types::KrillaRect,
) -> Result<Option<Rect>, i32> {
    if settings.present & flag == 0 {
        return Ok(None);
    }

    Ok(Some(Rect::try_from(value)?))
}

impl TryFrom<KrillaPageSettings> for PageSettings {
    type Error = i32;

    fn try_from(value: KrillaPageSettings) -> Result<Self, Self::Error> {
        let settings = PageSettings::from_wh(value.width, value.height)
            .ok_or(status::INVALID_GEOMETRY)?
            .with_crop_box(optional_box(&value, PAGE_BOX_CROP, value.crop_box)?)
            .with_bleed_box(optional_box(&value, PAGE_BOX_BLEED, value.bleed_box)?)
            .with_trim_box(optional_box(&value, PAGE_BOX_TRIM, value.trim_box)?)
            .with_art_box(optional_box(&value, PAGE_BOX_ART, value.art_box)?);

        // `from_wh` already installs a media box covering the whole surface, so only override
        // it when the caller asked for something different.
        Ok(
            match optional_box(&value, PAGE_BOX_MEDIA, value.media_box)? {
                Some(media) => settings.with_media_box(Some(media)),
                None => settings,
            },
        )
    }
}

ffi! {
    /// Creates a document with default serialize settings.
    ///
    /// The handle is owned by the caller (rule R1) and released with `krilla_document_free`.
    fn krilla_document_new(out: *mut *mut KrillaDocument) {
        let document = KrillaDocument::new(Document::new(), next_document_id());

        // SAFETY: out-parameter contract; `write_out` null-checks.
        unsafe { handle::write_out(out, handle::into_handle(document))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases a document, along with any page left open.
    ///
    /// Safe to call on a poisoned document — that is the one operation poisoning still
    /// permits — and safe to call with a null pointer.
    fn krilla_document_free(doc: *mut KrillaDocument) {
        // SAFETY: R1 — the caller surrenders the handle and does not use it again.
        unsafe { handle::drop_handle(doc) };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Opens a page, writing the token that identifies it to `out_token`.
    ///
    /// Only one page may be open at a time; a second call returns `PAGE_ALREADY_OPEN`. Every
    /// subsequent drawing call quotes the token, so work aimed at a page that has since been
    /// closed is rejected rather than silently misdirected.
    fn krilla_document_start_page(doc, settings: KrillaPageSettings, out_token: *mut u64) {
        let settings = PageSettings::try_from(settings)?;
        let token = doc.start_page(settings)?;

        // SAFETY: out-parameter contract; `write_out` null-checks.
        unsafe { handle::write_out(out_token, token)? };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Closes the open page, flushing its content stream into the document.
    ///
    /// Returns `POP_UNDERFLOW` if the caller left a `push` unmatched. The page still closes
    /// cleanly in that case — the shim rebalances first, because an unbalanced surface trips
    /// an assertion inside krilla's `Drop` and a panic in `drop` aborts the process.
    fn krilla_document_close_page(doc, token: u64) {
        doc.close_page(token)?;
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Writes the token of the open page to `out_token`, or 0 if no page is open.
    fn krilla_document_open_page(doc, out_token: *mut u64) {
        let token = doc.open_token().unwrap_or(0);

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out_token, token)? };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Serializes the document, writing the PDF bytes to `out_ptr` / `out_len`.
    ///
    /// Consumes the document's contents: every later call but `krilla_document_free` returns
    /// `FINISHED`. Returns `PAGE_ALREADY_OPEN` if a page is still open, rather than closing
    /// it implicitly — an unclosed page means the caller lost track of their own state, and
    /// papering over that would hide the bug behind output that looks almost right.
    ///
    /// On `KRILLA_ERROR` the document's validation failures are available through the error
    /// object written to `out_error`. The buffer is owned by the caller (rule R5) and freed
    /// with `krilla_buffer_free`.
    fn krilla_document_finish(
        doc,
        out_ptr: *mut *mut u8,
        out_len: *mut usize,
        out_error: *mut *mut crate::api::document::KrillaErrorObject,
    ) {
        match doc.finish()? {
            Ok(bytes) => {
                // SAFETY: out-parameter contract; `buffer_out` null-checks both pointers.
                unsafe { handle::buffer_out(bytes, out_ptr, out_len)? };
                Ok(status::OK)
            }
            Err(error) => {
                let object = KrillaErrorObject::new(error);

                // SAFETY: out-parameter contract.
                unsafe { handle::write_out(out_error, handle::into_handle(object))? };
                Ok(status::KRILLA_ERROR)
            }
        }
    }
}

/// A captured `KrillaError`, kept alive so the managed side can walk its structure.
///
/// krilla batches validation failures and only surfaces them at `finish`, as a
/// `Vec<(ValidationError, Validators)>` of up to 28 distinct variants, most carrying a
/// `Location` and some carrying font or glyph context. Flattening that to a string would
/// throw away exactly the parts a caller needs to fix the document, so the error is retained
/// behind a handle and read through accessors instead.
///
/// Populated in Phase 1e; the object exists now so `krilla_document_finish` has its final
/// signature and the ABI does not shift underneath the managed bindings.
pub struct KrillaErrorObject {
    /// The captured failure, retained so accessors can walk its structure.
    pub error: krilla::error::KrillaError,
}

impl KrillaErrorObject {
    /// Captures an error returned by `Document::finish`.
    pub fn new(error: krilla::error::KrillaError) -> Self {
        Self { error }
    }
}

ffi! {
    /// Releases an error object.
    fn krilla_error_free(error: *mut KrillaErrorObject) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(error) };
        Ok(status::OK)
    }
}

ffi! {
    /// Writes a human-readable description of an error object, as UTF-8.
    ///
    /// Validation failures are enumerated rather than counted. krilla's own `Display` for the
    /// batch says only "validation failed with N errors", which tells a caller nothing about
    /// what to fix — and the individual variants carry exactly that: which rule, which
    /// conformance profile, and where.
    fn krilla_error_message(
        error: *const KrillaErrorObject,
        out_ptr: *mut *mut u8,
        out_len: *mut usize,
    ) {
        // SAFETY: R1 — live handle from `krilla_document_finish`.
        let error = unsafe { handle::as_ref(error)? };

        let message = match &error.error {
            krilla::error::KrillaError::Validation(errors) => {
                let mut text = format!(
                    "validation failed with {} {}",
                    errors.len(),
                    if errors.len() == 1 { "error" } else { "errors" }
                );

                for (error, validators) in errors {
                    // Debug rather than Display: `ValidationError` has no Display, and its
                    // Debug output already names the variant and its payload, which is the
                    // actionable part.
                    text.push_str(&format!("\n  - {error:?} (required by {validators:?})"));
                }

                text
            }
            other => other.to_string(),
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::string_out(message, out_ptr, out_len)? };
        Ok(status::OK)
    }
}
