﻿module SheetBeautifyHelpers

open Fable.Core
open CommonTypes
open DrawHelpers
open Fable.React
open Optics
open Operators
open Node.ChildProcess
open DrawModelType
open Symbol
open Elmish
open EEExtensions
open Optics
open Optics.Operators
open DrawHelpers
open Helpers
open CommonTypes
open ModelType
open DrawModelType
open Sheet.SheetInterface
open CommonTypes
open DrawModelType.BusWireT
open DrawModelType.SymbolT
open BusWire
open BusWireUpdateHelpers
open BusWireRoutingHelpers
open EEExtensions
open Optics
open Optics.Operators
open DrawHelpers
open Helpers
open CommonTypes
open ModelType
open DrawModelType
open Sheet.SheetInterface



//-----------------Module for beautify Helper functions--------------------------//
// Typical candidates: all individual code library functions.
// Other helpers identified by Team


// Key Type Difficulty Value read or written 
// B1R, B1W RW 
//  Value read or written: The dimensions of a custom component symbol


/// Calculates the final dimensions of a symbol, considering potential scaling.
let caseInvariantEqual str1 str2 =
        String.toUpper str1 = String.toUpper str2
let getlabel (model:SheetT.Model) (label:string): SymbolT.Symbol option = 
        model.Wire.Symbol.Symbols
        |> Map.values
        |> Seq.tryFind (fun sym -> caseInvariantEqual label sym.Component.Label)

//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// get and set dimensions of a custom component symbol

/// <summary>
/// lens for getting and setting the scaled dimensions of a custom component symbol
/// </summary>

let getCustomSymDim (symbol: SymbolT.Symbol) =
        let originalWidth = symbol.Component.W
        let originalHeight = symbol.Component.H

        let width = match symbol.HScale with
                        | Some scale -> originalWidth * scale
                        | None -> originalWidth
        let height = match symbol.VScale with
                        | Some scale -> originalHeight * scale
                        | None -> originalHeight
        (width, height)


let setCustomSymDim ((width, height): (float * float)) (symbol: SymbolT.Symbol) =
    let originalWidth = symbol.Component.W
    let originalHeight = symbol.Component.H

    let hScale = width / originalWidth
    let vScale = height / originalHeight
    { symbol with HScale = Some hScale; VScale = Some vScale }


let customSymDim_ = Lens.create getCustomSymDim setCustomSymDim


//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// B2 Change the position of a symbol on the sheet

/// <summary>
/// Change the position of a symbol on the sheet
/// </summary>
/// <param name="symbol">The symbol to be moved.</param>
/// <returns>symbol with the provided position.</returns>

let moveSymbolToPosition (symbol: SymbolT.Symbol) (newPos: XYPos): SymbolT.Symbol=
    { symbol with Pos = newPos }


    


    
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// B3 Read/write the order of ports on a specified side of a symbol

/// <summary>
/// lens for getting and setting port order on a specified side of a symbol
/// </summary>

let getPortOrder (side: Edge) (symbol: SymbolT.Symbol) =
    Map.tryFind side symbol.PortMaps.Order

let setPortOrder (side: Edge) (newPortOrder: string list option) (symbol: SymbolT.Symbol) =
    match newPortOrder with
    | Some (newOrder: string list) ->
        let updatedPortOrder = Map.add side newOrder symbol.PortMaps.Order
        { symbol with PortMaps = { symbol.PortMaps with Order = updatedPortOrder } }
    | None -> symbol

let symPortOrder_ side = Lens.create (getPortOrder side) (setPortOrder side)

//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// B4 The reverses state of the inputs of a MUX2 

/// <summary>
/// lens for getting and setting the reversed state of the inputs of a MUX2
/// </summary>

let getreverseMux2Input (symbol: SymbolT.Symbol): bool = 
        
        let InputPort: option<bool> = symbol.ReversedInputPorts
        
        match InputPort with
        | Some state -> state
        | None -> false
        
let setreverseMux2Input (newState: bool) (symbol: SymbolT.Symbol): SymbolT.Symbol =

    { symbol with ReversedInputPorts = Some newState }

let reverseMux2Input_ = Lens.create getreverseMux2Input setreverseMux2Input        


//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// B5 R - The position of a port on the sheet. It cannot directly be written.

/// <summary>
/// Read the position of a port on the sheet
/// </summary>
/// <param name="symbol">The symbol port belongs to.</param>
/// <param name="port">the port obj to be located</param>
/// <returns>XYPos of the port.</returns>

let getPortPosition (sym: Symbol) (port: Port) : XYPos =
    let TopLeftPos = sym.Pos
    let offset = getPortPos sym port
    { X = TopLeftPos.X + offset.X; Y = TopLeftPos.Y - offset.Y }

//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// B6 R - The Bounding box of a symbol outline (position is contained in this)

/// <summary>
/// Read the Bounding box of a symbol
/// </summary>
/// <param name="symbol">The symbol.</param>
/// <returns>Bounding box of the symbol.</returns>

let getBoundingBox (symbol: Symbol) : BoundingBox =

    // extract height and width from symbol, scale it using HScale and VScale if they exist
    let scaledW, scaledH = 
        (
            (match symbol.HScale with 
                | Some(scale) -> symbol.Component.W * scale 
                | None -> symbol.Component.W),
            
            (match symbol.VScale with 
                | Some(scale) -> symbol.Component.H * scale 
                | None -> symbol.Component.H)
        )

    // Constructing and returning the BoundingBox with scaled dimensions.
    { 
        TopLeft = symbol.Pos; 
        W = scaledW; 
        H = scaledH 
    }

//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// B7 RW  Low The rotation state of a symbol

/// <summary>
/// lens for getting and setting the rotation state of a symbol
/// </summary>


let getSymbolRotation (symbol: SymbolT.Symbol) : Rotation = 
    symbol.STransform.Rotation
let setSymbolRotation (desiredRotation: Rotation) (symbol: SymbolT.Symbol) : SymbolT.Symbol =
    { symbol with STransform = { symbol.STransform with Rotation = desiredRotation}}
    
let SymbolRotation_ = Lens.create getSymbolRotation setSymbolRotation

//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// B8 RW  Low The Flipped state of a symbol 

/// <summary>
/// lens for getting and setting the flipped state of a symbol
/// </summary>

let getSymbolFlipped (symbol: SymbolT.Symbol) : bool = 
    symbol.STransform.Flipped
let setSymbolFlipped (desiredFlipped: bool) (symbol: SymbolT.Symbol) : SymbolT.Symbol =
    { symbol with STransform = { symbol.STransform with Flipped = desiredFlipped}}

let symbolFlipped_ = Lens.create getSymbolFlipped setSymbolFlipped





//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// T1R The number of pairs of symbols that intersect each other. Count over all pairs of symbols.

/// <summary>
/// The number of pairs of symbols that intersect each other
/// </summary>
/// <param name="sheet">The sheet.</param>
/// <returns>a interger, representing number of pairs of symbols that intersect.</returns>

let countIntersectingSymbolPairs (sheet: SheetT.Model) =

    let wireModel = sheet.Wire

    // Get Bounding boxes of all symbols into a list
    let boxes =
        mapValues sheet.BoundingBoxes
        |> Array.toList
        |> List.mapi (fun n box -> n,box)
    
    // Iterate through all symbol pairs and count the number of intersecting pairs
    List.allPairs boxes boxes 
    |> List.filter (fun ((n1,box1),(n2,box2)) -> (n1 <> n2) && BlockHelpers.overlap2DBox box1 box2)
    |> List.length



//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// T2R R Low The number of distinct wire visible segments that intersect with one or more symbols.

/// <summary>
/// The number of distinct wire visible segments that intersect with one or more symbols.
/// </summary>
/// <param name="sheet">The sheet.</param>
/// <returns>a interger, representing number of pairs of symbols that intersect.</returns>

let countSymbolIntersectingWire (sheet: SheetT.Model) =

    let wireModel = sheet.Wire

    wireModel.Wires
    |> Map.fold (
        fun acc _ wire -> 
            if BusWireRoute.findWireSymbolIntersections wireModel wire <> [] 
            then acc + 1 
            else acc
    ) 0


//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// Function to convert a wire into a list of segments, useful for T3-6

type SegVector = {Start: XYPos; Direction: XYPos}

let getEndPoint (seg: SegVector) = 
    { X = seg.Start.X + seg.Direction.X; Y = seg.Start.Y + seg.Direction.Y }

// provided function
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
    let tryCoalesceAboutIndex (segVecs: XYPos list) (index: int) =
        if index > 0 && index < segVecs.Length - 1 && segVecs[index] =~ XYPos.zero then
            let before = segVecs.[0..index-2]
            let coalesced = segVecs.[index-1] + segVecs.[index+1]
            let after = segVecs.[index+2..]
            before @ [coalesced] @ after
        else
            segVecs


    wire.Segments
    |> List.mapi getSegmentVector
    |> (fun segVecs ->
            (segVecs,[1..segVecs.Length-2])
            ||> List.fold tryCoalesceAboutIndex)


/// <summary>
/// convert a wire into a list of segments represented by SegVector
/// </summary>
/// <param name="wId">The Id of the wire needs to be converted.</param>
/// <param name="model">The model containing that wire.</param>
/// <returns>list of SegVector of all the segments of that wire.</returns>

let wireToSegments (wId: ConnectionId) (model: SheetT.Model) =

    let segmentsXYPos = visibleSegments wId model

    let addXYPos (p1: XYPos) (p2: XYPos) = 
        { X = p1.X + p2.X; Y = p1.Y + p2.Y }

    segmentsXYPos
    |> List.fold (fun (acc: SegVector list, lastPos: XYPos) pos -> 
        match acc with
        | [] -> ([{ Start = lastPos; Direction = pos }], addXYPos lastPos pos)
        | _ -> (acc @ [{ Start = lastPos; Direction = pos }], addXYPos lastPos pos)
    ) ([], { X = 0.0; Y = 0.0 })
    |> fst



//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// T3R - Number of visible wire Intersecting at right-angles

/// <summary>Check if a segment is vertical</summary>
let isVertical (direction: XYPos) = direction.X = 0.0

/// <summary>Calculate the range of a segment</summary>
let calculateRange (startPos: XYPos) (endPos: XYPos) (isVertical: bool) =
    if isVertical then
        (min startPos.Y endPos.Y, max startPos.Y endPos.Y)
    else
        (min startPos.X endPos.X, max startPos.X endPos.X)

/// <summary>
/// Check if two segments are crossing at right angle
/// </summary>
/// <param name="seg1">the first SegVector</param>
/// <param name="seg2">the second SegVector</param>
/// <returns>bool value, indicate weather two segment Intersect.</returns>

let isCrossingAtRightAngle (seg1: SegVector) (seg2: SegVector) =
    let end1 = getEndPoint seg1
    let end2 = getEndPoint seg2

    // if two segments are parallel, they cannot intersect at right angle
    match (isVertical seg1.Direction, isVertical seg2.Direction) with
    | (true, true) | (false, false) -> false
    | _ ->
        // check if segments intersect
        let seg1Range = calculateRange seg1.Start end1 (isVertical seg1.Direction)
        let seg2Range = calculateRange seg2.Start end2 (isVertical seg2.Direction)

        let seg1Pos = if isVertical seg1.Direction then seg1.Start.X else seg1.Start.Y
        let seg2Pos = if isVertical seg1.Direction then seg2.Start.Y else seg2.Start.X

        seg1Pos > fst seg2Range && seg1Pos < snd seg2Range &&
        seg2Pos > fst seg1Range && seg2Pos < snd seg1Range

///<summary>count number of intersections within a list of segments</summary>
let countRightAngleIntersect (segVectorList: list<SegVector>) =
    let verticals = segVectorList |> List.filter (fun seg -> seg.Direction.X = 0.0)
    let horizontals = segVectorList |> List.filter (fun seg -> seg.Direction.Y = 0.0)

    let mutable count = 0

    verticals
    |> List.collect (fun vSeg -> horizontals |> List.filter (isCrossingAtRightAngle vSeg))
    |> List.length


/// <summary>
/// Count the total number of right angle intersections of two differet wires
/// </summary>
/// <param name="model">the model</param>
/// <returns>integer indicate number of Intersect.</returns>
let countTotalRightAngleIntersect (model: SheetT.Model) =
    model.Wire.Wires
    |> Map.toList // Convert the map of wires to a list of (key, value) pairs
    |> List.collect (fun (wId, _) -> wireToSegments wId model) // flatten the lists of segments
    |> countRightAngleIntersect



//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// T4 Sum of wiring segment length, counting only one when there are N same-net 
// segments overlapping (this is the visible wire length on the sheet). Count over whole 
// sheet. 

///<summary>Check if two segments are overlapping</summary>
let isOverlapping (seg1: SegVector) (seg2: SegVector) =

    let end1 = getEndPoint seg1
    let end2 = getEndPoint seg2

    (seg1.Start.X = seg2.Start.X || seg1.Start.Y = seg2.Start.Y) &&
    ((seg1.Start.X <= end2.X && end1.X >= seg2.Start.X) ||
    (seg1.Start.Y <= end2.Y && end1.Y >= seg2.Start.Y))


///<summary>Merge two overlapping segments if they are parallel and overlapping</summary>
let mergeTwoSegments seg1 seg2 =
    
    let start1, start2 = seg1.Start, seg2.Start
    let end1, end2 = getEndPoint seg1, getEndPoint seg2

    // start and end of the merged segment
    let startX = min start1.X start2.X
    let startY = min start1.Y start2.Y
    let endX = max end1.X end2.X
    let endY = max end1.Y end2.Y

    { 
        Start = { X = startX; Y = startY }; 
        Direction = { X = endX - startX; Y = startY - endY } 
    }


///<summary> Merge all parallel overlapping segments in a list</summary>
let mergeAllOverlapSegments segList =
    let rec mergeRecursive acc remaining =
        match remaining with
        | [] -> acc
        | hd :: tl ->
            let overlaps, nonOverlaps = List.partition (isOverlapping hd) tl
            let mergedSegment = List.fold mergeTwoSegments hd overlaps
            mergeRecursive (mergedSegment :: acc) nonOverlaps
    mergeRecursive [] segList |> List.rev

/// <summary> Calculate sum of segment lengths N same-net segments overlapping count once only</summary>
/// <param name="model">the model the algo is applying to</param>
/// <returns>float indicate the length of visible wire length.</returns>
let totalVisibleWiringLength (model: SheetT.Model) =

    // list of segments without overlapping
    let segWithoutOverlap = 
        model.Wire.Wires
        |> Map.toList // Convert the map of wires to a list<SegVector>
        |> List.collect (fun (wId, _) -> wireToSegments wId model) // flatten the lists of segments

    segWithoutOverlap
    |> mergeAllOverlapSegments // Merge overlapping segments
    |> List.sumBy (fun seg -> abs seg.Direction.X + abs seg.Direction.Y) // Sum length
    
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// T5R - Number of visible wire right-angles. Count over whole sheet.
// Count the number of right angles in a wire

/// <summary>Sums up the right angles of one wire</summary>
/// <param name="wId">the Id of the wire</param>
/// <param name="model">the model the wire belongs to</param>
/// <returns>int indicate the number of right angle.</returns>
let countWireRightAngles (wId: ConnectionId) (model: SheetT.Model) =

    let segmentsPos = visibleSegments wId model

    segmentsPos
    |> List.length
    |> (fun numSegments -> 
        if numSegments <> 0 
        then numSegments - 1 
        else 0)
    

/// <summary>Sums up the right angles from all wires in the model.</summary>
/// <param name="model">the model the algo is applying to</param>
/// <returns>int indicate the number of right angle.</returns>
let countTotalRightAngles (model: SheetT.Model) =
    model.Wire.Wires
    |> Map.fold (fun acc wId _ -> acc + countWireRightAngles wId model) 0


//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
//-------------------------------------------------------------------------------------------------//
// T6R R High 

// The zero-length segments in a wire with non-zero segments on either side that have 
// Lengths of opposite signs lead to a wire retracing itself. Note that this can also apply 
// at the end of a wire (where the zero-length segment is one from the end). This is a 
// wiring artifact that should never happen but errors in routing or separation can 
// cause it. Count over the whole sheet. Return from one function a list of all the 
// segments that retrace, and also a list of all the end of wire segments that retrace so 
// far that the next segment (index = 3 or Segments.Length – 4) - starts inside a symbol. 

/// <summary>
/// Finds retracing segments within wires of a model, returning retracing segments and end-of-wire retracing segments.
/// </summary>
/// <param name="model">The model to analyze.</param>
/// <returns>A tuple with two lists: retracing segments and end-of-wire retracing segments.</returns>

let findRetracingSegments (model: SheetT.Model) : Segment list * Segment list =
    let allSegmentsXYPos =
        model.Wire.Wires
        |> Map.toList // Convert the map of wires to a list of (key, value) pairs
        |> List.map (fun (wId, _) -> visibleSegments wId model)
    
    let allSegments =
        model.Wire.Wires
        |> Map.toList // Convert the map of wires to a list of (key, value) pairs
        |> List.map (fun (wId, _) -> model.Wire.Wires[wId].Segments)

    let checkSegmentRetrace (retracingSegments: list<Segment>, endOfWireRetracing: list<Segment>)(segments: list<Segment>, segmentsXYPos: list<XYPos>)(index: int) =
        let isZeroVector (v:XYPos) = (v.X = 0.0) && (v.Y = 0.0)
        let hasOppositeDirection (v1:XYPos) (v2:XYPos) = (v1.X * v2.X < 0.0) || (v1.Y * v2.Y < 0.0)

        let thisXYPos = segmentsXYPos.[index]
        let prevXYPos = segmentsXYPos.[index - 1]
        let nextXYPos = segmentsXYPos.[index + 1]
        let thisSeg = segments.[index]
        

        match thisSeg with
        | thisSeg when isZeroVector thisXYPos ->

            match thisSeg with
                | _ when hasOppositeDirection prevXYPos nextXYPos -> 
                    (thisSeg::retracingSegments, thisSeg::endOfWireRetracing)

                | _ when index = segments.Length-1 || index = 0 ->
                    (retracingSegments, thisSeg::endOfWireRetracing)

                | _ -> 
                    (retracingSegments, endOfWireRetracing)
        | _ ->
            (retracingSegments, endOfWireRetracing)

    
    let checkWireRetrace (segments: list<Segment>, segmentsXYPos: list<XYPos>) =
        [1 .. (List.length segmentsXYPos - 1)]
        |> List.fold (fun acc idx ->
            checkSegmentRetrace acc (segments, segmentsXYPos) idx
        ) ([],[])
    
    List.zip allSegments allSegmentsXYPos
    |> List.fold (fun (acc1, acc2) (cur1, cur2) ->
            let new1, new2 = checkWireRetrace (cur1, cur2)
            (acc1 @ new1, acc2 @ new2)
        )([], [])
