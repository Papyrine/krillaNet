//! Embedded file attachments, and embedding pages from existing PDFs.

use std::sync::Arc;

use krilla::embed::{AssociationKind, EmbeddedFile, MimeType};
use krilla::pdf::{Pdf, PdfDocument};

use crate::api::metadata::KrillaDateTime;
use crate::guard::{ffi, ffi_doc};
use crate::handle;
use crate::status;
use crate::types::KrillaSize;

fn association_kind(value: i32) -> Result<AssociationKind, i32> {
    match value {
        0 => Ok(AssociationKind::Source),
        1 => Ok(AssociationKind::Data),
        2 => Ok(AssociationKind::Alternative),
        3 => Ok(AssociationKind::Supplement),
        4 => Ok(AssociationKind::Unspecified),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

ffi_doc! {
    /// Attaches a file to the document.
    ///
    /// `association_kind` describes how the attachment relates to the document — 0 source,
    /// 1 data, 2 alternative, 3 supplement, 4 unspecified. PDF/A-3 and PDF/A-4f require it to
    /// be meaningful rather than a default.
    ///
    /// Returns `INVALID_ARGUMENT` if a file is already embedded under the same path, which is
    /// how krilla signals the collision.
    #[allow(clippy::too_many_arguments)]
    fn krilla_document_embed_file(
        doc,
        path_ptr: *const u8,
        path_len: usize,
        mime_ptr: *const u8,
        mime_len: usize,
        description_ptr: *const u8,
        description_len: usize,
        data_ptr: *const u8,
        data_len: usize,
        kind: i32,
        modification_date: KrillaDateTime,
        has_modification_date: bool,
        compress: i32,
    ) {
        // SAFETY: R4 — borrowed UTF-8 for the duration of the call.
        let path = unsafe { handle::str_arg(path_ptr, path_len)? }.to_owned();

        // SAFETY: R4 — optional, borrowed for the call.
        let mime = unsafe { handle::opt_str_arg(mime_ptr, mime_len)? };

        // SAFETY: R4 — optional, borrowed for the call.
        let description = unsafe { handle::opt_str_arg(description_ptr, description_len)? };

        // SAFETY: R3 — readable for the call; copied into `Data` immediately below.
        let data = unsafe { handle::slice(data_ptr, data_len)? };

        // krilla validates the mime type and rejects a malformed one, so a bad string is a
        // caller error rather than something to pass through.
        let mime_type = match mime {
            Some(value) => Some(MimeType::new(value).ok_or(status::INVALID_ARGUMENT)?),
            None => None,
        };

        let file = EmbeddedFile {
            path,
            mime_type,
            description: description.map(str::to_owned),
            association_kind: association_kind(kind)?,
            data: data.to_vec().into(),
            modification_date: has_modification_date.then(|| modification_date.into()),
            // -1 leaves the choice to krilla; 0 and 1 force it off and on.
            compress: match compress {
                0 => Some(false),
                1 => Some(true),
                _ => None,
            },
            location: None,
        };

        match doc.doc_mut()?.embed_file(file) {
            Some(()) => Ok(status::OK),
            None => {
                crate::guard::set_last_error("a file is already embedded under this path");
                Err(status::INVALID_ARGUMENT)
            }
        }
    }
}

/// An existing PDF, parsed so its pages can be embedded.
///
/// The page count is captured at load time: krilla's `PdfDocument` keeps its `pages()`
/// accessor crate-private, so the underlying `Pdf` has to be asked before it is wrapped.
pub struct KrillaPdfDocument {
    pub(crate) inner: PdfDocument,
    pub(crate) page_count: usize,
}

ffi! {
    /// Parses an existing PDF document from memory.
    ///
    /// The bytes are copied (rule R3).
    fn krilla_pdf_new(data_ptr: *const u8, data_len: usize, out: *mut *mut KrillaPdfDocument) {
        // SAFETY: R3 — readable for the call; copied immediately below.
        let bytes = unsafe { handle::slice(data_ptr, data_len)? };

        if bytes.is_empty() {
            return Err(status::INVALID_ARGUMENT);
        }

        let pdf = match Pdf::new(bytes.to_vec()) {
            Ok(pdf) => pdf,
            Err(error) => {
                crate::guard::set_last_error(format!("{error:?}"));
                return Err(status::INVALID_ARGUMENT);
            }
        };

        let page_count = pdf.pages().len();

        let document = KrillaPdfDocument {
            inner: PdfDocument::new(Arc::new(pdf)),
            page_count,
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(document))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases a parsed PDF.
    fn krilla_pdf_free(pdf: *mut KrillaPdfDocument) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(pdf) };
        Ok(status::OK)
    }
}

ffi! {
    /// Writes the number of pages in a parsed PDF.
    fn krilla_pdf_page_count(pdf: *const KrillaPdfDocument, out: *mut usize) {
        // SAFETY: R1 — live handle.
        let pdf = unsafe { handle::as_ref(pdf)? };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, pdf.page_count)? };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Draws one page of an existing PDF into the given size on the open surface.
    ///
    /// An out-of-range `page_index` is reported when the document is finished rather than
    /// here, matching krilla, which defers the check.
    fn krilla_surface_draw_pdf_page(
        doc,
        token: u64,
        pdf: *const KrillaPdfDocument,
        size: KrillaSize,
        page_index: usize,
    ) {
        // SAFETY: R1 — live handle.
        let pdf = unsafe { handle::as_ref(pdf)? };
        let size = krilla::geom::Size::try_from(size)?;

        // Cheap to clone: PdfDocument is Arc-backed.
        let inner = pdf.inner.clone();
        doc.surface_mut(token)?.draw_pdf_page(&inner, size, page_index);
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Appends whole pages from an existing PDF to this document.
    ///
    /// Unlike `krilla_surface_draw_pdf_page`, which paints a page into an area of a page you
    /// are composing, this adds them as pages in their own right.
    fn krilla_document_embed_pdf_pages(
        doc,
        pdf: *const KrillaPdfDocument,
        indices_ptr: *const usize,
        indices_len: usize,
    ) {
        // SAFETY: R1 — live handle.
        let pdf = unsafe { handle::as_ref(pdf)? };

        if indices_len == 0 {
            return Ok(status::OK);
        }

        if indices_ptr.is_null() {
            return Err(status::NULL_ARGUMENT);
        }

        // SAFETY: R3 — the caller guarantees `indices_len` readable elements for the call.
        let indices = unsafe { std::slice::from_raw_parts(indices_ptr, indices_len) };

        let inner = pdf.inner.clone();
        doc.doc_mut()?.embed_pdf_pages(&inner, indices);
        Ok(status::OK)
    }
}
