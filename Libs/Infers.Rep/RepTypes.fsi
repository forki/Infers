﻿// Copyright (C) by Vesa Karvonen

/// `Infers.Rep` is a library providing inference rules for datatype generic
/// programming with the `Infers` library.
///
/// `Infers.Rep` uses reflection and run-time code generation to build type
/// representations for various F# types.  Those type representations can be
/// accessed using `Infers` by writing rules over the structure of types.  The
/// type representations provided by `Infers.Rep` make it possible to manipulate
/// values of the represented types efficiently: after the type representation
/// has been created, no further use of slow reflection, boxing or other kinds
/// of auxiliary memory allocations are required.
namespace Infers.Rep

open Infers

////////////////////////////////////////////////////////////////////////////////

/// Represents an empty product as a special case for union cases.
type Empty = struct end

/// Represents a pair of the types `'e` and `'r`.
#if DOC
///
/// Note that the idea behind using a struct type is to make it possible to
/// construct and deconstruct products without performing any heap allocations.
/// When used carefully, avoiding copying and making sure structs are stack
/// allocated, this can lead to significantly better performance than with heap
/// allocated products.  However, naive use results in both heap allocations and
/// copying, which can lead to worse performance than with heap allocated
/// products.
///
/// Note that while it is in no way enforced, the idea is that in a nested
/// product the `Elem` field is the current singleton element and `Rest` is
/// the remainder of the nested produced.  For example, the nested product
/// of the type
///
///> char * int * float * bool
///
/// would be
///
///> Pair<char, Pair<int, Pair<float, bool>>>
///
/// The `Rep` rules generate products in this manner and it is good to know
/// this so that the processing of the singleton `Elem` field and the remainder
/// product `Rest` can be done in the desired order.
#endif
type [<Struct>] Pair<'e, 'r> =
  /// The current element.
  val mutable Elem: 'e

  /// The remainder of the product.
  val mutable Rest: 'r

  /// Constructs a pair.
  new: 'e * 'r -> Pair<'e, 'r>

[<AutoOpen>]
module Pair =
  /// Active pattern for convenient matching of pair structs.
  val inline (|Pair|): Pair<'e, 'r> -> 'e * 'r

////////////////////////////////////////////////////////////////////////////////

/// Representation of the type `'t` as nested choices of type `'s`.
#if DOC
///
/// A choice object also contains members for accessing individual cases of the
/// choice.  Those members are of the form
///
///> member _: Case<'p, 'o, 't>
///
/// where `'p` is a representation of the case as a product and `'o` is a nested
/// choice that identifies the particular case.
#endif
type [<AbstractClass; InferenceRules>] AsChoices<'s, 't> =
  new: int -> AsChoices<'s, 't>

  /// The number of cases the discriminated union type `'t` has.
  val Arity: int

  /// Returns the integer tag of the given discriminated union value.
  abstract Tag: 't -> int

//  abstract ToSum: 'u -> 'c
//  abstract OfSum: 'c -> 'u

////////////////////////////////////////////////////////////////////////////////

/// Base class for type representations.
type [<InferenceRules>] Rep<'t> =
  new: unit -> Rep<'t>

////////////////////////////////////////////////////////////////////////////////

/// Representation for primitive types.
type [<AbstractClass>] Prim<'t> =
  inherit Rep<'t>
  new: unit -> Prim<'t>

////////////////////////////////////////////////////////////////////////////////

/// Representation for types that are not yet supported.  Pull requests are
/// welcome!
type [<AbstractClass>] Unsupported<'t> =
  inherit Rep<'t>
  new: unit -> Unsupported<'t>

////////////////////////////////////////////////////////////////////////////////

/// Type representation for the F# product type (tuple or record) `'t`.
#if DOC
///
/// A product object also contains a member of the form
///
///> member _: AsProduct<'p, 't, 't>
///
/// where the type `'p` is a representation of the product as a nested record.
/// The member is visible to inference rules, but it cannot be given a signature
/// in F#.
///
/// See also `Union<'t>`.
#endif
type [<AbstractClass>] Product<'t> =
  inherit Rep<'t>
  new: unit -> Product<'t>

/// Representation of an element of type `'e` of the product type `'t`.
type [<AbstractClass>] Elem<'e, 'r, 'o, 't> =
  new: int -> Elem<'e, 'r, 'o, 't>

  /// The index of the element.
  val Index: int

  /// Returns the value of the element.
  abstract Get: 't -> 'e

/// Representation of a possibly labelled element of type `'e`.
type [<AbstractClass>] Labelled<'e, 'r, 'o, 't> =
  inherit Elem<'e, 'r, 'o, 't>
  new: int * string -> Labelled<'e, 'r, 'o, 't>
  
  /// The name of the label.
  val Name: string

////////////////////////////////////////////////////////////////////////////////

/// Type representation for the F# tuple type `'t`.
type [<AbstractClass>] Tuple<'t> =
  inherit Product<'t>
  new: unit -> Tuple<'t>

/// Representation of an element of type `'e` of a tuple of type `'t`.
type [<AbstractClass>] Item<'e, 'r, 't> =
  inherit Elem<'e, 'r, 't, 't>
  new: int -> Item<'e, 'r, 't>

////////////////////////////////////////////////////////////////////////////////

/// Type representation for the F# record type `'t`.
type [<AbstractClass>] Record<'t> =
  inherit Product<'t>
  new: unit -> Record<'t>

/// Representation of a field of type `'e` of the record type `'t`.
type [<AbstractClass>] Field<'e, 'r, 't> =
  inherit Labelled<'e, 'r, 't, 't>
  new: int * string * bool -> Field<'e, 'r, 't>

  /// Whether the field is mutable.
  val IsMutable: bool

  /// Sets the value of the field assuming this is a mutable field.
  abstract Set: 't * 'e -> unit

////////////////////////////////////////////////////////////////////////////////

/// Representation of the type `'t` as nested pairs of type `'p`.
#if DOC
///
/// A product object also contains members for accessing the elements of the
/// product.  Depending on the type `'t` those members are of one of the
/// following forms:
///
///> member _:  Item<'e, 'sp,      't>
///> member _: Label<'l, 'sp, 'sc, 'u>
///> member _: Field<'f, 'sp,      'r>
///
/// Those members are visible to inference rules, but they cannot be given a
/// signature in F#.
#endif
type [<AbstractClass; InferenceRules>] AsPairs<'p, 'o, 't> =
  new: int * bool -> AsPairs<'p, 'o, 't>

  /// The number of elements the product type has.
  val Arity: int

  /// Whether the product type is mutable.
  val IsMutable: bool

  /// Copies the fields of the type `'t` to the generic product of type `'p`.
  abstract Extract: 't * byref<'p> -> unit

  /// Creates a new instance of type `'t` from the nested pairs of type `'p`.
  abstract Create: byref<'p> -> 't

  /// Overwrites the fields of the record type `'t` with values from the nested
  /// pairs of type `'p`.
  abstract Overwrite: Record<'t> * into: 't * from: byref<'p> -> unit

  /// Convenience function to convert from product type to nested pairs.
  abstract ToPairs: 't -> 'p

  /// Convenience function to convert from nested pairs to product type.
  abstract OfPairs: 'p -> 't

  /// Convenience function to create a new default valued (all default values)
  /// object of the record type `'t`.
  abstract Default: Record<'t> -> 't

////////////////////////////////////////////////////////////////////////////////

/// Type representation for the F# discriminated union type `'t`.
#if DOC
///
/// A union object also contains a member of the form
///
///> member _: AsSum<'s, 't>
///
/// where type `'s` is a representation of the union as nested binary choices.
/// The member is visible to inference rules, but it cannot be given a signature
/// in F#.
///
/// Note that while union types are not considered as product types in
/// `Infers.Rep`, one can view a union type with only a single case as a
/// product.  For example,
///
///> type foo = Bar of int * string * float
///
/// can be viewed as a product
///
///> AsProduct<Pair<int, Pair<string, float>>,
///>           Pair<int, Pair<string, float>>,
///>           foo>
///
/// and the `Rep.viewAsProduct` rule provides this directly.  If you need to
/// handle product types and union types separately, say in a pretty printing
/// generic, you should have the `Union<_>` and `Product<_>` predicates in your
/// rules.
#endif
type [<AbstractClass>] Union<'t> =
  inherit Rep<'t>
  new: unit -> Union<'t>

/// Representation of a case of the F# discriminated union type `'t`.
type [<AbstractClass>] Case<'p, 'o, 't> =
  inherit AsPairs<'p, 'o, 't>
  new: string * int * int -> Case<'p, 'o, 't>

  /// The name of the case.
  val Name: string

  /// The integer tag of the case.
  val Tag: int

/// Representation of a possibly labelled element of type `'e` of a case of the
/// F# discriminated union type `'t`.
type [<AbstractClass>] Label<'e, 'r, 'o, 't> =
  inherit Labelled<'e, 'r, 'o, 't>
  new: int * string -> Label<'e, 'r, 'o, 't>
