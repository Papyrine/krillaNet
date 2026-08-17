//! Document, page and surface lifecycle.
//!
//! This is the only module in the crate that erases lifetimes, and the only one whose
//! correctness rests on an argument rather than on the type system. Everything else is
//! ordinary safe code over the handles this module hands out.
//!
//! # The problem
//!
//! krilla models PDF construction as a chain of nested mutable borrows:
//!
//! ```text
//! Document ──&mut──▶ Page<'a> ──&mut──▶ Surface<'a>
//! ```
//!
//! Only one page may be open at a time, and only one surface per page. A C ABI has no way to
//! express any of that, so the shim owns the whole chain behind a single handle and enforces
//! the ordering itself.
//!
//! # The invariants
//!
//! - **I1** — `doc` is boxed, so the `Document` has a stable address. It is never moved, and
//!   never touched at all while a page is open. Every document-level operation checks
//!   `open.is_none()` first.
//! - **I2** — `Page` and `Surface` are individually boxed and reached only through raw
//!   pointers. They are deliberately *not* held as live `Box` values: a `Box` asserts unique
//!   ownership of its allocation under Stacked Borrows, which would conflict with the erased
//!   borrow the page still conceptually holds on the document.
//! - **I3** — Drop order is always surface, then page, then document. `close_page` and the
//!   `Drop` impl are the only places that destroy them, and both follow that order.
//! - **I4** — Every page carries a monotonic token. Drawing calls quote the token they were
//!   issued, so a call against a closed page is rejected with `STALE_PAGE` rather than
//!   dereferencing a dangling pointer.
//!
//! Together these mean the erased `'static` lifetimes are never observable: a `Page<'static>`
//! only ever exists while the `Document` it borrows is alive, boxed, and untouched.
//!
//! # Balancing before drop
//!
//! `Surface`'s `Drop` impl asserts that the push stack is empty, that no sub-builders remain,
//! and that no marked-content section is open. A panic inside `drop` during unwinding aborts
//! the process no matter what `catch_unwind` is in scope, so the shim tracks push depth and
//! tagged state itself and rebalances before dropping. A caller who forgets a `pop` gets a
//! status code from `close_page`, not a dead process.

use krilla::Document;
use krilla::annotation::Annotation;
use krilla::page::{Page, PageSettings};
use krilla::surface::Surface;
use krilla::tagging::Identifier;

use crate::status;

/// An identifier slot, handed to callers as an index and resolved into a real krilla
/// [`Identifier`] when the tag tree is built.
///
/// Content identifiers resolve immediately. Annotation identifiers cannot: adding an
/// annotation needs `&mut Page`, which is unavailable while the surface borrows it, so the
/// annotation is buffered and its identifier is filled in at page close.
pub enum IdentifierSlot {
    /// A real krilla identifier, ready to be placed in the tag tree.
    Resolved(Identifier),
    /// Reserved for an annotation that has not been applied to its page yet.
    PendingAnnotation,
}

/// A buffered annotation, applied to the page once the surface has been dropped.
struct PendingAnnotation {
    annotation: Annotation,
    /// Slot to receive the identifier, for annotations added as tagged.
    slot: Option<usize>,
}

/// The currently open page.
struct OpenPage {
    /// Dropped first. See I3.
    surface: *mut Surface<'static>,
    /// Dropped second. See I3.
    page: *mut Page<'static>,
    token: u64,
    push_depth: usize,
    tag_open: bool,
    annotations: Vec<PendingAnnotation>,
}

/// A sub-stream opened on top of the page surface, used to build graphics, masks and
/// patterns.
///
/// Nests the same way pages do: each sub-stream borrows the surface below it, so they form a
/// stack that must be unwound in order. `surface_mut` always resolves to the innermost open
/// surface, which is what keeps the borrow below it untouched (I1 applied one level down).
struct OpenStream {
    /// Dropped first, like a page's surface.
    surface: *mut Surface<'static>,
    /// Dropped second, and yields the finished `Stream`.
    builder: *mut crate::api::graphic::SubStreamBuilder,
    token: u64,
    push_depth: usize,
}

/// The one handle the whole ABI hangs off.
pub struct KrillaDocument {
    /// `None` once finished. Boxed for I1.
    doc: Option<Box<Document>>,
    open: Option<OpenPage>,
    /// Sub-streams, innermost last. Drawing always targets the last entry.
    streams: Vec<OpenStream>,
    next_token: u64,
    /// Identity for cross-document handle checks. krilla cannot detect a `Graphic` used in
    /// the wrong document and would silently emit an invalid PDF.
    pub id: u64,
    /// Set by `guard_doc` when a call panics. Every later call but `free` returns `POISONED`.
    pub poisoned: bool,
    /// Tag identifiers, handed to callers as indices into this vector.
    pub identifiers: Vec<IdentifierSlot>,
}

impl KrillaDocument {
    /// Wraps a fresh krilla document, taking the identity used for handle ownership checks.
    pub fn new(doc: Document, id: u64) -> Self {
        Self {
            doc: Some(Box::new(doc)),
            open: None,
            streams: Vec::new(),
            next_token: 1,
            id,
            poisoned: false,
            identifiers: Vec::new(),
        }
    }

    /// Borrows the document for a document-level operation.
    ///
    /// Fails while a page is open, which is what upholds I1.
    pub fn doc_mut(&mut self) -> Result<&mut Document, i32> {
        if self.open.is_some() {
            return Err(status::PAGE_ALREADY_OPEN);
        }

        match self.doc.as_mut() {
            Some(doc) => Ok(doc),
            None => Err(status::FINISHED),
        }
    }

    /// Borrows the innermost open surface, checking the caller's token against I4.
    ///
    /// While a sub-stream is open it *is* the innermost surface, and the page surface beneath
    /// it is borrowed. Quoting the page's token then is refused rather than served: writing
    /// to a surface that something else holds a live borrow of is exactly the aliasing the
    /// erased lifetimes can no longer prevent on their own.
    pub fn surface_mut(&mut self, token: u64) -> Result<&mut Surface<'static>, i32> {
        if let Some(stream) = self.streams.last_mut() {
            if stream.token != token {
                return Err(status::STALE_PAGE);
            }

            // SAFETY: I2 + I4 — from `Box::into_raw` in `begin_stream`, still live because it
            // is the last entry on the stack, and reachable only through this method.
            return Ok(unsafe { &mut *stream.surface });
        }

        let open = self.open.as_mut().ok_or(status::NO_OPEN_PAGE)?;

        if open.token != token {
            return Err(status::STALE_PAGE);
        }

        // SAFETY: I2 + I4 — the pointer was produced by `Box::into_raw` in `start_page` and
        // the matching token proves `close_page` has not run, so it is still live. No other
        // reference to it exists: the only way to reach a surface is through this method,
        // and R6 forbids concurrent use of one document.
        Ok(unsafe { &mut *open.surface })
    }

    /// Borrows the push-depth counter of whichever surface `token` names.
    fn depth_mut(&mut self, token: u64) -> Result<&mut usize, i32> {
        if let Some(stream) = self.streams.last_mut() {
            if stream.token != token {
                return Err(status::STALE_PAGE);
            }

            return Ok(&mut stream.push_depth);
        }

        let open = self.open.as_mut().ok_or(status::NO_OPEN_PAGE)?;

        if open.token != token {
            return Err(status::STALE_PAGE);
        }

        Ok(&mut open.push_depth)
    }

    /// Opens a sub-stream on the surface `token` names, returning the new token.
    pub fn begin_stream(&mut self, token: u64) -> Result<u64, i32> {
        // Resolves and validates the parent in one step, and leaves it borrowed for exactly
        // as long as `open_sub_stream` needs it.
        let parent = self.surface_mut(token)?;

        // SAFETY: the parent stays live and untouched until this sub-stream is closed —
        // `surface_mut` and `depth_mut` both resolve to the innermost entry, so nothing can
        // reach the parent while it is on the stack.
        let (builder, surface) = unsafe { crate::api::graphic::open_sub_stream(parent) };

        let sub = self.next_token;
        self.next_token += 1;

        self.streams.push(OpenStream {
            surface,
            builder,
            token: sub,
            push_depth: 0,
        });

        Ok(sub)
    }

    /// Closes the innermost sub-stream and yields its content.
    pub fn finish_stream(&mut self, token: u64) -> Result<krilla::stream::Stream, i32> {
        let Some(stream) = self.streams.last() else {
            return Err(status::NO_OPEN_PAGE);
        };

        if stream.token != token {
            return Err(status::STALE_PAGE);
        }

        let mut stream = self.streams.pop().expect("checked immediately above");

        // Same rebalancing as `close_page`, for the same reason: krilla's `Surface::drop`
        // asserts an empty push stack, and a panic in drop aborts.
        let imbalance = std::mem::take(&mut stream.push_depth);

        {
            // SAFETY: I2 — live, and exclusively ours now that it is off the stack.
            let surface = unsafe { &mut *stream.surface };

            for _ in 0..imbalance {
                surface.pop();
            }
        }

        // SAFETY: both pointers came from one `open_sub_stream` call and are surrendered here.
        let finished =
            unsafe { crate::api::graphic::close_sub_stream(stream.builder, stream.surface) };

        if imbalance > 0 {
            return Err(status::POP_UNDERFLOW);
        }

        Ok(finished)
    }

    /// Token of the currently open page, if any.
    pub fn open_token(&self) -> Option<u64> {
        self.open.as_ref().map(|open| open.token)
    }

    /// Whether `finish` has already consumed the document.
    pub fn is_finished(&self) -> bool {
        self.doc.is_none()
    }

    /// Opens a page and its surface, returning the token that identifies them.
    pub fn start_page(&mut self, settings: PageSettings) -> Result<u64, i32> {
        if self.open.is_some() {
            return Err(status::PAGE_ALREADY_OPEN);
        }

        let doc = match self.doc.as_mut() {
            Some(doc) => doc,
            None => return Err(status::FINISHED),
        };

        let page = doc.start_page_with(settings);

        // SAFETY: I1 + I2 + I3. `page` borrows `*doc`, which is boxed and therefore at a
        // stable address, and which nothing will touch until this page is closed —
        // `doc_mut` refuses to hand it out while `open.is_some()`. Erasing the borrow to
        // 'static is sound because the erased value is destroyed in `close_page` or `Drop`
        // before the document it borrows, and is unreachable except through `self.open`.
        let page: Page<'static> = unsafe { std::mem::transmute::<Page<'_>, Page<'static>>(page) };
        let page = Box::into_raw(Box::new(page));

        // SAFETY: I2 — `page` was just created by `Box::into_raw` above and is live. Taking
        // `&mut` is sound because no other reference to it exists yet.
        let surface = unsafe { (*page).surface() };

        // SAFETY: same argument as the page transmute. The surface borrows `*page`, which is
        // separately boxed and destroyed strictly after the surface (I3).
        let surface: Surface<'static> =
            unsafe { std::mem::transmute::<Surface<'_>, Surface<'static>>(surface) };
        let surface = Box::into_raw(Box::new(surface));

        let token = self.next_token;
        self.next_token += 1;

        self.open = Some(OpenPage {
            surface,
            page,
            token,
            push_depth: 0,
            tag_open: false,
            annotations: Vec::new(),
        });

        Ok(token)
    }

    /// Records a `push_*` on the open page, refusing beyond `MAX_PUSH_DEPTH`.
    ///
    /// Stack exhaustion inside krilla is not catchable on any platform, so the ceiling has to
    /// be enforced before the call rather than recovered from after it.
    pub fn push(&mut self, token: u64) -> Result<(), i32> {
        let depth = self.depth_mut(token)?;

        if *depth >= status::MAX_PUSH_DEPTH {
            return Err(status::DEPTH_LIMIT);
        }

        *depth += 1;
        Ok(())
    }

    /// Records a `pop`, refusing to underflow. krilla's own `pop` unwraps an empty stack.
    pub fn pop(&mut self, token: u64) -> Result<(), i32> {
        let depth = self.depth_mut(token)?;

        if *depth == 0 {
            return Err(status::POP_UNDERFLOW);
        }

        *depth -= 1;
        Ok(())
    }

    /// Marks a tagged section open. krilla panics on a nested `start_tagged`.
    pub fn open_tag(&mut self, token: u64) -> Result<(), i32> {
        let open = self.open.as_mut().ok_or(status::NO_OPEN_PAGE)?;

        if open.token != token {
            return Err(status::STALE_PAGE);
        }

        if open.tag_open {
            return Err(status::TAG_ALREADY_OPEN);
        }

        open.tag_open = true;
        Ok(())
    }

    /// Marks a tagged section closed. krilla panics on an unmatched `end_tagged`.
    pub fn close_tag(&mut self, token: u64) -> Result<(), i32> {
        let open = self.open.as_mut().ok_or(status::NO_OPEN_PAGE)?;

        if open.token != token {
            return Err(status::STALE_PAGE);
        }

        if !open.tag_open {
            return Err(status::NO_OPEN_TAG);
        }

        open.tag_open = false;
        Ok(())
    }

    /// Buffers an annotation until the page closes.
    ///
    /// `Page::add_annotation` needs `&mut Page`, which the live surface holds. Buffering is
    /// what lets the surface be created once, at `start_page`, instead of being torn down and
    /// rebuilt around every annotation — which would emit a separate content stream each time.
    ///
    /// When `tagged` is set, a slot is reserved now and filled with the real identifier at
    /// close. The returned index is stable, so callers can build the tag tree immediately.
    pub fn add_annotation(
        &mut self,
        token: u64,
        annotation: Annotation,
        tagged: bool,
    ) -> Result<Option<usize>, i32> {
        let slot = if tagged {
            let slot = self.identifiers.len();
            self.identifiers.push(IdentifierSlot::PendingAnnotation);
            Some(slot)
        } else {
            None
        };

        let open = self.open.as_mut().ok_or(status::NO_OPEN_PAGE)?;

        if open.token != token {
            return Err(status::STALE_PAGE);
        }

        open.annotations
            .push(PendingAnnotation { annotation, slot });
        Ok(slot)
    }

    /// Resolves an identifier slot handed out earlier.
    ///
    /// Fails on an index that was never issued, and on an annotation slot whose page has not
    /// been closed — which means the caller is building the tag tree while the annotation it
    /// references is still pending, and the real identifier does not exist yet.
    pub fn identifier(&self, slot: usize) -> Result<Identifier, i32> {
        match self.identifiers.get(slot) {
            Some(IdentifierSlot::Resolved(identifier)) => Ok(*identifier),
            Some(IdentifierSlot::PendingAnnotation) => {
                crate::guard::set_last_error(
                    "this annotation's page is still open; close it before building the tag tree",
                );
                Err(status::NO_OPEN_PAGE)
            }
            None => Err(status::INVALID_ARGUMENT),
        }
    }

    /// Registers a resolved content identifier, returning its index.
    pub fn push_identifier(&mut self, identifier: Identifier) -> usize {
        let slot = self.identifiers.len();
        self.identifiers.push(IdentifierSlot::Resolved(identifier));
        slot
    }

    /// Closes the open page.
    ///
    /// Rebalances the surface first: any `push` the caller forgot to `pop`, and any tagged
    /// section left open, would otherwise trip an assertion inside `Surface::drop`. The
    /// imbalance is reported back as a status so the caller learns about the bug, but the
    /// page is closed cleanly either way.
    pub fn close_page(&mut self, token: u64) -> Result<(), i32> {
        // Sub-streams borrow the page surface, so any still open must be unwound before the
        // surface underneath them can be dropped. Discarding their content is the right call:
        // an unfinished stream was never turned into a graphic, mask or pattern, so nothing
        // references it.
        let orphaned = self.discard_open_streams();

        let mut open = match self.open.take() {
            Some(open) => open,
            None => return Err(status::NO_OPEN_PAGE),
        };

        if open.token != token {
            // Put it back: the caller quoted a stale token, which must not close the page
            // that happens to be open now.
            self.open = Some(open);
            return Err(status::STALE_PAGE);
        }

        let imbalance = {
            // SAFETY: I2 + I4 — live until this function drops it, and exclusively ours now
            // that `open` has been taken out of `self`.
            let surface = unsafe { &mut *open.surface };

            if open.tag_open {
                surface.end_tagged();
                open.tag_open = false;
            }

            let depth = open.push_depth;
            for _ in 0..depth {
                surface.pop();
            }
            open.push_depth = 0;

            depth
        };

        // I3: surface first.
        // SAFETY: the pointer came from `Box::into_raw` in `start_page`, is live, and is
        // being surrendered. Nothing references it afterwards — `open` is local and about to
        // go out of scope.
        drop(unsafe { Box::from_raw(open.surface) });

        // With the surface gone the page is no longer borrowed, so buffered annotations can
        // finally be applied.
        {
            // SAFETY: as above; the page outlives the surface by construction (I3).
            let page = unsafe { &mut *open.page };

            for pending in open.annotations.drain(..) {
                match pending.slot {
                    Some(slot) => {
                        let identifier = page.add_tagged_annotation(pending.annotation);
                        self.identifiers[slot] = IdentifierSlot::Resolved(identifier);
                    }
                    None => page.add_annotation(pending.annotation),
                }
            }
        }

        // I3: page second. Its `Drop` flushes the content stream into the document.
        // SAFETY: same as the surface — live, owned, and unreachable afterwards.
        drop(unsafe { Box::from_raw(open.page) });

        if orphaned > 0 || imbalance > 0 {
            return Err(status::POP_UNDERFLOW);
        }

        Ok(())
    }

    /// Unwinds every open sub-stream, discarding content, and reports how many there were.
    ///
    /// Innermost first, since each borrows the one below it.
    fn discard_open_streams(&mut self) -> usize {
        let count = self.streams.len();

        while let Some(mut stream) = self.streams.pop() {
            {
                // SAFETY: I2 — live, and exclusively ours now that it is off the stack.
                let surface = unsafe { &mut *stream.surface };

                for _ in 0..std::mem::take(&mut stream.push_depth) {
                    surface.pop();
                }
            }

            // SAFETY: both pointers came from one `open_sub_stream` call, surrendered here.
            let _ =
                unsafe { crate::api::graphic::close_sub_stream(stream.builder, stream.surface) };
        }

        count
    }

    /// Consumes the document and serializes it.
    ///
    /// Refuses while a page is open rather than closing it implicitly: an unclosed page means
    /// the caller lost track of their own state, and silently papering over that would hide
    /// the bug in output that looks almost right.
    pub fn finish(&mut self) -> Result<Result<Vec<u8>, krilla::error::KrillaError>, i32> {
        if self.open.is_some() {
            return Err(status::PAGE_ALREADY_OPEN);
        }

        let doc = match self.doc.take() {
            Some(doc) => doc,
            None => return Err(status::FINISHED),
        };

        Ok((*doc).finish())
    }
}

impl Drop for KrillaDocument {
    /// Releases whatever is still open, in the order I3 requires.
    ///
    /// Freeing a document with a page still open is a caller bug, but it must not leak and
    /// must not abort, so the same rebalancing `close_page` does is repeated here.
    fn drop(&mut self) {
        // Sub-streams first: each borrows the surface below it. Skipped when poisoned, for
        // the same reason the page teardown is.
        if !self.poisoned {
            self.discard_open_streams();
        } else {
            // Deliberately leak: `OpenStream` holds raw pointers and has no destructor, so
            // clearing the vector abandons the boxes behind them. Touching a surface left in
            // an unknown state by a panic risks a second panic inside `drop`, which aborts —
            // and the document is dead either way.
            self.streams.clear();
        }

        if let Some(open) = self.open.take() {
            // A poisoned document reached this state through a panic, so its surface is in an
            // unknown condition. Touching it to rebalance risks a second panic inside `drop`,
            // which would abort. Leaking the two boxes is strictly better than that: the
            // process is about to learn of a bug either way, and the document is dead.
            if self.poisoned {
                std::mem::forget(open);
                return;
            }

            // SAFETY: I2 — live, and exclusively ours now that `open` is taken.
            let surface = unsafe { &mut *open.surface };

            if open.tag_open {
                surface.end_tagged();
            }

            for _ in 0..open.push_depth {
                surface.pop();
            }

            // SAFETY: I3 — surface before page, both from `Box::into_raw` in `start_page`.
            unsafe {
                drop(Box::from_raw(open.surface));
                drop(Box::from_raw(open.page));
            }
        }

        // `doc` drops last, by field order, after anything that borrowed it is gone.
    }
}
