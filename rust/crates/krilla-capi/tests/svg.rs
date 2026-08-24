//! SVG parsing, sizing, drawing, and the resolver that is deliberately not wired up.

use std::ptr;

use krilla_capi::api::svg::krilla_svg_supported;
use krilla_capi::status;

#[test]
fn supported_matches_the_build() {
    let mut supported = 0u32;
    assert_eq!(krilla_svg_supported(&mut supported), status::OK);
    assert_eq!(supported, u32::from(cfg!(feature = "svg")));
}

#[test]
fn supported_rejects_a_null_out() {
    assert_eq!(krilla_svg_supported(ptr::null_mut()), status::NULL_ARGUMENT);
}

#[cfg(feature = "svg")]
mod enabled {
    use std::ptr;

    use krilla_capi::api::document::{
        KrillaErrorObject, krilla_document_close_page, krilla_document_finish,
        krilla_document_free, krilla_document_new, krilla_document_start_page,
    };
    use krilla_capi::api::error::krilla_buffer_free;
    use krilla_capi::api::svg::{
        KrillaSvg, krilla_surface_draw_svg, krilla_svg_free, krilla_svg_new,
        krilla_svg_options_add_font, krilla_svg_options_free, krilla_svg_options_new,
        krilla_svg_options_set_default_family, krilla_svg_size,
    };
    use krilla_capi::status;
    use krilla_capi::types::{KrillaPageSettings, KrillaRect, KrillaSize};

    const SQUARE: &str = r#"<svg xmlns="http://www.w3.org/2000/svg" width="64" height="32"><rect width="64" height="32" fill="red"/></svg>"#;

    fn parse(source: &str) -> *mut KrillaSvg {
        let mut svg = ptr::null_mut();
        assert_eq!(
            krilla_svg_new(source.as_ptr(), source.len(), ptr::null(), &mut svg),
            status::OK
        );
        svg
    }

    fn size_of(svg: *mut KrillaSvg) -> (f32, f32) {
        let mut width = 0.0f32;
        let mut height = 0.0f32;
        assert_eq!(krilla_svg_size(svg, &mut width, &mut height), status::OK);
        (width, height)
    }

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

    /// Draws `source` onto one page and returns the finished PDF.
    fn render(source: &str) -> Vec<u8> {
        let mut doc = ptr::null_mut();
        assert_eq!(krilla_document_new(&mut doc), status::OK);

        let mut token = 0u64;
        assert_eq!(
            krilla_document_start_page(doc, page_settings(100.0, 100.0), &mut token),
            status::OK
        );

        let svg = parse(source);
        assert_eq!(
            krilla_surface_draw_svg(
                doc,
                token,
                svg,
                KrillaSize {
                    width: 64.0,
                    height: 32.0,
                },
                true,
                4.0,
            ),
            status::OK
        );

        assert_eq!(krilla_svg_free(svg), status::OK);
        assert_eq!(krilla_document_close_page(doc, token), status::OK);

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
        assert_eq!(krilla_document_free(doc), status::OK);
        bytes
    }

    #[test]
    fn width_and_height_attributes_are_the_size() {
        let svg = parse(SQUARE);
        assert_eq!(size_of(svg), (64.0, 32.0));
        assert_eq!(krilla_svg_free(svg), status::OK);
    }

    /// A `viewBox` with no `width`/`height` sizes the document, which is what makes an SVG
    /// scalable in the first place — and what a layout engine needs for an aspect ratio.
    #[test]
    fn a_viewbox_alone_is_the_size() {
        let source = r#"<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 10"/>"#;
        let svg = parse(source);
        assert_eq!(size_of(svg), (20.0, 10.0));
        assert_eq!(krilla_svg_free(svg), status::OK);
    }

    /// Neither attribute leaves usvg's own 100x100 default, which is a real size rather than a
    /// failure: an SVG with no intrinsic dimensions still has to lay out somewhere.
    #[test]
    fn no_size_at_all_falls_back_to_the_usvg_default() {
        let source = r#"<svg xmlns="http://www.w3.org/2000/svg"/>"#;
        let svg = parse(source);
        assert_eq!(size_of(svg), (100.0, 100.0));
        assert_eq!(krilla_svg_free(svg), status::OK);
    }

    #[test]
    fn malformed_data_is_rejected() {
        let source = "<svg><unclosed>";
        let mut svg = ptr::null_mut();
        assert_eq!(
            krilla_svg_new(source.as_ptr(), source.len(), ptr::null(), &mut svg),
            status::INVALID_SVG
        );
        assert!(svg.is_null());
    }

    #[test]
    fn empty_data_is_rejected() {
        let mut svg = ptr::null_mut();
        assert_eq!(
            krilla_svg_new(ptr::null(), 0, ptr::null(), &mut svg),
            status::INVALID_SVG
        );
    }

    #[test]
    fn a_null_svg_is_rejected() {
        let mut width = 0.0f32;
        let mut height = 0.0f32;
        assert_eq!(
            krilla_svg_size(ptr::null(), &mut width, &mut height),
            status::NULL_ARGUMENT
        );
    }

    #[test]
    fn options_round_trip() {
        let mut options = ptr::null_mut();
        assert_eq!(krilla_svg_options_new(&mut options), status::OK);

        let family = "Liberation Sans";
        assert_eq!(
            krilla_svg_options_set_default_family(options, family.as_ptr(), family.len()),
            status::OK
        );

        // Not a font, so it is rejected rather than quietly poisoning the database.
        assert_eq!(
            krilla_svg_options_add_font(options, ptr::null(), 0),
            status::INVALID_FONT
        );

        assert_eq!(krilla_svg_options_free(options), status::OK);
    }

    #[test]
    fn a_non_positive_filter_scale_is_rejected() {
        let mut doc = ptr::null_mut();
        assert_eq!(krilla_document_new(&mut doc), status::OK);

        let mut token = 0u64;
        assert_eq!(
            krilla_document_start_page(doc, page_settings(100.0, 100.0), &mut token),
            status::OK
        );

        let svg = parse(SQUARE);
        let size = KrillaSize {
            width: 64.0,
            height: 32.0,
        };

        assert_eq!(
            krilla_surface_draw_svg(doc, token, svg, size, true, 0.0),
            status::INVALID_ARGUMENT
        );
        assert_eq!(
            krilla_surface_draw_svg(doc, token, svg, size, true, f32::NAN),
            status::INVALID_ARGUMENT
        );

        assert_eq!(krilla_svg_free(svg), status::OK);
        assert_eq!(krilla_document_close_page(doc, token), status::OK);
        assert_eq!(krilla_document_free(doc), status::OK);
    }

    /// Ignored under Miri: usvg parsing and krilla serialization over a real document costs
    /// minutes there, and the pointer choreography on this path is covered by the tests above.
    #[test]
    #[cfg_attr(miri, ignore)]
    fn drawing_produces_content() {
        let bytes = render(SQUARE);
        assert!(bytes.starts_with(b"%PDF-"));
        // An empty page serializes to well under a kilobyte; drawn content pushes it past that.
        assert!(
            bytes.len() > 800,
            "expected drawn content, got {} bytes",
            bytes.len()
        );
    }

    /// The reason `hardened()` exists.
    ///
    /// usvg's stock `resolve_string` takes an `<image href>` that is not a data URI, joins it to
    /// the working directory and reads whatever it finds, inlining it into the tree. An SVG is
    /// content and frequently comes from somewhere untrusted, so that is an arbitrary file read
    /// reachable from a document, and the bytes then land in the PDF — an exfiltration primitive
    /// rather than merely a surprise.
    ///
    /// Differential, and it has to be: the two documents differ only in whether the file the
    /// `href` names exists, so the assertion holds if and only if nothing is read. An earlier
    /// version pointed the `href` at this crate's `Cargo.toml` and asserted its bytes were
    /// absent from the output — which passed with the resolver fully live, because usvg sniffs
    /// the format first and drops anything that is not an image. A test of this has to name
    /// something usvg would actually accept.
    #[test]
    #[cfg_attr(miri, ignore)]
    fn a_file_href_resolves_to_nothing() {
        let dir = std::env::temp_dir().join("krilla-capi-svg-href");
        std::fs::create_dir_all(&dir).unwrap();
        let path = dir.join("payload.svg");

        // Two hundred bars, so inlining this is unmistakable in the content stream rather than
        // a difference of a few bytes that compression could account for.
        let mut payload =
            String::from(r#"<svg xmlns="http://www.w3.org/2000/svg" width="64" height="32">"#);
        for x in 0..200 {
            payload.push_str(&format!(
                r#"<rect x="{x}" y="0" width="1" height="32" fill="blue"/>"#
            ));
        }
        payload.push_str("</svg>");
        std::fs::write(&path, &payload).unwrap();

        let document = |href: String| {
            format!(
                r#"<svg xmlns="http://www.w3.org/2000/svg" width="64" height="32">
                     <image href="{href}" width="64" height="32"/>
                   </svg>"#
            )
        };

        let slashed = path.display().to_string().replace(r"\", "/");
        let present = render(&document(slashed.clone())).len();
        let absent = render(&document(format!("{slashed}.does-not-exist"))).len();

        std::fs::remove_file(&path).ok();

        assert_eq!(
            present, absent,
            "a file that exists rendered differently from one that does not, so the resolver              is still reading from disk"
        );
    }

    /// A `data:` URI is admitted, because its bytes are already in the document — the same rule
    /// the managed image store follows. Without this the hardening above would read as "SVG
    /// cannot embed images at all", which is a different and much larger restriction.
    #[test]
    #[cfg_attr(miri, ignore)]
    fn a_data_uri_image_is_admitted() {
        // A 1x1 red PNG.
        const PIXEL: &str = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

        let source = format!(
            r#"<svg xmlns="http://www.w3.org/2000/svg" width="64" height="32">
                 <image href="{PIXEL}" width="64" height="32"/>
               </svg>"#
        );

        let with_image = render(&source).len();
        let without = render(SQUARE).len();

        assert!(
            with_image > without,
            "the data URI drew nothing: {with_image} bytes against {without}"
        );
    }
}
