//! The exported C functions.
//!
//! Split out from the state machine in [`crate::document`] so that the unsafe core stays
//! small and reviewable on its own. Everything here is ordinary safe code apart from
//! dereferencing caller-supplied pointers, which goes through [`crate::handle`].

pub mod document;
pub mod embed;
pub mod error;
pub mod graphic;
pub mod image;
pub mod metadata;
pub mod outline;
pub mod paint;
pub mod path;
pub mod surface;
pub mod tag;
pub mod text;
