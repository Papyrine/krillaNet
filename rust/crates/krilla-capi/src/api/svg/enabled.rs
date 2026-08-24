//! The half of the SVG surface that needs usvg and krilla-svg.

use krilla_svg::{SurfaceExt, SvgSettings};
use usvg::{Options, Tree};

use crate::guard::{ffi, ffi_doc};
use crate::handle;
use crate::status;
use crate::types::KrillaSize;

/// Parse-time settings: the fonts an SVG's `<text>` is shaped against, and the family it falls
/// back to when it names one that was never registered.
///
/// Separate from the draw call because usvg resolves text while it parses — a `<text>` element
/// becomes positioned glyphs in the tree, not a node carrying a family name — so fonts supplied
/// afterwards would arrive too late to reach anything.
pub struct KrillaSvgOptions {
    pub(crate) inner: Options<'static>,
}

/// A parsed SVG document.
///
/// Parsing is the expensive half, so one handle should be reused wherever the same SVG appears;
/// krilla-svg draws it into whatever size each call asks for.
pub struct KrillaSvg {
    pub(crate) tree: Tree,
}

/// Builds usvg options with the file-reading image resolver removed.
///
/// usvg's default `resolve_string` takes an `<image href>` that is not a data URI, joins it to
/// `resources_dir` — the process's working directory when that is unset, as it is here — and
/// reads whatever it finds. An SVG is content, frequently from somewhere untrusted, so that is
/// an arbitrary-file-read reachable from a document; the bytes would then be embedded in the
/// PDF, which makes it an exfiltration primitive rather than merely a surprise.
///
/// The data resolver is left alone, matching the rule the managed image store already follows:
/// a `data:` URI's bytes are already in the document, so admitting them grants nothing.
fn hardened() -> Options<'static> {
    let mut options = Options::default();
    options.image_href_resolver.resolve_string = Box::new(|_, _| None);
    options
}

ffi! {
    /// Creates SVG parse options with no fonts registered.
    fn krilla_svg_options_new(out: *mut *mut KrillaSvgOptions) {
        // SAFETY: out-parameter contract.
        unsafe {
            handle::write_out(out, handle::into_handle(KrillaSvgOptions { inner: hardened() }))?
        };
        Ok(status::OK)
    }
}

ffi! {
    /// Registers a font for `<text>` inside an SVG parsed with these options.
    ///
    /// Nothing is loaded from the host: usvg is built with `system-fonts` available, because
    /// krilla-svg's own dependency enables it and cargo unions features, but the database
    /// starts empty and is never asked to enumerate. Which fonts an SVG can use is the
    /// caller's decision, for the same reason it is for the document around it.
    ///
    /// The bytes are copied (rule R3).
    fn krilla_svg_options_add_font(
        options: *mut KrillaSvgOptions,
        data_ptr: *const u8,
        data_len: usize,
    ) {
        // SAFETY: R1 — live handle.
        let options = unsafe { handle::as_mut(options)? };

        // SAFETY: R3 — readable for the duration of the call; copied immediately below.
        let bytes = unsafe { handle::slice(data_ptr, data_len)? };

        if bytes.is_empty() {
            return Err(status::INVALID_FONT);
        }

        options.inner.fontdb_mut().load_font_data(bytes.to_vec());
        Ok(status::OK)
    }
}

ffi! {
    /// Sets the family used for text that names no family, or names one that was not registered.
    ///
    /// usvg's own default is "Times New Roman", which resolves against an empty database to
    /// nothing at all — so without this, text in an SVG that does not name a registered family
    /// silently disappears.
    fn krilla_svg_options_set_default_family(
        options: *mut KrillaSvgOptions,
        family_ptr: *const u8,
        family_len: usize,
    ) {
        // SAFETY: R1 — live handle.
        let options = unsafe { handle::as_mut(options)? };

        // SAFETY: R4 — UTF-8, borrowed for the call; copied into the options below.
        let family = unsafe { handle::str_arg(family_ptr, family_len)? };
        options.inner.font_family = family.to_owned();
        Ok(status::OK)
    }
}

ffi! {
    /// Releases SVG parse options.
    fn krilla_svg_options_free(options: *mut KrillaSvgOptions) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(options) };
        Ok(status::OK)
    }
}

ffi! {
    /// Parses an SVG document, plain or gzip-compressed.
    ///
    /// `options` may be null, which parses against the same hardened defaults with no fonts
    /// registered — correct for the great majority of SVGs, which carry no text.
    ///
    /// The bytes are copied where they are retained (rule R3): usvg builds an owned tree, so
    /// the caller need not keep the buffer alive.
    fn krilla_svg_new(
        data_ptr: *const u8,
        data_len: usize,
        options: *const KrillaSvgOptions,
        out: *mut *mut KrillaSvg,
    ) {
        // SAFETY: R3 — readable for the duration of the call; usvg copies what it retains.
        let bytes = unsafe { handle::slice(data_ptr, data_len)? };

        if bytes.is_empty() {
            return Err(status::INVALID_SVG);
        }

        let fallback;
        let options = if options.is_null() {
            fallback = KrillaSvgOptions { inner: hardened() };
            &fallback
        } else {
            // SAFETY: R1 — live handle.
            unsafe { handle::as_ref(options)? }
        };

        let tree = match Tree::from_data(bytes, &options.inner) {
            Ok(tree) => tree,
            Err(error) => {
                crate::guard::set_last_error(error.to_string());
                return Err(status::INVALID_SVG);
            }
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(KrillaSvg { tree }))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Writes the SVG's intrinsic size, in CSS pixels.
    ///
    /// This is usvg's resolved size rather than the raw `width`/`height` attributes: a document
    /// giving only a `viewBox` reports the viewBox's extent, and one giving neither reports
    /// usvg's 100x100 default. Always strictly positive, which is what lets a layout engine
    /// divide by it to get an aspect ratio.
    fn krilla_svg_size(svg: *const KrillaSvg, out_width: *mut f32, out_height: *mut f32) {
        // SAFETY: R1 — live handle.
        let svg = unsafe { handle::as_ref(svg)? };
        let size = svg.tree.size();

        // SAFETY: out-parameter contract.
        unsafe {
            handle::write_out(out_width, size.width())?;
            handle::write_out(out_height, size.height())?;
        }

        Ok(status::OK)
    }
}

ffi! {
    /// Releases a parsed SVG.
    fn krilla_svg_free(svg: *mut KrillaSvg) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(svg) };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Draws an SVG into the given size, in surface units.
    ///
    /// The tree is scaled from its intrinsic size to `size` and clipped to it, so aspect ratio
    /// is the caller's concern exactly as it is for a raster image; `krilla_svg_size` provides
    /// the numbers needed to preserve it.
    ///
    /// `embed_text` keeps text as selectable, searchable glyph runs. Turning it off outlines
    /// every glyph, which is larger, unsearchable, and needed only where a font's licence
    /// forbids embedding. `filter_scale` is the resolution filters are rasterised at, filters
    /// being the one part of SVG with no PDF equivalent.
    fn krilla_surface_draw_svg(
        doc,
        token: u64,
        svg: *const KrillaSvg,
        size: KrillaSize,
        embed_text: bool,
        filter_scale: f32,
    ) {
        // SAFETY: R1 — live handle.
        let svg = unsafe { handle::as_ref(svg)? };
        let size = krilla::geom::Size::try_from(size)?;

        if !filter_scale.is_finite() || filter_scale <= 0.0 {
            return Err(status::INVALID_ARGUMENT);
        }

        let settings = SvgSettings {
            embed_text,
            filter_scale,
        };

        doc.surface_mut(token)?.draw_svg(&svg.tree, size, settings);
        Ok(status::OK)
    }
}
