//! Tagged PDF: the logical structure tree that makes a document accessible.
//!
//! # Shape of this API
//!
//! krilla models tags as `TagKind`, an enum of 39 variants each wrapping a `Tag<T>` with
//! statically typed attributes. That is pleasant in Rust and unrepresentable in C, so the
//! surface here is flattened: one opaque tag handle, a `kind` discriminant chosen at
//! construction, and attribute setters that apply to whichever kind is inside.
//!
//! Most attributes live on `TagKind` itself and work on any kind. Four are structural enough
//! that krilla only accepts them through a typed constructor — heading level, list numbering,
//! table-header scope, and the alt text of a figure or formula — so those take their value at
//! construction instead.
//!
//! # How the pieces fit
//!
//! 1. `krilla_surface_start_tagged` / `end_tagged` mark spans of drawn content, each yielding
//!    an identifier.
//! 2. `krilla_page_add_tagged_link` yields identifiers for annotations.
//! 3. Identifiers go into `TagGroup`s, which nest into a `TagTree`.
//! 4. `krilla_document_set_tag_tree` attaches the finished tree.
//!
//! Every identifier must appear exactly once in the tree. krilla reports a duplicate or an
//! orphan when the document is finished.

use std::num::{NonZeroU16, NonZeroU32};

use krilla::tagging::{
    Artifact, ArtifactType, ContentTag, ListNumbering, Node, SpanTag, TableHeaderScope, Tag, TagId,
    TagKind, TagTree, kind,
};

use crate::guard::{ffi, ffi_doc};
use crate::handle;
use crate::status;
use crate::types::KrillaRect;

/// A node in the tree as the shim accumulates it, before krilla's types are built.
///
/// The tree cannot be built directly because identifiers arrive as slot indices: an
/// annotation's real `Identifier` is not known until its page closes, which is typically
/// after the caller has already placed it in the structure. Deferring the whole build to
/// `set_tag_tree` means every slot is resolvable by then.
pub enum PendingNode {
    /// A nested group: its tag, and its own children.
    Group(TagKind, Vec<PendingNode>),
    /// A slot index into the document's identifier table.
    Identifier(usize),
}

/// A structure tag, plus the children accumulated under it.
///
/// Combines krilla's `TagKind` and `TagGroup`: keeping them separate would double the handle
/// count for no gain, since a tag that is not going into the tree has no purpose.
pub struct KrillaTag {
    kind: Option<TagKind>,
    children: Vec<PendingNode>,
}

impl KrillaTag {
    /// Takes the tag and its children as a pending node, consuming the handle's contents.
    fn take_group(&mut self) -> Result<PendingNode, i32> {
        let kind = self.kind.take().ok_or(status::CONSUMED)?;
        Ok(PendingNode::Group(kind, std::mem::take(&mut self.children)))
    }
}

/// The document structure tree.
pub struct KrillaTagTree {
    lang: Option<String>,
    children: Vec<PendingNode>,
    consumed: bool,
}

/// Discriminants follow krilla's own declaration order, so the managed enum can be read
/// against the Rust one directly.
fn list_numbering(value: i32) -> Result<ListNumbering, i32> {
    match value {
        0 => Ok(ListNumbering::None),
        1 => Ok(ListNumbering::Disc),
        2 => Ok(ListNumbering::Circle),
        3 => Ok(ListNumbering::Square),
        4 => Ok(ListNumbering::Decimal),
        5 => Ok(ListNumbering::LowerRoman),
        6 => Ok(ListNumbering::UpperRoman),
        7 => Ok(ListNumbering::LowerAlpha),
        8 => Ok(ListNumbering::UpperAlpha),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

fn header_scope(value: i32) -> Result<TableHeaderScope, i32> {
    match value {
        0 => Ok(TableHeaderScope::Row),
        1 => Ok(TableHeaderScope::Column),
        2 => Ok(TableHeaderScope::Both),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

/// Maps a discriminant to a `TagKind`.
///
/// The order is fixed ABI and mirrored by the managed `TagKind` enum. The four kinds needing
/// a construction-time value are excluded and have their own entry points.
fn tag_kind(value: i32) -> Result<TagKind, i32> {
    let kind = match value {
        0 => Tag::<kind::Part>::Part.into(),
        1 => Tag::<kind::Article>::Article.into(),
        2 => Tag::<kind::Section>::Section.into(),
        3 => Tag::<kind::Div>::Div.into(),
        4 => Tag::<kind::BlockQuote>::BlockQuote.into(),
        5 => Tag::<kind::Caption>::Caption.into(),
        6 => Tag::<kind::TOC>::TOC.into(),
        7 => Tag::<kind::TOCI>::TOCI.into(),
        8 => Tag::<kind::Index>::Index.into(),
        9 => Tag::<kind::P>::P.into(),
        10 => Tag::<kind::LI>::LI.into(),
        11 => Tag::<kind::Lbl>::Lbl.into(),
        12 => Tag::<kind::LBody>::LBody.into(),
        13 => Tag::<kind::Table>::Table.into(),
        14 => Tag::<kind::TR>::TR.into(),
        15 => Tag::<kind::TD>::TD.into(),
        16 => Tag::<kind::THead>::THead.into(),
        17 => Tag::<kind::TBody>::TBody.into(),
        18 => Tag::<kind::TFoot>::TFoot.into(),
        19 => Tag::<kind::Span>::Span.into(),
        20 => Tag::<kind::InlineQuote>::InlineQuote.into(),
        21 => Tag::<kind::Note>::Note.into(),
        22 => Tag::<kind::Reference>::Reference.into(),
        23 => Tag::<kind::BibEntry>::BibEntry.into(),
        24 => Tag::<kind::Code>::Code.into(),
        25 => Tag::<kind::Link>::Link.into(),
        26 => Tag::<kind::Annot>::Annot.into(),
        27 => Tag::<kind::Form>::Form.into(),
        28 => Tag::<kind::NonStruct>::NonStruct.into(),
        29 => Tag::<kind::Datetime>::Datetime.into(),
        30 => Tag::<kind::Terms>::Terms.into(),
        31 => Tag::<kind::Title>::Title.into(),
        32 => Tag::<kind::Strong>::Strong.into(),
        33 => Tag::<kind::Em>::Em.into(),
        _ => return Err(status::INVALID_ARGUMENT),
    };

    Ok(kind)
}

/// Reads a list of tag ids passed as parallel pointer and length arrays.
///
/// # Safety
///
/// Both arrays must hold `count` readable elements, and each pointer/length pair must
/// describe readable UTF-8, all for the duration of the call.
unsafe fn tag_id_array(
    ptrs: *const *const u8,
    lens: *const usize,
    count: usize,
) -> Result<Vec<TagId>, i32> {
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
        .map(|(ptr, len)| unsafe { handle::str_arg(*ptr, *len).map(|id| TagId::from(id.bytes())) })
        .collect()
}

fn wrap(kind: TagKind, out: *mut *mut KrillaTag) -> Result<i32, i32> {
    let tag = KrillaTag {
        kind: Some(kind),
        children: Vec::new(),
    };

    // SAFETY: out-parameter contract; `write_out` null-checks.
    unsafe { handle::write_out(out, handle::into_handle(tag))? };
    Ok(status::OK)
}

impl KrillaTag {
    fn kind_mut(&mut self) -> Result<&mut TagKind, i32> {
        self.kind.as_mut().ok_or(status::CONSUMED)
    }
}

ffi! {
    /// Creates a structure tag of the given kind.
    ///
    /// Headings, lists, table headers, figures and formulas carry a value krilla only accepts
    /// at construction; use their dedicated constructors instead.
    fn krilla_tag_new(kind: i32, out: *mut *mut KrillaTag) {
        wrap(tag_kind(kind)?, out)
    }
}

ffi! {
    /// Creates a heading tag at `level`, 1 being the most significant.
    ///
    /// The title is optional in general and required by PDF/UA.
    fn krilla_tag_new_heading(
        level: u16,
        title_ptr: *const u8,
        title_len: usize,
        out: *mut *mut KrillaTag,
    ) {
        let level = NonZeroU16::new(level).ok_or(status::INVALID_ARGUMENT)?;

        // SAFETY: R4 — optional, borrowed for the duration of the call.
        let title = unsafe { handle::opt_str_arg(title_ptr, title_len)? };

        wrap(Tag::Hn(level, title.map(str::to_owned)).into(), out)
    }
}

ffi! {
    /// Creates a list tag with the given numbering style.
    fn krilla_tag_new_list(numbering: i32, out: *mut *mut KrillaTag) {
        wrap(Tag::L(list_numbering(numbering)?).into(), out)
    }
}

ffi! {
    /// Creates a table header cell scoped to its row, column, or both.
    fn krilla_tag_new_table_header(scope: i32, out: *mut *mut KrillaTag) {
        wrap(Tag::TH(header_scope(scope)?).into(), out)
    }
}

ffi! {
    /// Creates a figure tag with alternative text describing the image.
    ///
    /// Alt text is what a screen reader announces, and PDF/UA requires it.
    fn krilla_tag_new_figure(alt_ptr: *const u8, alt_len: usize, out: *mut *mut KrillaTag) {
        // SAFETY: R4 — optional, borrowed for the duration of the call.
        let alt = unsafe { handle::opt_str_arg(alt_ptr, alt_len)? };

        wrap(Tag::Figure(alt.map(str::to_owned)).into(), out)
    }
}

ffi! {
    /// Creates a formula tag with alternative text.
    fn krilla_tag_new_formula(alt_ptr: *const u8, alt_len: usize, out: *mut *mut KrillaTag) {
        // SAFETY: R4 — optional, borrowed for the duration of the call.
        let alt = unsafe { handle::opt_str_arg(alt_ptr, alt_len)? };

        wrap(Tag::Formula(alt.map(str::to_owned)).into(), out)
    }
}

ffi! {
    /// Releases a tag. Safe on a tag already consumed by a push.
    fn krilla_tag_free(tag: *mut KrillaTag) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(tag) };
        Ok(status::OK)
    }
}

/// Generates a setter for an optional-string attribute of `TagKind`.
macro_rules! tag_string_setter {
    ($name:ident, $method:ident, $doc:expr) => {
        ffi! {
            #[doc = $doc]
            fn $name(tag: *mut KrillaTag, ptr: *const u8, len: usize) {
                // SAFETY: R4 — optional, borrowed for the duration of the call.
                let value = unsafe { handle::opt_str_arg(ptr, len)? };

                // SAFETY: R1 — live handle.
                unsafe { handle::as_mut(tag)? }
                    .kind_mut()?
                    .$method(value.map(str::to_owned));
                Ok(status::OK)
            }
        }
    };
}

tag_string_setter!(
    krilla_tag_set_lang,
    set_lang,
    "Sets the natural language of this subtree, as a BCP 47 tag."
);
tag_string_setter!(
    krilla_tag_set_alt_text,
    set_alt_text,
    "Sets alternative text: what a screen reader announces in place of the content."
);
tag_string_setter!(
    krilla_tag_set_actual_text,
    set_actual_text,
    "Sets replacement text: what the content actually says, for content whose glyphs do not \
     spell it — a ligature, or a dropped capital."
);
tag_string_setter!(
    krilla_tag_set_expanded,
    set_expanded,
    "Sets the expansion of an abbreviation or acronym."
);

ffi! {
    /// Sets the tag's identifier, used to reference it from a table cell's `headers` list.
    fn krilla_tag_set_id(tag: *mut KrillaTag, ptr: *const u8, len: usize) {
        // SAFETY: R4 — optional, borrowed for the duration of the call.
        let value = unsafe { handle::opt_str_arg(ptr, len)? };

        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(tag)? }
            .kind_mut()?
            .set_id(value.map(|id| TagId::from(id.bytes())));
        Ok(status::OK)
    }
}

ffi! {
    /// Sets how many rows a table cell spans.
    fn krilla_tag_set_row_span(tag: *mut KrillaTag, span: u32) {
        let span = NonZeroU32::new(span).ok_or(status::INVALID_ARGUMENT)?;

        // SAFETY: R1 — live handle.
        let kind = unsafe { handle::as_mut(tag)? }.kind_mut()?;

        // Only cells carry a span. A non-cell is a caller mistake worth reporting rather than
        // silently ignoring, since a mis-tagged table reads wrongly to a screen reader.
        match kind {
            TagKind::TD(cell) => cell.set_row_span(Some(span)),
            TagKind::TH(cell) => cell.set_row_span(Some(span)),
            _ => return Err(status::INVALID_ARGUMENT),
        }

        Ok(status::OK)
    }
}

ffi! {
    /// Sets how many columns a table cell spans.
    fn krilla_tag_set_col_span(tag: *mut KrillaTag, span: u32) {
        let span = NonZeroU32::new(span).ok_or(status::INVALID_ARGUMENT)?;

        // SAFETY: R1 — live handle.
        let kind = unsafe { handle::as_mut(tag)? }.kind_mut()?;

        match kind {
            TagKind::TD(cell) => cell.set_col_span(Some(span)),
            TagKind::TH(cell) => cell.set_col_span(Some(span)),
            _ => return Err(status::INVALID_ARGUMENT),
        }

        Ok(status::OK)
    }
}

ffi! {
    /// Associates a table cell with the header cells that describe it, by their tag ids.
    fn krilla_tag_set_headers(
        tag: *mut KrillaTag,
        ptrs: *const *const u8,
        lens: *const usize,
        count: usize,
    ) {
        // SAFETY: R3/R4 — forwarded to the caller's contract on the parallel arrays.
        let ids = unsafe { tag_id_array(ptrs, lens, count)? };

        // SAFETY: R1 — live handle.
        let kind = unsafe { handle::as_mut(tag)? }.kind_mut()?;

        match kind {
            TagKind::TD(cell) => cell.set_headers(Some(ids)),
            TagKind::TH(cell) => cell.set_headers(Some(ids)),
            _ => return Err(status::INVALID_ARGUMENT),
        }

        Ok(status::OK)
    }
}

ffi! {
    /// Sets a table's summary, describing its structure in prose.
    fn krilla_tag_set_summary(tag: *mut KrillaTag, ptr: *const u8, len: usize) {
        // SAFETY: R4 — optional, borrowed for the duration of the call.
        let value = unsafe { handle::opt_str_arg(ptr, len)? };

        // SAFETY: R1 — live handle.
        let kind = unsafe { handle::as_mut(tag)? }.kind_mut()?;

        match kind {
            TagKind::Table(table) => table.set_summary(value.map(str::to_owned)),
            _ => return Err(status::INVALID_ARGUMENT),
        }

        Ok(status::OK)
    }
}

ffi! {
    /// Adds a content or annotation identifier as a child of the tag.
    ///
    /// The identifier comes from `krilla_surface_start_tagged` or
    /// `krilla_page_add_tagged_link`, and must be placed in the tree exactly once.
    fn krilla_tag_push_identifier(tag: *mut KrillaTag, identifier: usize) {
        // SAFETY: R1 — live handle.
        unsafe { handle::as_mut(tag)? }
            .children
            .push(PendingNode::Identifier(identifier));
        Ok(status::OK)
    }
}

ffi! {
    /// Moves `child` into `parent`, consuming the child.
    fn krilla_tag_push_child(parent: *mut KrillaTag, child: *mut KrillaTag) {
        if std::ptr::eq(parent.cast_const(), child.cast_const()) {
            return Err(status::INVALID_ARGUMENT);
        }

        // SAFETY: R1 — live handle, distinct from `parent` per the check above.
        let child = unsafe { handle::as_mut(child)? };
        let node = child.take_group()?;

        // SAFETY: R1 — live handle, distinct from `child`.
        unsafe { handle::as_mut(parent)? }.children.push(node);
        Ok(status::OK)
    }
}

ffi! {
    /// Creates an empty tag tree.
    fn krilla_tag_tree_new(out: *mut *mut KrillaTagTree) {
        let tree = KrillaTagTree {
            lang: None,
            children: Vec::new(),
            consumed: false,
        };

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, handle::into_handle(tree))? };
        Ok(status::OK)
    }
}

ffi! {
    /// Releases a tag tree.
    fn krilla_tag_tree_free(tree: *mut KrillaTagTree) {
        // SAFETY: R1 — the caller surrenders the handle.
        unsafe { handle::drop_handle(tree) };
        Ok(status::OK)
    }
}

ffi! {
    /// Sets the document's default natural language, as a BCP 47 tag. Required by PDF/UA.
    fn krilla_tag_tree_set_lang(tree: *mut KrillaTagTree, ptr: *const u8, len: usize) {
        // SAFETY: R4 — optional, borrowed for the duration of the call.
        let value = unsafe { handle::opt_str_arg(ptr, len)? };

        // SAFETY: R1 — live handle.
        let tree = unsafe { handle::as_mut(tree)? };

        if tree.consumed {
            return Err(status::CONSUMED);
        }

        tree.lang = value.map(str::to_owned);
        Ok(status::OK)
    }
}

ffi! {
    /// Moves a tag into the tree as a top-level entry, consuming the tag.
    fn krilla_tag_tree_push(tree: *mut KrillaTagTree, tag: *mut KrillaTag) {
        // SAFETY: R1 — live handle.
        let node = unsafe { handle::as_mut(tag)? }.take_group()?;

        // SAFETY: R1 — live handle.
        let tree = unsafe { handle::as_mut(tree)? };

        if tree.consumed {
            return Err(status::CONSUMED);
        }

        tree.children.push(node);
        Ok(status::OK)
    }
}

/// Builds a krilla node, resolving identifier slots against the document.
fn build(node: PendingNode, doc: &crate::document::KrillaDocument) -> Result<Node, i32> {
    match node {
        PendingNode::Identifier(slot) => Ok(Node::Leaf(doc.identifier(slot)?)),
        PendingNode::Group(kind, children) => {
            let children = children
                .into_iter()
                .map(|child| build(child, doc))
                .collect::<Result<Vec<_>, _>>()?;

            Ok(Node::Group(krilla::tagging::TagGroup::with_children(
                kind, children,
            )))
        }
    }
}

ffi_doc! {
    /// Attaches the tag tree to the document, consuming it.
    ///
    /// This is where the tree is actually built. Identifiers were handed out as slot indices,
    /// because an annotation's real identifier is not known until its page closes — usually
    /// after the caller has already placed it in the structure. Deferring the build to here
    /// means every slot has resolved, and an identifier that was never issued is reported
    /// rather than producing a structurally broken document.
    fn krilla_document_set_tag_tree(doc, tree: *mut KrillaTagTree) {
        // SAFETY: R1 — live handle.
        let tree = unsafe { handle::as_mut(tree)? };

        if tree.consumed {
            return Err(status::CONSUMED);
        }

        let lang = tree.lang.clone();
        let children = std::mem::take(&mut tree.children);
        tree.consumed = true;

        let nodes = children
            .into_iter()
            .map(|child| build(child, doc))
            .collect::<Result<Vec<_>, _>>()?;

        let mut built = TagTree::from(nodes);
        built = built.with_lang(lang);

        doc.doc_mut()?.set_tag_tree(built);
        Ok(status::OK)
    }
}

// -- Marking content on a surface ---------------------------------------------------------

ffi_doc! {
    /// Marks the start of a tagged span of content, writing its identifier to `out`.
    ///
    /// `kind` is 0 for a text span, 1 for other content (paths, images, mixed), or 2 for an
    /// artifact. Artifacts — running heads, page numbers, decorative rules — are excluded
    /// from the logical tree entirely and yield a placeholder identifier that must not be
    /// pushed into it.
    #[allow(clippy::too_many_arguments)]
    fn krilla_surface_start_tagged(
        doc,
        token: u64,
        kind: i32,
        artifact_type: i32,
        bbox: KrillaRect,
        has_bbox: bool,
        lang_ptr: *const u8,
        lang_len: usize,
        alt_ptr: *const u8,
        alt_len: usize,
        expanded_ptr: *const u8,
        expanded_len: usize,
        actual_ptr: *const u8,
        actual_len: usize,
        out: *mut usize,
    ) {
        // SAFETY: R4 — all optional, borrowed for the duration of the call.
        let (lang, alt, expanded, actual) = unsafe {
            (
                handle::opt_str_arg(lang_ptr, lang_len)?,
                handle::opt_str_arg(alt_ptr, alt_len)?,
                handle::opt_str_arg(expanded_ptr, expanded_len)?,
                handle::opt_str_arg(actual_ptr, actual_len)?,
            )
        };

        let bbox = if has_bbox {
            Some(krilla::geom::Rect::try_from(bbox)?)
        } else {
            None
        };

        let artifact = artifact_kind(artifact_type)?;

        // krilla panics when a background artifact has no bounding box in PDF 1.7. Rejecting
        // it here keeps it an argument error instead of a poisoned document.
        if kind == 2 && matches!(artifact, ArtifactType::Page) && bbox.is_none() {
            crate::guard::set_last_error("a page (background) artifact requires a bounding box");
            return Err(status::INVALID_ARGUMENT);
        }

        let tag = match kind {
            0 => ContentTag::Span(
                SpanTag::empty()
                    .with_lang(lang)
                    .with_alt_text(alt)
                    .with_expanded(expanded)
                    .with_actual_text(actual),
            ),
            1 => ContentTag::Other,
            2 => ContentTag::Artifact(Artifact::new(artifact, bbox)),
            _ => return Err(status::INVALID_ARGUMENT),
        };

        doc.open_tag(token)?;
        let identifier = doc.surface_mut(token)?.start_tagged(tag);
        let slot = doc.push_identifier(identifier);

        // SAFETY: out-parameter contract.
        unsafe { handle::write_out(out, slot)? };
        Ok(status::OK)
    }
}

fn artifact_kind(value: i32) -> Result<ArtifactType, i32> {
    match value {
        0 => Ok(ArtifactType::Header),
        1 => Ok(ArtifactType::Footer),
        2 => Ok(ArtifactType::Page),
        3 => Ok(ArtifactType::Other),
        _ => Err(status::INVALID_ARGUMENT),
    }
}

ffi_doc! {
    /// Ends the current tagged span.
    fn krilla_surface_end_tagged(doc, token: u64) {
        doc.close_tag(token)?;
        doc.surface_mut(token)?.end_tagged();
        Ok(status::OK)
    }
}
