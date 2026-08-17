//! Buffer, string and last-error accessors.

use crate::guard::{ffi, peek_last_error};
use crate::handle;
use crate::status;

ffi! {
    /// Releases a buffer written by an export's `ptr` + `len` out-parameters.
    ///
    /// Rule R2: the native library allocated it, so the native library frees it. Calling
    /// `Marshal.FreeHGlobal` on one of these corrupts the heap, because the Windows builds
    /// link the CRT statically and therefore do not share the host's allocator.
    fn krilla_buffer_free(ptr: *mut u8, len: usize) {
        // SAFETY: R5 — the caller passes back exactly what a successful call wrote out.
        unsafe { handle::free_buffer(ptr, len) };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases a UTF-8 string written by an export's `ptr` + `len` out-parameters.
    ///
    /// Identical to `krilla_buffer_free`; separate only so the managed side reads clearly.
    fn krilla_string_free(ptr: *mut u8, len: usize) {
        // SAFETY: R5, as above.
        unsafe { handle::free_buffer(ptr, len) };
        Ok(status::OK)
    }
}

ffi! {
    /// Writes the last error message for the calling thread as UTF-8, or an empty buffer if
    /// there is none.
    ///
    /// The message is diagnostic detail for a status code already returned — it is never the
    /// primary error channel, and it is thread-local, so it must be read on the thread that
    /// saw the failure. The caller owns the buffer and frees it with `krilla_string_free`.
    fn krilla_last_error_message(out_ptr: *mut *mut u8, out_len: *mut usize) {
        let message = peek_last_error().unwrap_or_default();

        // SAFETY: out-parameter contract; `string_out` null-checks both pointers.
        unsafe { handle::string_out(message, out_ptr, out_len)? };
        Ok(status::OK)
    }
}
