//! Document outline (bookmarks) and link annotations.

use krilla::action::LinkAction;
use krilla::annotation::{Annotation, LinkAnnotation, Target};
use krilla::destination::{Destination, NamedDestination, XyzDestination};
use krilla::geom::Rect;
use krilla::outline::{Outline, OutlineNode};

use crate::guard::{ffi, ffi_doc};
use crate::handle;
use crate::status;
use crate::types::{KrillaPoint, KrillaRect};

/// An outline node, which owns its children until it is pushed into a parent.
pub struct KrillaOutlineNode {
    inner: Option<OutlineNode>,
    depth: usize,
}

/// The document outline: a list of root nodes.
pub struct KrillaOutline {
    inner: Option<Outline>,
}

/// Builds an XYZ destination, validating the page index.
///
/// krilla's `XyzDestination::new` takes a `usize` page index and is documented to panic if it
/// is out of range at serialization time. The index may legitimately point at a page that does
/// not exist yet — forward references are resolved at finish — so the only check available
/// here is against absurd values.
fn destination(page_index: u32, point: KrillaPoint) -> XyzDestination {
    XyzDestination::new(page_index as usize, point.into())
}

ffi! {
    /// Creates an empty outline.
    fn krilla_outline_new(out: *mut *mut KrillaOutline) {
        let outline = KrillaOutline {
            inner: Some(Outline::new()),
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(outline))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases an outline.
    fn krilla_outline_free(outline: *mut KrillaOutline) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(outline) };
        Ok(status::OK)
    }
}

ffi! {
    /// Creates an outline node targeting a point on a page.
    ///
    /// `page_index` is zero-based and may refer to a page that has not been created yet;
    /// krilla resolves the reference when the document is finished.
    fn krilla_outline_node_new(
        text_ptr: *const u8,
        text_len: usize,
        page_index: u32,
        point: KrillaPoint,
        out: *mut *mut KrillaOutlineNode,
    ) {
        // SAFETY: R4 — borrowed UTF-8 for the duration of the call.
        let text = unsafe { handle::str_arg(text_ptr, text_len)? }.to_owned();

        let node = KrillaOutlineNode {
            inner: Some(OutlineNode::new(text, destination(page_index, point))),
            depth: 1,
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(node))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases an outline node. Safe on a node already consumed by a push.
    fn krilla_outline_node_free(node: *mut KrillaOutlineNode) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(node) };
        Ok(status::OK)
    }
}

ffi! {
    /// Sets whether the node starts expanded in a viewer's bookmark pane.
    fn krilla_outline_node_set_open(node: *mut KrillaOutlineNode, open: bool) {
        // SAFETY: R1 — live handle.
        let node = unsafe { handle::as_mut(node)? };
        let inner = node.inner.take().ok_or(status::CONSUMED)?;
        node.inner = Some(inner.with_open(open));
        Ok(status::OK)
    }
}

ffi! {
    /// Moves `child` into `parent`, consuming the child.
    ///
    /// The child handle must still be released with `krilla_outline_node_free`.
    ///
    /// Nesting is capped at `MAX_OUTLINE_DEPTH`: an outline deep enough to exhaust the stack
    /// would take the process down during serialization, and stack overflow is not catchable
    /// on any platform.
    fn krilla_outline_node_push_child(
        parent: *mut KrillaOutlineNode,
        child: *mut KrillaOutlineNode,
    ) {
        if std::ptr::eq(parent.cast_const(), child.cast_const()) {
            return Err(status::INVALID_ARGUMENT);
        }

        // SAFETY: R1 — live handle, and distinct from `parent` per the check above.
        let child = unsafe { handle::as_mut(child)? };
        let child_depth = child.depth;
        let child_node = child.inner.take().ok_or(status::CONSUMED)?;

        // SAFETY: R1 — live handle, distinct from `child`.
        let parent = unsafe { handle::as_mut(parent)? };
        let mut parent_node = parent.inner.take().ok_or(status::CONSUMED)?;

        let depth = child_depth + 1;

        if depth > status::MAX_OUTLINE_DEPTH {
            // Put both back so a refused call leaves the caller's handles usable.
            child.inner = Some(child_node);
            parent.inner = Some(parent_node);
            return Err(status::DEPTH_LIMIT);
        }

        parent_node.push_child(child_node);
        parent.inner = Some(parent_node);
        parent.depth = parent.depth.max(depth);
        Ok(status::OK)
    }
}

ffi! {
    /// Moves a node into the outline as a top-level entry, consuming the node.
    fn krilla_outline_push(outline: *mut KrillaOutline, node: *mut KrillaOutlineNode) {
        // SAFETY: R1 — live handle.
        let node = unsafe { handle::as_mut(node)? };
        let value = node.inner.take().ok_or(status::CONSUMED)?;

        // SAFETY: R1 — live handle.
        let outline = unsafe { handle::as_mut(outline)? };
        let mut inner = outline.inner.take().ok_or(status::CONSUMED)?;

        inner.push_child(value);
        outline.inner = Some(inner);
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Attaches the outline to the document, consuming it.
    fn krilla_document_set_outline(doc, outline: *mut KrillaOutline) {
        // SAFETY: R1 — live handle.
        let outline = unsafe { handle::as_mut(outline)? };
        let value = outline.inner.take().ok_or(status::CONSUMED)?;

        doc.doc_mut()?.set_outline(value);
        Ok(status::OK)
    }
}

// -- Link annotations ---------------------------------------------------------------------

/// Builds the annotation described by the arguments.
///
/// # Safety
///
/// When `uri_len` is non-zero, `uri_ptr` must describe readable UTF-8 for the call.
unsafe fn build_link(
    rect: KrillaRect,
    uri_ptr: *const u8,
    uri_len: usize,
    page_index: u32,
    point: KrillaPoint,
) -> Result<Annotation, i32> {
    let rect = Rect::try_from(rect)?;

    // SAFETY: caller contract on the URI pair.
    let uri = unsafe { handle::opt_str_arg(uri_ptr, uri_len)? };

    let target = match uri {
        Some(uri) => Target::Action(LinkAction::new(uri.to_owned()).into()),
        None => Target::Destination(Destination::Xyz(destination(page_index, point))),
    };

    Ok(LinkAnnotation::new(rect, target).into())
}

ffi_doc! {
    /// Adds a link annotation to the open page.
    ///
    /// Passing a URI makes it an external link; passing none makes it jump to
    /// `page_index` / `point` within the document.
    ///
    /// The annotation is buffered and applied when the page closes. krilla needs exclusive
    /// access to the page to attach one, and the surface holds that for as long as the page
    /// is open; buffering avoids tearing the surface down and rebuilding it, which would emit
    /// a separate content stream each time.
    fn krilla_page_add_link(
        doc,
        token: u64,
        rect: KrillaRect,
        uri_ptr: *const u8,
        uri_len: usize,
        page_index: u32,
        point: KrillaPoint,
    ) {
        // SAFETY: R4 — forwarded to the caller's contract on the URI pair.
        let annotation = unsafe { build_link(rect, uri_ptr, uri_len, page_index, point)? };

        doc.add_annotation(token, annotation, false)?;
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Adds a link annotation as a tagged element, writing its identifier to `out_identifier`.
    ///
    /// The identifier is a slot index usable in the tag tree straight away, even though the
    /// annotation itself is not applied until the page closes.
    #[allow(clippy::too_many_arguments)]
    fn krilla_page_add_tagged_link(
        doc,
        token: u64,
        rect: KrillaRect,
        uri_ptr: *const u8,
        uri_len: usize,
        page_index: u32,
        point: KrillaPoint,
        out_identifier: *mut usize,
    ) {
        // SAFETY: R4 — forwarded to the caller's contract on the URI pair.
        let annotation = unsafe { build_link(rect, uri_ptr, uri_len, page_index, point)? };

        let slot = doc
            .add_annotation(token, annotation, true)?
            .ok_or(status::INVALID_ARGUMENT)?;

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out_identifier, slot)? };
        Ok(status::OK)
    }
}

ffi_doc! {
    /// Registers a global named destination, which other documents and viewers can target by
    /// name.
    ///
    /// Destinations used by link annotations are registered automatically; this is only for
    /// names that nothing in the document links to.
    fn krilla_document_register_named_destination(
        doc,
        name_ptr: *const u8,
        name_len: usize,
        page_index: u32,
        point: KrillaPoint,
    ) {
        // SAFETY: R4 — borrowed UTF-8 for the duration of the call.
        let name = unsafe { handle::str_arg(name_ptr, name_len)? }.to_owned();
        let named = NamedDestination::new(name, destination(page_index, point));

        // A duplicate name bound to a different destination is rejected by krilla, which
        // signals it by returning None.
        match doc.doc_mut()?.register_named_destination(named) {
            Some(()) => Ok(status::OK),
            None => {
                crate::guard::set_last_error(
                    "a different destination is already registered under this name",
                );
                Err(status::INVALID_ARGUMENT)
            }
        }
    }
}
