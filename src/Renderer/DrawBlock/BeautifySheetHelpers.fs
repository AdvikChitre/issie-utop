module BeautifySheetHelpers

open DrawModelType
open CommonTypes
open DrawModelType.SymbolT
open DrawModelType.SheetT
open DrawModelType.BusWireT
open Optics
open Optics.Operators
open Helpers
open SymbolHelpers
open BlockHelpers
open Symbol
open BusWireRoute
open BusWire

// ################################################## //
// ##########          Helpers       ################ //
// ################################################## //

// lens to set the symbolMap in the model
let symbolMap_: Lens<SheetT.Model, Map<ComponentId, Symbol>> =
    Lens.create (fun m -> m.Wire.Symbol.Symbols) (fun v m ->
        let updatedSymbol = { m.Wire.Symbol with Symbols = v }
        { m with Wire = { m.Wire with Symbol = updatedSymbol } })

/// visibleSegments helper provided by Professor.
/// The visible segments of a wire, as a list of vectors, from source end to target end.
/// Note that a zero length segment one from either end of a wire is allowed which if present
/// causes the end three segments to coalesce into a single visible segment.
let visibleSegments (wId: ConnectionId) (model: SheetT.Model) : XYPos list =

    let wire = model.Wire.Wires[wId] // get wire from model

    /// helper to match even and odd integers in patterns (active pattern)
    let (|IsEven|IsOdd|) (n: int) =
        match n % 2 with
        | 0 -> IsEven
        | _ -> IsOdd

    /// Convert seg into its XY Vector (from start to end of segment).
    /// index must be the index of seg in its containing wire.
    let getSegmentVector (index: int) (seg: BusWireT.Segment) =
        // The implicit horizontal or vertical direction  of a segment is determined by
        // its index in the list of wire segments and the wire initial direction
        match index, wire.InitialOrientation with
        | IsEven, BusWireT.Vertical
        | IsOdd, BusWireT.Horizontal -> { X = 0.; Y = seg.Length }
        | IsEven, BusWireT.Horizontal
        | IsOdd, BusWireT.Vertical -> { X = seg.Length; Y = 0. }

    /// Return a list of segment vectors with 3 vectors coalesced into one visible equivalent
    /// if this is possible, otherwise return segVecs unchanged.
    /// Index must be in range 1..segVecs
    let tryCoalesceAboutIndex (segVecs: XYPos list) (index: int) =
        if
            index < segVecs.Length - 1
            && segVecs[index] =~ XYPos.zero
        then
            segVecs[0 .. index - 2]
            @ [ segVecs[index - 1] + segVecs[index + 1] ]
            @ segVecs[index + 2 .. segVecs.Length - 1]
        else
            segVecs

    wire.Segments
    |> List.mapi getSegmentVector
    |> (fun segVecs ->
        (segVecs, [ 1 .. segVecs.Length - 2 ])
        ||> List.fold tryCoalesceAboutIndex)

// ################################################## //
// ##########      Indiv Functions   ################ //
// ################################################## //

/// B1R: The dimensions of a custom component symbol
let getCustomComponentSymbolDims (sym: Symbol) : XYPos =
    // directly return the third element from the result of getCustomSymCorners
    // format: [|{X=0.0;Y=0.0}; {X=0.0;Y=yDim'}; {X=xDim';Y=yDim'}; {X=xDim';Y=0.0}|]
    (getCustomSymCorners sym).[2]

/// B1W: The dimensions of a custom component symbol
let setCustomComponentSymbolDims (sym: Symbol) (pos: XYPos) : Symbol =
    // Note: do not modify h and w as they are the default dimensions of the symbol.
    // We only scale them as needed
    let hScale = pos.X / sym.Component.H
    let vScale = pos.Y / sym.Component.W
    { sym with HScale = Some hScale; VScale = Some vScale }

/// B2W Helper
let setSymbolPos (sym: Symbol) (newPos: XYPos) : Symbol =
    let comp' = { sym.Component with X = newPos.X; Y = newPos.Y }
    { sym with
        Component = comp'
        Pos = newPos
        LabelBoundingBox =
            { sym.LabelBoundingBox with
                TopLeft = sym.LabelBoundingBox.TopLeft - sym.Pos + newPos } }

/// B2W: The position of a symbol on the sheet
let setSymbolPosOnSheet (symID: ComponentId) (pos: XYPos) (model: SheetT.Model) : SheetT.Model =
    let symbolMap = model.Wire.Symbol.Symbols
    let sym: Symbol =
        match symbolMap.TryFind(symID) with
        | Some s -> s
        | None -> failwithf "Symbol with id %A not found in model" symID

    let newSym = moveSymbol pos sym // let newSym be the symbol with the new position
    let newSymbolMap = Map.remove symID symbolMap |> Map.add symID newSym
    // return the model with the new symbolMap
    // { model with Wire.Symbol = { model.Wire.Symbol with Symbols = newSymbolMap } }
    model |> Optic.set symbolMap_ newSymbolMap

/// B3R: Read/write the order of ports on a specified side of a symbol
let getPortMapsOrder
    (sym: Symbol)
    (side: Edge)
    // get Symbol.PortMaps.Order which is Map<Edge, string list>
    // return all strings for given Edge
    =
    sym.PortMaps.Order.TryFind(side)
    |> Option.defaultValue []
/// B3W: Read/write the order of ports on a specified side of a symbol
let setPortMapsOrder (sym: Symbol) (newSide: Edge) (newOrder: string list) : Symbol =
    let portMaps = sym.PortMaps
    let newPortMaps =
        { portMaps with Order = portMaps.Order |> Map.add newSide newOrder }
    { sym with PortMaps = newPortMaps }

/// B4R: The reversed state of the inputs of a MUX2
// question for TA: should I always failwithf?
let getMUX2ReversedInput (sym: Symbol) : bool =
    match sym.Component.Type with
    | Mux2 ->
        sym.ReversedInputPorts
        |> Option.defaultValue false
    | _ -> failwithf "Symbol is not a MUX2"

/// B4W: The reversed state of the inputs of a MUX2
/// There are two variants of this helper: one toggles the state, and the other sets the state to a predefined value.
/// setToggleMUX2ReversedInput toggles the state of the MUX2
let setToggleMUX2ReversedInput (sym: Symbol) : Symbol =
    let toggle (state: bool option) =
        match state with
        | Some s -> Some(not s)
        | None -> None
    match sym.Component.Type with
    | Mux2 -> { sym with ReversedInputPorts = toggle sym.ReversedInputPorts }
    | _ -> failwithf "Symbol is not a MUX2"

/// B4W: The reversed state of the inputs of a MUX2
/// There are two variants of this helper: one toggles the state, and the other sets the state to a predefined value.
/// setMUX2ReversedInput sets the state of the MUX2 to a predefined value
let setMUX2ReversedInput (sym: Symbol) (state: bool) : Symbol =
    let stateOption = Some state
    match sym.Component.Type with
    | Mux2 -> { sym with ReversedInputPorts = stateOption }
    | _ -> failwithf "Symbol is not a MUX2"

/// B5R: The position of a port on the sheet. It cannot directly be written.
let getPortPosOnSheet (sym: Symbol) (port: Port) =
    // get the position of the symbol, and relative position of the port
    // add the two positions together and return the result

    let portPosOnSymbol = getPortPos (sym: Symbol) (port: Port)
    sym.Pos + portPosOnSymbol

/// B6R: The Bounding box of a symbol outline (position is contained in this)
let getSymbolOutlineBoundingBox (sym: Symbol) : BoundingBox =
    let h, w = getRotatedHAndW sym
    if sym.Annotation = Some ScaleButton then
        { TopLeft = sym.Pos - { X = 9.; Y = 9. }; H = 17.; W = 17. }
    else
        { TopLeft = sym.Pos; H = float (h); W = float (w) }

/// B7R: The rotation state of a symbol
let getSymbolRotation (sym: Symbol) : Rotation = sym.STransform.Rotation

// B7W: The rotation state of a symbol
let setSymbolRotation (sym: Symbol) (newRot: Rotation) : Symbol =
    let sTransform = sym.STransform
    let newSTransform = { sTransform with Rotation = newRot }
    { sym with STransform = newSTransform }

/// B8R: The flip state of a symbol
let getSymbolFlip (sym: Symbol) : bool = sym.STransform.flipped

/// B8W: The flip state of a symbol
let setSymbolFlip (sym: Symbol) (newFlip: bool) : Symbol =
    let sTransform = sym.STransform
    let newSTransform = { sTransform with flipped = newFlip }
    { sym with STransform = newSTransform }

/// T1R: The number of pairs of symbols that intersect each other. See Tick3 for a related function. Count over all pairs of symbols.
let countIntersectingSymbolPairs (model: SheetT.Model) =
    let boxes =
        mapValues model.BoundingBoxes
        |> Array.toList
        |> List.mapi (fun n box -> n, box)
    List.allPairs boxes boxes
    |> List.filter (fun ((n1, box1), (n2, box2)) -> (n1 <> n2) && BlockHelpers.overlap2DBox box1 box2)
    |> List.length

///T2R, T3R Helper.
/// Remove all invisible segments from wires on a sheet.Wire.Wires.
/// Input and output are both of type Map<ConnectionId, Wire>.
/// visibleSegments would've worked, but outputs an XYPos list, which is a format that isn't well accepted by the other functions and types.
/// This is achieved by utilising existing helper function segmentsToIssieVertices to convert all segments to a list of vertices.
/// It is then very easy to remove duplicate vertices.
/// We can utilise another helper function issieVerticesToSegments to convert vertices back to segments, and create new wires.
let removeWireInvisibleSegments (wires: Map<ConnectionId, Wire>) =
    wires
    |> Map.map (fun connId wire ->
        let uniqueVertices =
            segmentsToIssieVertices wire.Segments wire
            |> List.distinctBy (fun (x, y, _) -> (x, y))
        // segmentsToIssieVertices returns <float * float * bool> list
        // get rid of duplicate vertices sharing the same float values
        // later, we convert uniqueVertices back to segments

        let newSegments = issieVerticesToSegments connId uniqueVertices
        // for each wire, set the segments to the new segments
        wire |> Optic.set segments_ newSegments)

/// T2R The number of distinct wire visible segments that intersect with one or more symbols. See Tick3.HLPTick3.visibleSegments for a helper. Count over all visible wire segments.
let countVisibleSegsIntersectingSymbols (model: SheetT.Model) =
    let wireModel = model.Wire
    wireModel.Wires
    |> removeWireInvisibleSegments
    |> Map.fold
        // For each wire, define a function that takes an accumulator and a wire
        (fun acc _ wire ->
            // Add to the accumulator the count of intersections between the wire and symbols
            // The count is obtained by calling BusWireRoute.findWireSymbolIntersections, which returns a list of intersections,
            // and then using List.length to get the count of intersections
            acc
            + (BusWireRoute.findWireSymbolIntersections wireModel wire
               |> List.length))
        // Initialize the accumulator to 0
        0

/// T3R helper: Returns true if two 1D line segments intersect at a 90º angle.
/// Takes in two segments described as point-to-point  (XYPos * XYPos).
/// A variant of overlap2D in BlockHelpers.fs.
let perpendicularOverlap2D ((a1, a2): XYPos * XYPos) ((b1, b2): XYPos * XYPos) : bool =
    let overlapX = overlap1D (a1.X, a2.X) (b1.X, b2.X)
    let overlapY = overlap1D (a1.Y, a2.Y) (b1.Y, b2.Y)
    (overlapX || overlapY)
    && not (overlapX && overlapY)

/// T3R The number of distinct pairs of segments that cross each other at right angles.
/// Does not include 0 length segments or segments on same net intersecting at one end, or
/// segments on same net on top of each other. Count over whole sheet.
/// This can be potentially expanded to include more cases by modifying List.filter with more conditions
let countVisibleSegsPerpendicularCrossings (model: SheetT.Model) =
    let wireModel = model.Wire
    // Get all the wires from the Wire model
    let wires = Map.toList wireModel.Wires
    // Get all the absolute segments from each wire
    let segments =
        wires
        |> List.collect (fun (_, wire) ->
            getAbsSegments wire
            |> List.map (fun seg -> (wire, seg)))
    // Generate all pairs of segments
    let allPairs =
        segments
        |> List.mapi (fun i seg1 ->
            segments
            |> List.skip (i + 1)
            |> List.map (fun seg2 -> (seg1, seg2)))
        |> List.concat
    // Filter the pairs to get only those that belong to wires that do not share the same InputPortId or OutputPortId
    // and intersect at a 90º angle
    let filteredPairs =
        allPairs
        |> List.filter (fun ((wire1, seg1), (wire2, seg2)) ->
            wire1.InputPort <> wire2.InputPort
            && wire1.OutputPort <> wire2.OutputPort
            && overlap2D (seg1.Start, seg1.End) (seg2.Start, seg2.End))
    // Return the count of filtered pairs
    List.length filteredPairs

/// T4R Top-Level Helper: For N wires in the same-net, starting from the same port, count the total length
/// of segments that all overlap together multiplied by (N-1).
/// This value (scaled overlap count) is used to offset/substract from total number of segments. Count over whole sheet.
/// At the bottom of the function is an in-depth explanation of the algorithm.
/// See T4R getApproxVisibleSegmentsLength function for more information.
///
///             visible_Segments_Length
///             ≈ getApproxVisibleSegmentsLength
///             = totalSegmentsLength - (N-1) * sharedNetOverlapLength
///             = totalSegmentsLength - getSharedNetOverlapOffsetLength
///
let getSharedNetOverlapOffsetLength (model: SheetT.Model) =
    //#######################
    //   HELPERS
    //#######################
    /// Helper for T4R that takes in a group of wires and returns the length of the wire with the least number of segments
    let getMinSegments (wireGroup: (Wire * ASegment list) list) =
        wireGroup
        |> List.minBy (fun (_, absSegments) -> List.length absSegments)
        |> snd
        |> List.length

    /// Helper for T4R that takes in a list of absolute segments that all share the same start or end position.
    /// check if all the segments are moving horizontally or vertically, by checking ASegment.orientation
    /// check if every Asegment.Segment.Length is the same sign, then return the minimum absolute value
    /// if not, return 0.0 (no segments with shared orientations will overlap if they are travelling in opposite magnitudes)
    let partialOverlapDistance (absSegments: ASegment list) : float =
        let (allAligned: bool) =
            absSegments
            |> List.map (fun absSegment -> absSegment.Orientation)
            |> List.distinct
            |> List.length = 1
        let (allSameSign: bool) =
            absSegments
            |> List.map (fun absSegment -> absSegment.Segment.Length >= 0.0)
            |> List.distinct
            |> List.length = 1
        if allAligned && allSameSign then
            // get minimum absolute value of all the segment lengths
            absSegments
            |> List.map (fun absSegment -> absSegment.Segment.Length)
            |> List.map abs
            |> List.min
        else
            0.0

    /// Helper for T4R that find the diverging index of a group of wires, d, of a list of (Wire * ASegment List).
    /// all wires's segments from index 0 up to index d-1 are identical, i.e. have the same start and end points.
    /// at index d, this is when all the wires' segments at index d starts to have different end points, i.e. they diverge
    /// this function also determines if d exists in the shortest wire, as we will need to check the
    /// dth segment differently for all wires.
    let findDivergingIndex (wireGroup: (Wire * ASegment list) list) (isOutput) =
        // FSharp doesn't support negative indexing, so this is an implementation
        let inline accessSegment (absSegments: ASegment list) (index: int) isOutput =
            if (isOutput) then
                absSegments.[index]
            else
                absSegments.[absSegments.Length - index - 1]

        let minSegments = getMinSegments wireGroup
        let d =
            // iterate up d
            [ 0 .. minSegments - 1 ]
            |> Seq.tryFindIndex (fun d ->
                let startsAreSame =
                    wireGroup
                    |> List.map (fun (_, absSegments) -> (accessSegment absSegments d isOutput).Start) // get a list of all dth segment's start points
                    |> List.distinct // remove duplicates from list of start points, if all are identical, then list length = 1
                    |> List.length = 1
                let endsAreSame =
                    wireGroup
                    |> List.map (fun (_, absSegments) -> (accessSegment absSegments d isOutput).End) // get a list of all dth segment's end points
                    |> List.distinct // remove duplicates from list of start points, if all are identical, then list length = 1
                    |> List.length = 1
                not (startsAreSame && endsAreSame)) // We will find the first index, d, where all wires' 0th to (d-1)th segments share the same start and end points
        // but the dth segments have different end points
        match d with
        | Some d -> (d, (d <= minSegments - 1))
        | None -> minSegments - 1, true

    //#######################
    //   START OF FUNCTION
    //#######################

    let wires = Map.toList (removeWireInvisibleSegments (model.Wire.Wires))
    let wiresWithAbsSegments =
        wires
        |> List.map (fun (_, wire) -> (wire, getAbsSegments wire))

    // get and group wires that share the same input and output ports
    let outputGroupedWires =
        wiresWithAbsSegments
        |> List.groupBy (fun (wire, _) -> (wire.OutputPort))
        // type (InputPortId * (Wire * ASegment list) list) list
        // get rid of InputPortId groups that have less than or equal to 1 wire
        |> List.filter (fun (_, wireGroup) -> List.length wireGroup > 1)
        // type (InputPortId * (Wire * ASegment list) list) list, coalsce to ((Wire * ASegment list) list) list)
        |> List.map (fun (_, wireGroup) -> wireGroup)

    let inputGroupedWires =
        wiresWithAbsSegments
        |> List.groupBy (fun (wire, _) -> (wire.InputPort))
        // type (InputPortId * (Wire * ASegment list) list) list
        // get rid of InputPortId groups that have less than or equal to 1 wire
        |> List.filter (fun (_, wireGroup) -> List.length wireGroup > 1)
        // type (InputPortId * (Wire * ASegment list) list) list, coalsce to ((Wire * ASegment list) list) list)
        |> List.map (fun (_, wireGroup) -> wireGroup)

    let calculateOverlapLengths (wireGroups: (Wire * ASegment list) list) isOutput =
        if wireGroups = [] then //to prevent errors
            0.0
        else
            let d, dExists = findDivergingIndex wireGroups isOutput
            printf "D: %A" d
            let distanceTravelledByIdenticalSegs =
                // if starting from output, take first wire, calculate length of segments in 0th to (d-1)th index
                // if starting from input, take first wire, calculate length of segments from the last to (last+1-d)th index

                match d > 0, isOutput with
                | false, _ -> 0.0
                | true, true ->
                    wireGroups[0]
                    |> snd
                    // cut the wire to the (d-1)th segment, take the first (d) elements
                    |> List.take (d)
                    |> List.fold (fun acc segment -> acc + euclideanDistance segment.Start segment.End) 0.0
                | true, false ->
                    wireGroups[0]
                    |> snd
                    // take the last (d) elements of the wire, slice out
                    |> List.skip ((snd wireGroups[0]).Length - d)
                    |> List.fold (fun acc segment -> acc + euclideanDistance segment.Start segment.End) 0.0

            let distanceTravelledByDthSegs =
                // get all wires' (dth)th segments
                if dExists then
                    wireGroups
                    |> List.map (fun wireGroup ->
                        match (snd wireGroup) |> List.tryItem d with
                        | Some item -> item
                        | None -> failwith "Index out of range") // will never happen
                    // find the distances travelled by the dth segments
                    // we use Euclidean distance, although we know that the segments are either horizontal or vertical
                    |> partialOverlapDistance
                else
                    0.0
            // return the shared overlap length
            (distanceTravelledByIdenticalSegs
             + distanceTravelledByDthSegs)
            * (float (List.length wireGroups - 1))

    // map to calculateOverlapLengths to inputGroupedWires and return the sum of list elements
    let outputOverlapLengths =
        let outputOverlaps =
            outputGroupedWires
            |> List.map (fun wireGroup -> calculateOverlapLengths wireGroup true)
        let inputOverlaps =
            inputGroupedWires
            |> List.map (fun wireGroup -> calculateOverlapLengths wireGroup false)
        inputOverlaps @ outputOverlaps |> List.sum

    outputOverlapLengths
(*
    Algorithm: find the diverging index, or dth index, where segments of all wires up to index d-1th are identical, i.e. have the same start and end points.
    The dth segments are when the wires start to diverge.
    If wires net is at the input ports, the wires share a same end point,
    the dth segment starting from the end, is the first instance of the segments not sharing a start point

    Examples with 3 wires sharing an output port
    Example 1:  [(0,0, 0,1)], [(0,0 to 0,3)], [(0,0 to 0,5)],
    Shared overlap length is 1.

    Example 2: [(0,0 to 0,-1)], [(0,0 to 0,3)], [(0,0 to 0,5)],
    Shared overlap length is 0.

    Example 3: [(0,0 to 0,3), (0,3 to 1,3)], [(0,0 to 0,2), (0,3 to 5,3)], [(0,0 to 0,3), (0,3 to 7,3)],
    Shared overlap length is 2.

    Example 4: [(0,0 to 0,3), (0,3 to 1,3)], [(0,0 to 0,3), (0,3 to 5,3)], [(0,0 to 0,3), (0,3 to 7,3)],
    Shared overlap length is 3 + 1 = 4.

    Example 5 consists of 2 wires sharing an input port (and share the end point)
    Don't get confused! Input points share the same end
    [(4,5 to 4,3), (4,3 to 9,3), (9,3 to 9,5)],
                  [(4,3 to 9,3), (9,3 to 9,5)]
    Starting from the end, shared overlap length is 2 + 5 = 7

    If wires net is at the output ports, the wires share a same start point,
    the dth segment from the start, the first instance of the segments not sharing an end point
    Calculate the distance travelled by the kth segments for all wires (they will be the same for all, so use the first wire).

    The (k+1)th segment of each wires will have a same starting point and same travel direction, but different end points.
    The shared overlap length is the minimum of the end points of the (k+1)th segments of all wires.
    *)

/// T4R Helper: Get total length of wire segments in a model
let getTotalSegmentsLength (model: SheetT.Model) =
    let wireModel = model.Wire
    wireModel.Wires
    |> removeWireInvisibleSegments
    |> Map.fold
        (fun acc _ wire ->
            wire.Segments
            |> List.sumBy (fun seg -> abs (seg.Length)))
        0.0

/// T4R Top-Level function. Returns the sum of wiring segment length, counting only one when there are N same-net
/// segments overlapping (this is the visible wire length on the sheet). Count over whole
/// sheet.
/// Limitations: This function uses an approximation, only considers overlaps from one 'patch' of contiguous segments of wires that start from the same port
/// It will not count partial overlaps of less than N wires, nor subsequent overlappings after the first patch of contiguous segments (in the case wires diverge and later return to overlap)
/// So this value is not always exact. But since we assume most overlaps happen once with all wires starting from a shared port,
/// the approximation below is good enough, since it is likely to be used as a heuristic for wiring complexity.
/// It is approximated by (for n wires in a net):
///
///              totalSegmentsLength - (N-1) * sharedNetOverlapLength
///           => totalSegmentsLength - getSharedNetOverlapOffsetLength
///
/// To calculate the exact length accounting for overlaps is extremely complex (it is actively researched in computational geometry)
/// and becomes even more computational if considering x overlaps of 1..x..n
let getApproxVisibleSegmentsLength (model: SheetT.Model) =
    (getTotalSegmentsLength model)
    - (getSharedNetOverlapOffsetLength model)

///T5R:  Number of visible wire right-angles. Count over whole sheet. Note that this also counts wires that overlap
let countVisibleRAngles (model: SheetT.Model) =
    let wireModel = model.Wire
    wireModel.Wires
    |> removeWireInvisibleSegments
    // for each wire, get its wire.Segments, list, find its length minus 1
    |> Map.fold
        (fun acc _ wire ->
            wire.Segments.Length - 1
            // add the length of the segments list minus 1 to the accumulator
            + acc)
        // initialise the accumulator to 0
        0

/// T6R Function: The zero-length segments in a wire with non-zero segments on either side that have
/// Lengths of opposite signs lead to a wire retracing itself.
/// Returns a list tuple with:
/// 1. a list of all segments that retrace
/// 2. a list that includes adjacent segments to the retracing that starts(intersects) a symbol
// Algorithm:
// Repeat for every wire on the sheet:
// 1. Get the absolute segments of the wire
// 2. Look for a 3-segment pattern of a non-zero segment followed by a zero segment followed by a non-zero segment that has opposite signs to the previous
// 3. Add them as a list to a list of retracing segments
// 4. Add them as a list to a second retracing segments, ALSO including the segment before and after the pattern if it exists and is nonzero (max 5 segments in total)
// 5. For the 2nd list, run them through the function findSymbolIntersections
// Note that this function returns lists of segment lists, using a list of segments to represent a 'group' of retracing segments
// To fully satisfy T6R, we need to get a function that returns all unique segments within the list
// See function countUniqRetracingSegmentsAndIntersects
let getRetracingSegmentsAndIntersections (model: SheetT.Model) =
    let wireModel = model.Wire
    // wireModel.Wires is Map<ConnectionId, Wire>, Wire is a record with a list of segments
    // get a Map<Wire, Segment list> from wireModel.Wires
    // iterate through map, get wire, form a map with key value pair of Wire, Wire.Segments
    wireModel.Wires
    |> Map.fold
        (fun (retracingSegments_acc, retracingSegmentsWithIntersections_acc) _ wire ->
            let segments = wire.Segments
            let retracingSegments =
                segments
                |> List.windowed 3
                |> List.filter (fun segs ->
                    let (seg1, seg2, seg3) = (segs.[0], segs.[1], segs.[2])
                    (seg1.Length <> 0.0)
                    && (seg2.Length = 0.0)
                    && (seg3.Length <> 0.0)
                    && (seg1.Length * seg3.Length < 0.0)) // nonzero segments must have opposite signs
            let retracingSegmentsWithIntersections =
                segments
                |> List.windowed 3
                |> List.mapi (fun i segs ->
                    let (seg1, seg2, seg3) = (segs.[0], segs.[1], segs.[2])
                    if
                        (seg1.Length <> 0.0) // look for a pattern if NONZERO, ZERO, NONZERO
                        && (seg2.Length = 0.0)
                        && (seg3.Length <> 0.0)
                        && (seg1.Length * seg3.Length < 0.0) // nonzero segments must have opposite signs
                    then // look for the segment before and after the pattern if it exists and is nonzero
                        // Identify some NONZERO, NONZERO, ZERO, NONZERO, some NONZERO
                        let prev =
                            if i > 0 && segments.[i - 1].Length <> 0.0 then
                                [ segments.[i - 1] ]
                            else
                                []
                        let next =
                            if
                                i < List.length segments - 3
                                && segments.[i + 3].Length <> 0.0
                            then
                                [ segments.[i + 3] ]
                            else
                                []
                        Some(prev @ segs @ next)
                    else
                        None)
                |> List.choose id
                |> List.filter (fun segs ->
                    // get the first segment since all of them share WId,
                    // look it up in wireModel and find the wire that has these segments
                    // update the wire with the new segments
                    let testWire = { wireModel.Wires[segs[0].WireId] with Segments = segs }
                    // now check if that wire intersects with any symbols by
                    // running it with findSymbolIntersections and making sure list is non-empty
                    testWire
                    |> findWireSymbolIntersections wireModel
                    |> List.isEmpty
                    |> not)

            (retracingSegments @ retracingSegments_acc,
             retracingSegmentsWithIntersections
             @ retracingSegmentsWithIntersections_acc))
        ([], [])
(*
 Note that this can also apply
 at the end of a wire (where the zero-length segment is one from the end). This is a
 wiring artifact that should never happen but errors in routing or separation can
 cause it. Count over the whole sheet. Return from one function a list of all the
 segments that retrace, and also a list of all the end of wire segments that retrace so
 far that the next segment (index = 3 or Segments.Length – 4) - starts inside a symbol.
*)

/// T6R Function Variant.
/// The original T6R function, getRetracingSegmentsAndIntersections returns a tuple with:
/// 1. a list of all segments that retrace
/// 2. a list that includes adjacent segments to the retracing that starts(intersects) a symbol
///
/// This variant function, countUniqRetracingSegmentsAndIntersects, counts the unique segments where this occurs in a sheet.
/// This is a helpful heuristic to count wiring artefacts, to test routing algorithms
let countUniqRetracingSegmentsAndIntersects (model: SheetT.Model) =
    let (retracingSegments, retracingSegmentsWithIntersections) =
        getRetracingSegmentsAndIntersections model

    let uniqRetracingSegments =
        retracingSegments
        |> List.collect id
        |> List.distinct
    let uniqRetracingSegmentsWithIntersections =
        retracingSegmentsWithIntersections
        |> List.collect id
        |> List.distinct

    (List.length uniqRetracingSegments, List.length uniqRetracingSegmentsWithIntersections)
