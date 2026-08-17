//! The ABI boundary: panic containment, poisoning, and the macros every export goes through.
//!
//! # Why `catch_unwind` rather than `panic = "abort"`
//!
//! A panic reaching an `extern "C"` frame aborts the process. That is not undefined behaviour
//! — `extern "C"` gained an implicit abort-on-unwind shim in Rust 1.71 — but a dead host is
//! just as unacceptable when the consumer is a long-running web server rendering documents.
//!
//! And krilla does panic on inputs a .NET caller can produce:
//!
//! - `Surface::pop` unwraps an empty stack.
//! - `Surface`'s `Drop` asserts that the push stack is empty, that no sub-builders remain,
//!   and that no marked-content section is still open. A panic *in drop* during unwinding
//!   aborts regardless of `catch_unwind`, so the shim has to keep these balanced itself
//!   rather than rely on catching the failure.
//! - `Surface::start_tagged` panics when a background artifact has no bounding box.
//! - `XyzDestination::new` panics on an out-of-range page index.
//! - `Image::from_rgba8` panics when the buffer length does not match `width * height * 4`.
//!
//! The shim intercepts each of those before krilla sees them. `catch_unwind` is the backstop
//! for the ones not yet enumerated, in a crate large enough that "we found five" is not
//! "there are five".

use std::any::Any;
use std::cell::RefCell;
use std::panic::{AssertUnwindSafe, catch_unwind};
use std::sync::Once;

use crate::status;

thread_local! {
    static LAST_ERROR: RefCell<Option<String>> = const { RefCell::new(None) };
    static LAST_PANIC: RefCell<Option<String>> = const { RefCell::new(None) };
}

static HOOK: Once = Once::new();

/// Replaces the default panic hook, for two reasons: the payload alone does not carry the
/// source location, and a library embedded in a hosted .NET application must never write to
/// stderr.
fn install_hook() {
    HOOK.call_once(|| {
        std::panic::set_hook(Box::new(|info| {
            let location = info
                .location()
                .map(|l| format!("{}:{}:{}", l.file(), l.line(), l.column()))
                .unwrap_or_else(|| "<unknown>".to_owned());
            let message = payload_message(info.payload());
            LAST_PANIC.with(|slot| {
                *slot.borrow_mut() = Some(format!("panic at {location}: {message}"));
            });
        }));
    });
}

fn payload_message(payload: &(dyn Any + Send)) -> String {
    if let Some(text) = payload.downcast_ref::<&'static str>() {
        return (*text).to_owned();
    }

    if let Some(text) = payload.downcast_ref::<String>() {
        return text.clone();
    }

    "non-string panic payload".to_owned()
}

/// Records a message retrievable through `krilla_last_error_message`.
pub fn set_last_error(message: impl Into<String>) {
    LAST_ERROR.with(|slot| *slot.borrow_mut() = Some(message.into()));
}

/// Takes the recorded message, clearing it.
pub fn take_last_error() -> Option<String> {
    LAST_ERROR.with(|slot| slot.borrow_mut().take())
}

/// Discards any recorded message.
///
/// Called at the start of every guarded export. Without it a message set by an earlier failed
/// call survives, and the next unrelated failure reports it as its own detail — which is
/// worse than no detail at all, because it points at the wrong problem.
pub fn clear_last_error() {
    LAST_ERROR.with(|slot| *slot.borrow_mut() = None);
}

/// Reads the recorded message without clearing it.
pub fn peek_last_error() -> Option<String> {
    LAST_ERROR.with(|slot| slot.borrow().clone())
}

fn panic_message(payload: Box<dyn Any + Send>) -> String {
    LAST_PANIC
        .with(|slot| slot.borrow_mut().take())
        .unwrap_or_else(|| payload_message(&*payload))
}

/// Runs `body` with panics contained. Used by exports that own no document state.
pub fn guard<F>(body: F) -> i32
where
    F: FnOnce() -> i32,
{
    install_hook();
    clear_last_error();

    // AssertUnwindSafe is needed because several krilla types are not UnwindSafe. It is
    // legitimate here only because nothing observable outlives this call: exports routed
    // through this function either touch no shared state at all, or fail before mutating it.
    // Document-scoped work goes through `guard_doc` instead, which poisons on the panic path.
    match catch_unwind(AssertUnwindSafe(body)) {
        Ok(code) => code,
        Err(payload) => {
            set_last_error(panic_message(payload));
            status::PANIC
        }
    }
}

/// Runs `body` against a document, poisoning it if the body panics.
///
/// Poisoning is what makes the `AssertUnwindSafe` above honest: a panic partway through a
/// drawing operation can leave a half-written content stream, and marking the document dead
/// means no caller can ever observe it. This mirrors how `std::sync::Mutex` handles the same
/// problem.
pub fn guard_doc<F>(doc: *mut crate::document::KrillaDocument, body: F) -> i32
where
    F: FnOnce(&mut crate::document::KrillaDocument) -> i32,
{
    install_hook();
    clear_last_error();

    if doc.is_null() {
        return status::NULL_ARGUMENT;
    }

    // SAFETY: ownership rule R1 — `doc` was produced by `krilla_document_new` and has not
    // been passed to `krilla_document_free`. Enforced by the caller; documented in
    // `docs/ffi-abi.md`.
    let doc = unsafe { &mut *doc };

    if doc.poisoned {
        return status::POISONED;
    }

    match catch_unwind(AssertUnwindSafe(|| body(doc))) {
        Ok(code) => code,
        Err(payload) => {
            doc.poisoned = true;
            set_last_error(panic_message(payload));
            status::PANIC
        }
    }
}

/// Defines an export with no document parameter.
///
/// The body returns `Result<i32, i32>` so argument validation can use `?`; both arms collapse
/// to the same `i32` at the boundary.
macro_rules! ffi {
    (
        $(#[$meta:meta])*
        fn $name:ident($($arg:ident : $ty:ty),* $(,)?) $body:block
    ) => {
        $(#[$meta])*
        #[unsafe(no_mangle)]
        pub extern "C" fn $name($($arg: $ty),*) -> i32 {
            $crate::guard::guard(move || {
                // `mut` is required by bodies that take `&mut` on a captured handle, and
                // harmless for the ones that do not.
                #[allow(unused_mut)]
                let mut run = move || -> ::core::result::Result<i32, i32> { $body };
                match run() {
                    Ok(code) => code,
                    Err(code) => code,
                }
            })
        }
    };
}

/// Defines an export whose first parameter is the document.
macro_rules! ffi_doc {
    (
        $(#[$meta:meta])*
        fn $name:ident($doc:ident $(, $arg:ident : $ty:ty)* $(,)?) $body:block
    ) => {
        $(#[$meta])*
        #[unsafe(no_mangle)]
        pub extern "C" fn $name(
            $doc: *mut $crate::document::KrillaDocument
            $(, $arg: $ty)*
        ) -> i32 {
            $crate::guard::guard_doc($doc, move |$doc| {
                // `mut` is required by bodies that take `&mut` on a captured handle, and
                // harmless for the ones that do not.
                #[allow(unused_mut)]
                let mut run = move || -> ::core::result::Result<i32, i32> { $body };
                match run() {
                    Ok(code) => code,
                    Err(code) => code,
                }
            })
        }
    };
}

pub(crate) use {ffi, ffi_doc};
