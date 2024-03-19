module TestDrawBlockD3
open GenerateData
open Elmish

(******************************************************************************************
   This submodule contains a set of functions that enable random data generation
   for property-based testing of Draw Block wire routing functions.
   basic idea.
   1. Generate, in various ways, random circuit layouts
   2. For each layout apply smartautoroute to regenerate all wires
   3. Apply check functions to see if the resulting wire routing obeys "good layout" rules.
   4. Output any layouts with anomalous wire routing
*******************************************************************************************)

open TestDrawBlock
open TestLib
open TestDrawBlock.HLPTick3
open TestDrawBlock.HLPTick3.Asserts
open TestDrawBlock.HLPTick3.Builder
open TestDrawBlock.HLPTick3.Tests

open EEExtensions
open Optics
open Optics.Operators
open DrawHelpers
open Helpers
open CommonTypes
open ModelType
open DrawModelType
open Sheet.SheetInterface
open GenerateData
open SheetBeautifyHelpers
open SheetBeautifyHelpers.SegmentHelpers
open SheetBeautifyD3
open BusWireUpdate
open RotateScale


//------------------------------------------------------------------------------------------------------------------------//
//------------------------------functions to build issue schematics programmatically--------------------------------------//
//------------------------------------------------------------------------------------------------------------------------//

module Builder =
    let segsConnectedToSym (sheet: SheetT.Model) (sym: SymbolT.Symbol) =
        let countVisSegsInWire (wire: BusWireT.Wire) =
            visibleSegments wire.WId sheet
            |> List.length

        let symPortIds = 
            sym.PortMaps.Order
            |> mapValues
            |> Array.toList
            |> List.concat
        
        sheet.Wire.Wires
        |> Map.filter (fun _ wire -> List.contains (string wire.InputPort) symPortIds || List.contains (string wire.OutputPort) symPortIds)
        |> Map.toList
        |> List.map (fun (_, wire) -> wire)
        |> List.map countVisSegsInWire
        |> List.sum


    /// Print info needed for reverse circuit generation from sheet
    let printCircuitBuild (sheet: SheetT.Model) =
        failwithf "Not implemented"


//--------------------------------------------------------------------------------------------------//
//----------------------------------------Example Test Circuits using Gen<'a> samples---------------//
//--------------------------------------------------------------------------------------------------//

open Builder

/// small offsets in X&Y axis
let rnd=System.Random()

let placeSymbol (symLabel: string) (compType: ComponentType) (position: XYPos) (rotation:Rotation) (flip:option<SymbolT.FlipType>) (model: SheetT.Model) : Result<SheetT.Model, string> =
        let symLabel = String.toUpper symLabel // make label into its standard casing
        let symModel, symId = SymbolUpdate.addSymbol [] (model.Wire.Symbol) position compType symLabel
        let rotateModel = RotateScale.rotateBlock [symId] symModel rotation
        let flipModel = 
            match flip with
            |None -> rotateModel
            |Some f -> RotateScale.flipBlock [symId] rotateModel f 
        let sym = flipModel.Symbols[symId]
        match position + sym.getScaledDiagonal with
        | {X=x;Y=y} when x > maxSheetCoord || y > maxSheetCoord ->
            Error $"symbol '{symLabel}' position {position + sym.getScaledDiagonal} lies outside allowed coordinates"
        | _ ->
            model
            |> Optic.set symbolModel_ flipModel
            |> SheetUpdateHelpers.updateBoundingBoxes // could optimise this by only updating symId bounding boxes
            |> Ok
let dSelect (model:SheetT.Model) = 
    { model with
        SelectedComponents = []
        SelectedWires = []
    }
let selectA (model:SheetT.Model) = 
    let symbols = model.Wire.Symbol.Symbols |> Map.toList |> List.map fst
    let wires = model.Wire.Wires |> Map.toList |> List.map fst
    { model with
        SelectedComponents = symbols
        SelectedWires = wires
    }
let test2Builder = 
    let intToRot (x: int)= 
        match x with
        | 0 -> Degree90
        | 1 -> Degree90
        | 2 -> Degree180
        | 3 -> Degree270
        | _ -> Degree0
    let ints = GenerateData.map intToRot (randomInt 0 1 3)
    let floats = randomFloat 100 20 200
    List.allPairs (GenerateData.toList floats) (GenerateData.toList ints)
    |> List.toArray
    |> GenerateData.shuffleA
    |> GenerateData.fromArray
    

let test1Builder = 
    let getRotation x=  
            match x with
                |1  -> Degree0
                |2  -> Degree90
                |3  -> Degree180
                |4  -> Degree270
                |_  -> Degree0


    let getFlip x=
        match x with
            |1  -> Some SymbolT.FlipHorizontal
            |2  -> Some SymbolT.FlipVertical
            |3  -> None
    let thing = 0
    let allFlip = List.map getFlip [1..3]
    let allRot = List.map getRotation [1..4] 
    let combinations lst =
        [ for x in lst do
            for y in lst do
                for z in lst do
                    yield [x; y; z] ]
    List.allPairs allRot allFlip
    |>combinations 
    |>fromList



let offsetXY =
    let offsetX = randomFloat -2. 0.1 2.
    let offsetY = randomFloat -2. 0.1 2.
    (offsetX, offsetY)
    ||> product (fun (x: float) (y: float) -> {X=x; Y=y})

/// Returns the position in respect to the centre of the sheet
let pos x y = 
    middleOfSheet + {X=float x; Y=float y}

let makeTest1Circuit (ori:list<Rotation*(SymbolT.FlipType option)>)=
    let Mux1Pos = middleOfSheet + {X=300. ; Y=0.}
    let Mux2Pos = middleOfSheet + {X=300. ; Y=300.}
    initSheetModel
    |> placeSymbol "DM1" Demux4 middleOfSheet  (fst ori[0]) (snd ori[0])
    |> Result.bind(placeSymbol "MUX1" Mux4 Mux1Pos (fst ori[1]) (snd ori[1]))
    |> Result.bind (placeWire (portOf "DM1" 0) (portOf "MUX1" 0))
    |> Result.bind (placeWire (portOf "DM1" 1) (portOf "MUX1" 1))
    |> Result.bind (placeWire (portOf "DM1" 2) (portOf "MUX1" 2))
    |> Result.bind (placeWire (portOf "DM1" 3) (portOf "MUX1" 3))
    |> Result.bind(placeSymbol "MUX2" Mux4 Mux2Pos (fst ori[2]) (snd ori[2]))
    |> Result.bind (placeWire (portOf "DM1" 0) (portOf "MUX2" 0))
    |> Result.bind (placeWire (portOf "DM1" 1) (portOf "MUX2" 1))
    |> Result.bind (placeWire (portOf "DM1" 2) (portOf "MUX2" 2))
    |> Result.bind (placeWire (portOf "DM1" 3) (portOf "MUX2" 3))
    |> getOkOrFail
    |> autoGenerateWireLabels

let makeTest2Circuit (data: float*Rotation)=
    let rotation = snd data
    let gap = fst data
    printf "Test 2 rotation: %A" rotation
    printf "Test 2 gap: %A" gap
    let Pos1 = middleOfSheet + {X=gap ; Y=0.}
    let Pos2 = Pos1 + {X=gap ; Y=0.}
    let Pos3 = Pos2 + {X=gap ; Y=0.}
    let noWireModel =
        initSheetModel
        |> placeSymbol "C1" (Constant1( Width=8 , ConstValue=0 , DialogTextValue="0" )) middleOfSheet Degree0 None
        |> Result.bind(placeSymbol "SN1" (SplitN(3,[2;3;3],[0;1;2])) Pos1 Degree0 None)
        |> Result.bind(placeSymbol "MN1" (MergeN(3)) Pos2 Degree0 None)
        |> Result.bind(placeSymbol "B" (Output(8)) Pos3 Degree0 None)
        |> getOkOrFail
        |> selectA
    let rotModel = 
        match rotation with
        |Degree0 -> noWireModel
        |_ -> Optic.set symbolModel_ (rotateBlock noWireModel.SelectedComponents noWireModel.Wire.Symbol rotation) noWireModel
    let model =
        rotModel
        |> placeWire (portOf "C1" 0) (portOf "SN1" 0)
        |> Result.bind (placeWire (portOf "SN1" 0) (portOf "MN1" 0))
        |> Result.bind (placeWire (portOf "SN1" 1) (portOf "MN1" 1))
        |> Result.bind (placeWire (portOf "SN1" 2) (portOf "MN1" 2))
        |> Result.bind (placeWire (portOf "MN1" 0) (portOf "B" 0))
        |> getOkOrFail

    {model with Wire = model.Wire |>calculateBusWidths |>fst}


    
//------------------------------------------------------------------------------------------------//
//-------------------------Example assertions used to test sheets---------------------------------//
//------------------------------------------------------------------------------------------------//


module Asserts =
    let failOnAllTests (sample: int) _ =
            Some <| $"Sample {sample}"
//---------------------------------------------------------------------------------------//
//-----------------------------Demo tests on Draw Block code-----------------------------//
//---------------------------------------------------------------------------------------//

module Tests =
    
    let D3Test1 testNum firstSample dispatch =
        runTestOnSheets
            "Mux conected to 2 demux"
            firstSample
            test1Builder
            None
            makeTest1Circuit
            (AssertFunc failOnAllTests)
            Evaluations.nullEvaluator
            dispatch
        |> recordPositionInTest testNum dispatch
    
    let D3Test2 testNum firstSample dispatch =
        runTestOnSheets
            "Test for label placement"
            firstSample
            test2Builder
            None
            makeTest2Circuit
            (AssertFunc failOnAllTests)
            Evaluations.nullEvaluator
            dispatch
        |> recordPositionInTest testNum dispatch

    // ac2021: CAUSED COMPILE ERRORS SO COMMENTED
    // ac2021: I think it was caused by Alina's pr?
    // let D3Test2 testNum firstSample dispatch =
    //     runTestOnSheets
    //         "two custom components with random offset: fail all tests"
    //         firstSample
    //         offsetXY
    //         makeTest2Circuit
    //         Asserts.failOnAllTests
    //         dispatch
    //     |> recordPositionInTest testNum dispatch

    let testsToRunFromSheetMenu : (string * (int -> int -> Dispatch<Msg> -> Unit)) list =
        // Change names and test functions as required
        // delete unused tests from list
        [
            "Test1", D3Test1 // example
            "Test2", D3Test2 // example
            "Next Test Error", fun _ _ _ -> printf "Next Error:" // Go to the nexterror in a test
        ]
    
    let nextError (testName, testFunc) firstSampleToTest dispatch =
            let testNum =
                testsToRunFromSheetMenu
                |> List.tryFindIndex (fun (name,_) -> name = testName)
                |> Option.defaultValue 0
            testFunc testNum firstSampleToTest dispatch
    
    let testMenuFunc (testIndex: int) (dispatch: Dispatch<Msg>) (model: Model) =
            let name,func = testsToRunFromSheetMenu[testIndex] 
            printf "%s" name
            match name, model.DrawBlockTestState with
            | "Next Test Error", Some state ->
                nextError testsToRunFromSheetMenu[state.LastTestNumber] (state.LastTestSampleIndex+1) dispatch
            | "Next Test Error", None ->
                printf "Test Finished"
                ()
            | _ ->
                func testIndex 0 dispatch