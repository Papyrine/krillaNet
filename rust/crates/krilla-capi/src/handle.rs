//! Pointer and buffer helpers, and the ownership rules the whole ABI rests on.
//!
//! # Ownership rules
//!
//! These are referenced by number from `// SAFETY:` comments throughout the crate, and are
//! restated for consumers in `docs/ffi-abi.md`.
//!
//! - **R1** — Every `*mut T` handle this crate hands out is owned by the caller until passed
//!   to the matching `krilla_*_free`. Using a handle after freeing it is undefined.
//! - **R2** — Every allocation crossing the ABI is released by the side that made it. The
//!   Windows builds link the CRT statically (`+crt-static`), so the native library has its
//!   own heap and a .NET-side `free` on a Rust allocation would corrupt it. This is a
//!   correctness requirement, not a stylistic one.
//! - **R3** — Byte slices passed *in* (font data, image data, file contents) are borrowed for
//!   the duration of the call only. The shim copies anything it needs to retain, so callers
//!   never have to keep a pin alive across calls.
//! - **R4** — Strings passed *in* are UTF-8 `ptr` + `len`, not NUL-terminated, borrowed for
//!   the call. Invalid UTF-8 is rejected with `INVALID_UTF8` rather than replaced.
//! - **R5** — Strings and buffers passed *out* are owned by the caller and released with
//!   `krilla_string_free` / `krilla_buffer_free`.
//! - **R6** — Handles are not thread safe and carry no internal locking. A document and
//!   everything reachable from it must be used from one thread at a time.

use crate::status;

/// Reinterprets an opaque handle as a shared reference.
///
/// # Safety
///
/// `ptr` must satisfy R1 for the corresponding type.
pub unsafe fn as_ref<'a, T>(ptr: *const T) -> Result<&'a T, i32> {
    if ptr.is_null() {
        return Err(status::NULL_ARGUMENT);
    }

    // SAFETY: R1 — non-null, and the caller guarantees the handle is live.
    Ok(unsafe { &*ptr })
}

/// Reinterprets an opaque handle as a mutable reference.
///
/// # Safety
///
/// `ptr` must satisfy R1, and must not be aliased for the lifetime of the returned reference.
pub unsafe fn as_mut<'a, T>(ptr: *mut T) -> Result<&'a mut T, i32> {
    if ptr.is_null() {
        return Err(status::NULL_ARGUMENT);
    }

    // SAFETY: R1 — non-null and live. The C ABI is single-threaded per document (R6), so no
    // other reference to this handle can exist while the call is in progress.
    Ok(unsafe { &mut *ptr })
}

/// Writes a value to an out-parameter.
///
/// # Safety
///
/// `out` must be non-null, writable, and correctly aligned.
pub unsafe fn write_out<T>(out: *mut T, value: T) -> Result<(), i32> {
    if out.is_null() {
        return Err(status::NULL_ARGUMENT);
    }

    // SAFETY: caller contract.
    unsafe { out.write(value) };
    Ok(())
}

/// Boxes a value and yields an owning raw pointer, per R1.
pub fn into_handle<T>(value: T) -> *mut T {
    Box::into_raw(Box::new(value))
}

/// Reclaims a handle produced by [`into_handle`].
///
/// # Safety
///
/// `ptr` must satisfy R1 and must not be used again afterwards.
pub unsafe fn drop_handle<T>(ptr: *mut T) {
    if ptr.is_null() {
        return;
    }

    // SAFETY: R1 — the pointer came from `into_handle` and is being surrendered.
    drop(unsafe { Box::from_raw(ptr) });
}

/// Borrows an input byte slice, per R3.
///
/// An empty slice is represented by `len == 0`, in which case `ptr` may be null.
///
/// # Safety
///
/// `ptr` must point to at least `len` readable bytes for the duration of the call.
pub unsafe fn slice<'a>(ptr: *const u8, len: usize) -> Result<&'a [u8], i32> {
    if len == 0 {
        return Ok(&[]);
    }

    if ptr.is_null() {
        return Err(status::NULL_ARGUMENT);
    }

    // SAFETY: R3 — caller guarantees `len` readable bytes for the call.
    Ok(unsafe { std::slice::from_raw_parts(ptr, len) })
}

/// Borrows an input UTF-8 string, per R4.
///
/// # Safety
///
/// Same requirements as [`slice`].
pub unsafe fn str_arg<'a>(ptr: *const u8, len: usize) -> Result<&'a str, i32> {
    // SAFETY: forwarded to the caller's contract on `ptr`/`len`.
    let bytes = unsafe { slice(ptr, len)? };
    std::str::from_utf8(bytes).map_err(|_| status::INVALID_UTF8)
}

/// Borrows an optional input string, treating a zero length as absent.
///
/// Absence is signalled by the length alone, deliberately: an empty managed span marshals to
/// a *non-null* pointer into a zero-length buffer, so keying off the pointer would classify
/// every unset optional argument as a present-but-empty string. krilla rejects empty values
/// wherever this is used — an empty mime type, language tag or alt text is meaningless — so
/// collapsing the two cases costs nothing and removes a whole class of marshalling bug.
///
/// # Safety
///
/// Same requirements as [`slice`].
pub unsafe fn opt_str_arg<'a>(ptr: *const u8, len: usize) -> Result<Option<&'a str>, i32> {
    if len == 0 {
        return Ok(None);
    }

    // SAFETY: forwarded to the caller's contract on `ptr`/`len`.
    unsafe { str_arg(ptr, len).map(Some) }
}

/// Transfers an owned byte buffer to the caller, per R5.
///
/// # Safety
///
/// `out_ptr` and `out_len` must be non-null and writable.
pub unsafe fn buffer_out(
    bytes: Vec<u8>,
    out_ptr: *mut *mut u8,
    out_len: *mut usize,
) -> Result<(), i32> {
    if out_ptr.is_null() || out_len.is_null() {
        return Err(status::NULL_ARGUMENT);
    }

    let boxed = bytes.into_boxed_slice();
    let len = boxed.len();

    // `Box::into_raw` rather than `as_mut_ptr()` + `mem::forget`. The latter looks equivalent
    // and is not: taking `as_mut_ptr()` on a live `Box` performs a `Unique` retag that
    // invalidates the pointer for any later read, so the caller's first access is undefined.
    // Miri catches this; nothing else does. `into_raw` surrenders the allocation and yields a
    // pointer with provenance over all of it.
    let ptr = Box::into_raw(boxed).cast::<u8>();

    // SAFETY: both pointers checked non-null immediately above.
    unsafe {
        out_ptr.write(ptr);
        out_len.write(len);
    }

    Ok(())
}

/// Transfers an owned string to the caller as UTF-8 bytes, per R5.
///
/// # Safety
///
/// Same requirements as [`buffer_out`].
pub unsafe fn string_out(
    text: String,
    out_ptr: *mut *mut u8,
    out_len: *mut usize,
) -> Result<(), i32> {
    // SAFETY: forwarded to the caller's contract on the out-parameters.
    unsafe { buffer_out(text.into_bytes(), out_ptr, out_len) }
}

/// Transfers an owned glyph run to the caller, per R5.
///
/// The typed counterpart to [`buffer_out`], and separate from it on purpose: a glyph is 40
/// bytes with 8-byte alignment, so freeing one through the `u8` path would hand the allocator
/// the wrong layout.
///
/// # Safety
///
/// `out_ptr` and `out_len` must be non-null and writable.
pub unsafe fn glyphs_out(
    glyphs: Vec<crate::types::KrillaGlyph>,
    out_ptr: *mut *mut crate::types::KrillaGlyph,
    out_len: *mut usize,
) -> Result<(), i32> {
    if out_ptr.is_null() || out_len.is_null() {
        return Err(status::NULL_ARGUMENT);
    }

    let boxed = glyphs.into_boxed_slice();
    let len = boxed.len();

    // `Box::into_raw`, for the same reason as `buffer_out`: `as_mut_ptr()` on a live `Box`
    // retags and invalidates the pointer for the caller's first read.
    let ptr = Box::into_raw(boxed).cast::<crate::types::KrillaGlyph>();

    // SAFETY: both pointers checked non-null immediately above.
    unsafe {
        out_ptr.write(ptr);
        out_len.write(len);
    }

    Ok(())
}

/// Releases a glyph run produced by [`glyphs_out`].
///
/// # Safety
///
/// `ptr` and `len` must be exactly what a successful call wrote, and must not have been freed
/// already.
pub unsafe fn free_glyphs(ptr: *mut crate::types::KrillaGlyph, len: usize) {
    if ptr.is_null() || len == 0 {
        return;
    }

    // SAFETY: R5 — the allocation came from `glyphs_out`'s `Box::into_raw`, with the same
    // length and element type. `ptr::slice_from_raw_parts_mut` rather than the reference form,
    // so the allocation is not retagged on its way to being freed.
    unsafe {
        drop(Box::from_raw(std::ptr::slice_from_raw_parts_mut(ptr, len)));
    }
}

/// Releases a buffer produced by any export that writes `ptr` + `len` out-parameters.
///
/// Covers both `krilla_buffer_free` and `krilla_string_free`; they are distinct exports only
/// so the contract reads clearly from the managed side.
///
/// # Safety
///
/// `ptr` and `len` must be exactly what a successful call wrote, and must not have been freed
/// already.
pub unsafe fn free_buffer(ptr: *mut u8, len: usize) {
    if ptr.is_null() || len == 0 {
        return;
    }

    // SAFETY: R5 — the allocation came from `buffer_out`'s `Box::into_raw`, with the same
    // length. `ptr::slice_from_raw_parts_mut` rather than `slice::from_raw_parts_mut` because
    // the latter would materialise a `&mut [u8]` reference, retagging the allocation on the
    // way to freeing it.
    unsafe {
        drop(Box::from_raw(std::ptr::slice_from_raw_parts_mut(ptr, len)));
    }
}
