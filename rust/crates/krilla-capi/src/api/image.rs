//! Raster images.

use krilla::image::Image;

use crate::guard::{ffi, ffi_doc};
use crate::handle;
use crate::status;
use crate::types::KrillaSize;

/// A decoded image.
///
/// Expensive to create and cheap to clone or hash, so one handle should be reused wherever
/// the same image appears; krilla deduplicates them in the output.
pub struct KrillaImage {
    pub(crate) inner: Image,
}

/// Encoded formats accepted by `krilla_image_new_encoded`.
pub const IMAGE_FORMAT_PNG: i32 = 0;
/// JPEG.
pub const IMAGE_FORMAT_JPEG: i32 = 1;
/// GIF.
pub const IMAGE_FORMAT_GIF: i32 = 2;
/// WebP.
pub const IMAGE_FORMAT_WEBP: i32 = 3;

ffi! {
    /// Decodes an image from encoded bytes.
    ///
    /// `interpolate` asks viewers to smooth the image when scaled up. Note that PDF/A
    /// forbids it: turning it on makes the document fail A-conformance validation at
    /// `finish`.
    ///
    /// The bytes are copied (rule R3).
    fn krilla_image_new_encoded(
        format: i32,
        data_ptr: *const u8,
        data_len: usize,
        interpolate: bool,
        out: *mut *mut KrillaImage,
    ) {
        // SAFETY: R3 — readable for the duration of the call; copied immediately below.
        let bytes = unsafe { handle::slice(data_ptr, data_len)? };

        if bytes.is_empty() {
            return Err(status::INVALID_IMAGE);
        }

        let data = bytes.to_vec().into();

        let image = match format {
            IMAGE_FORMAT_PNG => Image::from_png(data, interpolate),
            IMAGE_FORMAT_JPEG => Image::from_jpeg(data, interpolate),
            IMAGE_FORMAT_GIF => Image::from_gif(data, interpolate),
            IMAGE_FORMAT_WEBP => Image::from_webp(data, interpolate),
            _ => return Err(status::INVALID_ARGUMENT),
        };

        let image = match image {
            Ok(image) => image,
            Err(message) => {
                crate::guard::set_last_error(message);
                return Err(status::INVALID_IMAGE);
            }
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(KrillaImage { inner: image }))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Builds an image from raw, non-premultiplied RGBA bytes, four per pixel, row-major.
    ///
    /// The buffer length must be exactly `width * height * 4`; krilla panics otherwise, so
    /// the length is checked here first.
    fn krilla_image_new_rgba8(
        data_ptr: *const u8,
        data_len: usize,
        width: u32,
        height: u32,
        out: *mut *mut KrillaImage,
    ) {
        if width == 0 || height == 0 {
            return Err(status::INVALID_ARGUMENT);
        }

        let expected = (width as usize)
            .checked_mul(height as usize)
            .and_then(|pixels| pixels.checked_mul(4))
            .ok_or(status::INVALID_ARGUMENT)?;

        if data_len != expected {
            return Err(status::INVALID_ARGUMENT);
        }

        // SAFETY: R3 — readable for the duration of the call; copied immediately below.
        let bytes = unsafe { handle::slice(data_ptr, data_len)? };
        let image = Image::from_rgba8(bytes.to_vec(), width, height);

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(KrillaImage { inner: image }))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Writes the image's pixel dimensions.
    fn krilla_image_size(image: *const KrillaImage, out_width: *mut u32, out_height: *mut u32) {
        // SAFETY: R1 — live handle.
        let image = unsafe { handle::as_ref(image)? };
        let (width, height) = image.inner.size();

        // SAFETY: out-parameter contract.
        unsafe {
            handle::write_out(out_width, width)?;
            handle::write_out(out_height, height)?;
        }

        Ok(status::OK)
    }
}

ffi! {
    /// Releases an image.
    fn krilla_image_free(image: *mut KrillaImage) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(image) };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Draws an image into the given size, in surface units.
    ///
    /// The image is scaled to fill `size` exactly; aspect ratio is the caller's concern, and
    /// `krilla_image_size` provides the pixel dimensions needed to preserve it.
    fn krilla_surface_draw_image(doc, token: u64, image: *const KrillaImage, size: KrillaSize) {
        // SAFETY: R1 — live handle.
        let image = unsafe { handle::as_ref(image)? };
        let size = krilla::geom::Size::try_from(size)?;

        doc.surface_mut(token)?.draw_image(image.inner.clone(), size);
        Ok(status::OK)
    }
}
