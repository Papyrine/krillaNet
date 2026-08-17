//! Document metadata, serialize settings and PDF conformance configuration.

use krilla::configure::{Accessibility, Archival, ConfigurationBuilder, PdfVersion};
use krilla::metadata::{DateTime, Metadata, PageLayout, TextDirection};

use crate::document::KrillaDocument;
use crate::guard::{ffi, ffi_doc};
use crate::handle;
use crate::status;

/// A metadata builder.
///
/// krilla's `Metadata` setters take `self` by value, which a C ABI cannot express, so the
/// value lives behind an `Option` and each setter takes and reassigns it.
pub struct KrillaMetadata {
    inner: Option<Metadata>,
}

impl KrillaMetadata {
    /// Applies a consuming builder method in place.
    fn update<F>(&mut self, apply: F) -> Result<(), i32>
    where
        F: FnOnce(Metadata) -> Metadata,
    {
        let current = self.inner.take().ok_or(status::CONSUMED)?;
        self.inner = Some(apply(current));
        Ok(())
    }
}

/// A date, as PDF records them.
///
/// Only the year is required; every other component is optional and omitted when absent,
/// which matches krilla's builder rather than forcing a fabricated midnight.
#[repr(C)]
#[derive(Copy, Clone)]
#[allow(missing_docs)]
pub struct KrillaDateTime {
    pub year: u16,
    /// 1..=12, or 0 for absent.
    pub month: u8,
    /// 1..=31, or 0 for absent.
    pub day: u8,
    /// 0..=23. Only read when `has_time` is set, since 0 is a valid hour.
    pub hour: u8,
    pub minute: u8,
    pub second: u8,
    pub has_time: u8,
    pub has_utc_offset: u8,
    pub utc_offset_hour: i8,
    pub utc_offset_minute: u8,
}

impl From<KrillaDateTime> for DateTime {
    fn from(value: KrillaDateTime) -> Self {
        let mut date = DateTime::new(value.year);

        if value.month != 0 {
            date = date.month(value.month);
        }

        if value.day != 0 {
            date = date.day(value.day);
        }

        if value.has_time != 0 {
            date = date
                .hour(value.hour)
                .minute(value.minute)
                .second(value.second);
        }

        if value.has_utc_offset != 0 {
            date = date
                .utc_offset_hour(value.utc_offset_hour)
                .utc_offset_minute(value.utc_offset_minute);
        }

        date
    }
}

fn text_direction(value: i32) -> Result<TextDirection, i32> {
    match value {
        0 => Ok(TextDirection::LeftToRight),
        1 => Ok(TextDirection::RightToLeft),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

fn page_layout(value: i32) -> Result<PageLayout, i32> {
    match value {
        0 => Ok(PageLayout::SinglePage),
        1 => Ok(PageLayout::OneColumn),
        2 => Ok(PageLayout::TwoColumnLeft),
        3 => Ok(PageLayout::TwoColumnRight),
        4 => Ok(PageLayout::TwoPageLeft),
        5 => Ok(PageLayout::TwoPageRight),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

ffi! {
    /// Creates an empty metadata builder.
    fn krilla_metadata_new(out: *mut *mut KrillaMetadata) {
        let metadata = KrillaMetadata {
            inner: Some(Metadata::new()),
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(metadata))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases a metadata builder.
    fn krilla_metadata_free(metadata: *mut KrillaMetadata) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(metadata) };
        Ok(status::OK)
    }
}

/// Generates a string setter for a `Metadata` builder method.
macro_rules! metadata_string_setter {
    ($name:ident, $method:ident, $doc:expr) => {
        ffi! {
            #[doc = $doc]
            fn $name(metadata: *mut KrillaMetadata, ptr: *const u8, len: usize) {
                // SAFETY: R4 — borrowed UTF-8 for the duration of the call.
                let text = unsafe { handle::str_arg(ptr, len)? }.to_owned();

                // SAFETY: R1 — live handle.
                unsafe { handle::as_mut(metadata)? }.update(|m| m.$method(text))?;
                Ok(status::OK)
            }
        }
    };
}

metadata_string_setter!(
    krilla_metadata_set_title,
    title,
    "Sets the document title. Required by PDF/UA."
);
metadata_string_setter!(
    krilla_metadata_set_description,
    description,
    "Sets the document description (the PDF `Subject` field)."
);
metadata_string_setter!(
    krilla_metadata_set_language,
    language,
    "Sets the natural language of the document, as a BCP 47 tag. Required by PDF/UA."
);
metadata_string_setter!(
    krilla_metadata_set_creator,
    creator,
    "Sets the name of the application that authored the original content."
);
metadata_string_setter!(
    krilla_metadata_set_producer,
    producer,
    "Sets the name of the application that produced the PDF."
);
metadata_string_setter!(
    krilla_metadata_set_document_id,
    document_id,
    "Sets the document identifier.\n\nSetting this together with a creation date is what makes output byte-reproducible; \
     without both, every run differs."
);

ffi! {
    /// Sets the document authors, from an array of UTF-8 pointer/length pairs.
    fn krilla_metadata_set_authors(
        metadata: *mut KrillaMetadata,
        ptrs: *const *const u8,
        lens: *const usize,
        count: usize,
    ) {
        // SAFETY: R3/R4 — forwarded to the caller's contract on the parallel arrays.
        let authors = unsafe { string_array(ptrs, lens, count)? };

        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(metadata)? }.update(|m| m.authors(authors))?;
        Ok(status::OK)
    }
}

ffi! {
    /// Sets the document keywords, from an array of UTF-8 pointer/length pairs.
    fn krilla_metadata_set_keywords(
        metadata: *mut KrillaMetadata,
        ptrs: *const *const u8,
        lens: *const usize,
        count: usize,
    ) {
        // SAFETY: R3/R4 — forwarded to the caller's contract on the parallel arrays.
        let keywords = unsafe { string_array(ptrs, lens, count)? };

        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(metadata)? }.update(|m| m.keywords(keywords))?;
        Ok(status::OK)
    }
}

/// Reads a list of strings passed as parallel pointer and length arrays.
///
/// Parallel arrays rather than an array of structs so the managed side can pin a
/// `ReadOnlySpan` of each without defining a further ABI struct.
///
/// # Safety
///
/// Both arrays must hold `count` readable elements, and each pointer/length pair must
/// describe readable UTF-8, all for the duration of the call.
unsafe fn string_array(
    ptrs: *const *const u8,
    lens: *const usize,
    count: usize,
) -> Result<Vec<String>, i32> {
    if count == 0 {
        return Ok(Vec::new());
    }

    if ptrs.is_null() || lens.is_null() {
        return Err(status::NULL_ARGUMENT);
    }

    // SAFETY: caller contract on both arrays.
    let (ptrs, lens) = unsafe {
        (
            std::slice::from_raw_parts(ptrs, count),
            std::slice::from_raw_parts(lens, count),
        )
    };

    ptrs.iter()
        .zip(lens)
        // SAFETY: caller contract — each pair describes readable UTF-8 for the call.
        .map(|(ptr, len)| unsafe { handle::str_arg(*ptr, *len).map(str::to_owned) })
        .collect()
}

ffi! {
    /// Sets the creation date.
    ///
    /// Needed for byte-reproducible output, and required by PDF/A.
    fn krilla_metadata_set_creation_date(metadata: *mut KrillaMetadata, date: KrillaDateTime) {
        let date = DateTime::from(date);

        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(metadata)? }.update(|m| m.creation_date(date))?;
        Ok(status::OK)
    }
}

ffi! {
    /// Sets the dominant reading direction: 0 left-to-right, 1 right-to-left.
    fn krilla_metadata_set_text_direction(metadata: *mut KrillaMetadata, direction: i32) {
        let direction = text_direction(direction)?;

        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(metadata)? }.update(|m| m.text_direction(direction))?;
        Ok(status::OK)
    }
}

ffi! {
    /// Sets how a viewer should lay pages out: 0 single page, 1 one column,
    /// 2 two-column left, 3 two-column right, 4 two-page left, 5 two-page right.
    fn krilla_metadata_set_page_layout(metadata: *mut KrillaMetadata, layout: i32) {
        let layout = page_layout(layout)?;

        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(metadata)? }.update(|m| m.page_layout(layout))?;
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Attaches the metadata to the document, consuming the builder.
    ///
    /// The handle must still be released with `krilla_metadata_free`.
    fn krilla_document_set_metadata(doc, metadata: *mut KrillaMetadata) {
        // SAFETY: R1 — live handle.
        let metadata = unsafe { handle::as_mut(metadata)? };
        let value = metadata.inner.take().ok_or(status::CONSUMED)?;

        doc.doc_mut()?.set_metadata(value);
        Ok(status::OK)
    }
}

// -- Configuration ------------------------------------------------------------------------

/// PDF version discriminants.
fn pdf_version(value: i32) -> Result<PdfVersion, i32> {
    match value {
        0 => Ok(PdfVersion::Pdf14),
        1 => Ok(PdfVersion::Pdf15),
        2 => Ok(PdfVersion::Pdf16),
        3 => Ok(PdfVersion::Pdf17),
        4 => Ok(PdfVersion::Pdf20),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

/// PDF/A conformance levels. -1 means none.
fn archival(value: i32) -> Result<Option<Archival>, i32> {
    match value {
        -1 => Ok(None),
        0 => Ok(Some(Archival::A1_A)),
        1 => Ok(Some(Archival::A1_B)),
        2 => Ok(Some(Archival::A2_A)),
        3 => Ok(Some(Archival::A2_B)),
        4 => Ok(Some(Archival::A2_U)),
        5 => Ok(Some(Archival::A3_A)),
        6 => Ok(Some(Archival::A3_B)),
        7 => Ok(Some(Archival::A3_U)),
        8 => Ok(Some(Archival::A4)),
        9 => Ok(Some(Archival::A4F)),
        10 => Ok(Some(Archival::A4E)),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

/// Accessibility conformance levels. -1 means none.
fn accessibility(value: i32) -> Result<Option<Accessibility>, i32> {
    match value {
        -1 => Ok(None),
        0 => Ok(Some(Accessibility::UA1)),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

/// Options for a new document, mirrored on the managed side.
#[repr(C)]
#[derive(Copy, Clone)]
#[allow(missing_docs)]
pub struct KrillaDocumentOptions {
    /// PDF version: 0 = 1.4, 1 = 1.5, 2 = 1.6, 3 = 1.7, 4 = 2.0.
    pub pdf_version: i32,
    /// PDF/A level, or -1 for none.
    pub archival: i32,
    /// Accessibility level, or -1 for none.
    pub accessibility: i32,
    pub compress_streams: u8,
    pub ascii_compatible: u8,
    pub xmp_metadata: u8,
    /// Enables the Tagged PDF structure tree. Required for PDF/UA and for PDF/A level A.
    pub enable_tagging: u8,
    pub pretty: u8,
    pub no_device_colorspace: u8,
    pub reserved: u8,
}

ffi! {
    /// Creates a document with explicit serialize settings and conformance configuration.
    ///
    /// Returns `INVALID_ARGUMENT` if krilla rejects the combination — for instance a
    /// conformance level that requires a newer PDF version than the one requested.
    fn krilla_document_new_with(
        options: KrillaDocumentOptions,
        out: *mut *mut KrillaDocument,
    ) {
        let mut builder = ConfigurationBuilder::new().with_version(pdf_version(options.pdf_version)?);

        if let Some(archival) = archival(options.archival)? {
            builder = builder.with_archival_validator(archival);
        }

        if let Some(accessibility) = accessibility(options.accessibility)? {
            builder = builder.with_accessibility_validator(accessibility);
        }

        let configuration = match builder.finish() {
            Ok(configuration) => configuration,
            Err(error) => {
                crate::guard::set_last_error(format!("{error:?}"));
                return Err(status::INVALID_ARGUMENT);
            }
        };

        let settings = krilla::SerializeSettings {
            compress_content_streams: options.compress_streams != 0,
            ascii_compatible: options.ascii_compatible != 0,
            xmp_metadata: options.xmp_metadata != 0,
            enable_tagging: options.enable_tagging != 0,
            pretty: options.pretty != 0,
            no_device_cs: options.no_device_colorspace != 0,
            configuration,
            ..Default::default()
        };

        let document = KrillaDocument::new(
            krilla::Document::new_with(settings),
            crate::api::document::next_document_id(),
        );

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(document))? };
        Ok(status::OK)
    }
}
