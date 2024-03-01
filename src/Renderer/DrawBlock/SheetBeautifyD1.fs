﻿module SheetBeautifyD1

open Optics
open CommonTypes
open DrawModelType
open DrawModelType.SymbolT
open DrawModelType.BusWireT
open Helpers
open Symbol
open BlockHelpers


module Helpers =
    let visibleSegments (wId: ConnectionId) (model: SheetT.Model): XYPos list =
            let wire = model.Wire.Wires[wId] // get wire from model 
            /// helper to match even and off integers in patterns (active pattern)
            let (|IsEven|IsOdd|) (n: int) = match n % 2 with | 0 -> IsEven | _ -> IsOdd
            /// Convert seg into its XY Vector (from start to end of segment).
            /// index must be the index of seg in its containing wire.
            let getSegmentVector (index:int) (seg: BusWireT.Segment) =
                // The implicit horizontal or vertical direction  of a segment is determined by 
                // its index in the list of wire segments and the wire initial direction
                match index, wire.InitialOrientation with
                | IsEven, BusWireT.Vertical | IsOdd, BusWireT.Horizontal -> {X=0.; Y=seg.Length}
                | IsEven, BusWireT.Horizontal | IsOdd, BusWireT.Vertical -> {X=seg.Length; Y=0.}
    
            /// Return a list of segment vectors with 3 vectors coalesced into one visible equivalent
            /// if this is possible, otherwise return segVecs unchanged.
            /// Index must be in range 1..segVecs
            let rec coalesce (segVecs: XYPos list)  =
                match List.tryFindIndex (fun segVec -> segVec =~ XYPos.zero) segVecs[1..segVecs.Length-2] with          
                | Some zeroVecIndex ->
                    let index = zeroVecIndex + 1 // base index as it should be on full segVecs
                    segVecs[0..index-2] @
                    [segVecs[index-1] + segVecs[index+1]] @
                    segVecs[index+2..segVecs.Length - 1]
                    |> coalesce
                | None -> segVecs
         
            wire.Segments
            |> List.mapi getSegmentVector
            |> coalesce

    /// SYMBOL/CUSTOM COMPONENT HELPER FUNCTIONS

    // Given D1 we are scaling custom components, we must have r/w to the dimensions
    /// get the Height and Width of Custom Cmponent
    let getCustomDimensions (sym: Symbol) : float*float =
        (sym.Component.H, sym.Component.W)
    
    /// update Height and Width of Custom Component
    let updateCustomDimensions (h: float) (w:float) (sym: Symbol) : Symbol =
        let updatedComponent = {sym.Component with H = h; W = w}
        {sym with Component = updatedComponent}

    //This helper may be useful when aligning same-type components
    /// returns all the Symbols in a sheet grouped by Component Type
    let getSameTypeSymbol (sheet: SheetT.Model) =
        let allSymbols = sheet.Wire.Symbol.Symbols |> Map.toList |> List.map snd
        let compGroups = 
            allSymbols
            |> List.groupBy (fun symbol -> symbol.Component.Type)
        compGroups


    // WIRE HELPER FUNCTIONS
    // from Derek Lai's code. These helpers will be used to detect segment crossings
    /// Returns true if two 1D line segments intersect
    let overlap1D ((a1, a2): float * float) ((b1, b2): float * float) : bool =
        let a_min, a_max = min a1 a2, max a1 a2
        let b_min, b_max = min b1 b2, max b1 b2
        a_max >= b_min && b_max >= a_min
    /// Returns true if two Boxes intersect, where each box is passed in as top right and bottom left XYPos tuples
    let overlap2D ((a1, a2): XYPos * XYPos) ((b1, b2): XYPos * XYPos) : bool =
        (overlap1D (a1.X, a2.X) (b1.X, b2.X)) && (overlap1D (a1.Y, a2.Y) (b1.Y, b2.Y))

    /// Returns a list of all the wires in the given model
    let getWireList (model: Model) =
        model.Wires
        |> Map.toList
        |> List.map snd

    
    
    // The XYPos of segments will be a useful helper for tracking segment locations on the sheet 
    /// Convert a wire and its segment displacement into actual segment start and end positions
    let getSegmentPositions (sheet:SheetT.Model) wire =
        let startPos = wire.StartPos
        visibleSegments wire.WId sheet
        |> List.fold (fun (acc, lastPos) seg ->
            let newPos = lastPos + seg
            ((lastPos, newPos) :: acc, newPos) // Prepend to list for efficiency
        ) ([], startPos)
        |> fst
        |> List.rev // Reverse the list to maintain original order
    
    /// Update BusWire model with given wires. Can also be used to add new wires.
    let updateModelWires (model: BusWireT.Model) (wiresToAdd: Wire list) : BusWireT.Model =
        model
        |> Optic.map wires_ (fun wireMap ->
            (wireMap, wiresToAdd)
            ||> List.fold (fun wireMap wireToAdd -> Map.add wireToAdd.WId wireToAdd wireMap))

    

module Beautify =
    let alignSymbols
        (wModel: BusWireT.Model)
        (symbolToSize: Symbol)
        (otherSymbol: Symbol)
        : BusWireT.Model =
    
        match RotateScale.getOppEdgePortInfo (wModel:BusWireT.Model) symbolToSize otherSymbol with
        | None -> wModel
        | Some(movePortInfo, otherPortInfo) ->
            let offset = RotateScale.alignPortsOffset movePortInfo otherPortInfo
            let symbol' = moveSymbol offset symbolToSize
            let model' = Optic.set (symbolOf_ symbolToSize.Id) symbol' wModel
            BusWireSeparate.routeAndSeparateSymbolWires model' symbolToSize.Id
