module TestDrawBlockFlip

open Elmish
open TimeHelpers
open GenerateData
open TestDrawBlockHelpers
open TestDrawBlockHelpers.TestLib
open TestDrawBlockHelpers

open TestDrawBlockHelpers.Asserts
open TestDrawBlockHelpers.Builder
open SheetBeautifyHelpers
open SheetBeautifyAlign
open SheetBeautifyFlip
open SheetBeautifyWireLabel
open SheetBeautify


open EEExtensions
open Optics
open Optics.Operators
open DrawHelpers
open Helpers
open CommonTypes
open ModelType
open DrawModelType
open Sheet.SheetInterface


//------------------------------------------------------------------------------------------------------------------------//
//------------------------------functions to build issue schematics programmatically--------------------------------------//
//------------------------------------------------------------------------------------------------------------------------//
module Builder =
    open Displays

    // ----------------------Circuit Builders DSL----------------------
    /// adapted from EdStem: minimal DSL for placing symbols on the sheet
    let minimalDSL (initSheetModel: SheetT.Model) (placers: (SheetT.Model -> Result<SheetT.Model,'a>) list) : Result<SheetT.Model,'a> = 
        (Ok initSheetModel, placers)
        ||> List.fold (fun circuitRes placer -> Result.bind placer circuitRes)

    let initCCSheet =
        [
            placeSymbol "X1" (Input1(1, None)) {X=middleOfSheet.X-150.; Y=middleOfSheet.Y};
            placeSymbol "X2" (Input1(1, None)) {X=middleOfSheet.X-150.; Y=middleOfSheet.Y+100.};
            placeSymbol "X3" (Input1(1, None)) {X=middleOfSheet.X-150.; Y=middleOfSheet.Y+200.};
            placeSymbol "X4" (Input1(1, None)) {X=middleOfSheet.X-150.; Y=middleOfSheet.Y+300.};
            placeSymbol "Y1" (Output(1)) {X=middleOfSheet.X+150.; Y=middleOfSheet.Y};
            placeSymbol "Y2" (Output(1)) {X=middleOfSheet.X+150.; Y=middleOfSheet.Y+10.};
            placeSymbol "Y3" (Output(1)) {X=middleOfSheet.X+150.; Y=middleOfSheet.Y+20.};
            placeSymbol "Y4" (Output(1)) {X=middleOfSheet.X+150.; Y=middleOfSheet.Y+300.};
        ]
        |> minimalDSL initSheetModel
        |> getOkOrFail

    /// DSL for symbol placer, automatically names the symbol using a given index
    let symPlacerDSL (idx: int) (compType: ComponentType) (pos: XYPos) (model: Model) (sheetModel: SheetT.Model) : Result<SheetT.Model, string> =
        match compType with
        | Custom(ccType) -> placeCustomSymbol (sprintf "S%d" idx) ccType model pos sheetModel
        | comp -> placeSymbol (sprintf "S%d" idx) comp pos sheetModel

    /// DSL for wire placer
    let wirePlacerDSL (sourceSymLabel: string) (targetSymLabel: string) (sourceIdx) (targetIdx) (sheetModel: SheetT.Model) : Result<SheetT.Model, string> =
        sheetModel
        |> placeWire (portOf sourceSymLabel sourceIdx) (portOf targetSymLabel targetIdx) 

//--------------------------------------------------------------------------------------------------//
//---------------------------------------- Predefined Gen<'a> and Helpers---------------------------//
//--------------------------------------------------------------------------------------------------//
module Generator =
    open System
    open Builder

    type GenCompStates = {
        Comp: ComponentType; 
        Pos: XYPos; 
        Flip: SymbolT.FlipType option; 
        Rotate: Rotation option
    }

    // ---------------------- Generator Constants ----------------------
    let ccType: CustomComponentType = {
        Name = "custom"
        InputLabels = [("X1", 1); ("X2", 1); ("X3", 1); ("X4", 1)]
        OutputLabels = [("Y1", 1); ("Y2", 1); ("Y3", 1); ("Y4", 1)]
        Form = None
        Description = None
    }

    let comps: Gen<ComponentType> = fromList [GateN(And,2); GateN(Or,2); Not; Mux2; Mux4; Custom(ccType)]
    let flips: Gen<SymbolT.FlipType option> = fromList [Some SymbolT.FlipType.FlipHorizontal; Some SymbolT.FlipType.FlipVertical; None]
    let rotates: Gen<Rotation option> = fromList [Some Degree90; Some Degree180; Some Degree270; None]

    // ---------------------- Generator Helpers ----------------------
    let makeTuple a b = (a, b)

    /// From Ed: Fischer-Yates shuffle algorithm
    /// Returns a random shuffled array without changing the input array
    let shuffleAGen (rng: System.Random) arrayToShuffle: 'a array =
        let tmpA = Array.copy arrayToShuffle
        for i = 0 to tmpA.Length - 1 do 
            let r = rng.Next(i, tmpA.Length);
            (tmpA[i],tmpA[r])
            |> fun (iv, rv) -> tmpA[r] <- iv;  tmpA[i]  <- rv
        tmpA

    /// Chunk the generator into smaller arrays of equal lengths
    let chunkShuffledGen (gen: Gen<'a>) (seed: int) (length: int) =
        gen
        |> toArray
        |> shuffleAGen (Random seed) // random by test number
        |> Array.chunkBySize length
        |> fromArray

    // ====================== Positions Generator ======================

    /// Sample data based on 11 equidistant points on a horizontal line
    let horizLinePositions =
        fromList [-100..20..100]
        |> map (fun n -> middleOfSheet + {X=float n; Y=0.})

    let gridPositions n : Gen<XYPos> =
        let genX = fromList [-n..20..n]
        let genY = fromList [-n..20..n]
        (genX, genY)
        ||> product (fun x y -> middleOfSheet + {X=float x; Y=float y})

    let gridPosGen (n: int) (interval: int) : Gen<XYPos> =
        let genX: Gen<int> = fromList [-n..interval..n]
        let genY = fromList [-n..interval..n]
        (genX, genY)
        ||> product (fun x y -> middleOfSheet + {X=float x; Y=float y})

    
    ///<summary> AUTHOR hn621 - Random Gen samples: component, position, flips, rotations</summary>
    let randomComponentSamples : Gen<GenCompStates> =
        rotates
        |> product makeTuple flips
        |> product makeTuple (gridPosGen 300 100)
        |> product makeTuple comps
        |> map (fun (comp, (pos, (flip, rotate))) -> 
                {
                    Comp=comp;
                    Pos=pos;
                    Flip=flip;
                    Rotate=rotate;
                }
        )

    ///<summary> AUTHOR hn621 - Random Gen samples: component, position, flips, rotations</summary>
    let randomComponentSamplesNoRotate : Gen<GenCompStates> =
        flips
        |> product makeTuple (gridPosGen 400 100)
        |> product makeTuple comps
        |> map (fun (comp, (pos, flip)) -> 
                {
                    Comp=comp;
                    Pos=pos;
                    Flip=flip;
                    Rotate=None;
                }
        )
    
open Builder
open Generator
// ====================== Fixed Circuit Generator ======================
let makeTest1Circuit (andPos:XYPos) =
    [
        placeSymbol "G1" (GateN(And,2)) {X=middleOfSheet.X+100.;Y=middleOfSheet.Y-100.};
        placeSymbol "S1" (Input1(1, None)) {X=middleOfSheet.X-150.;Y=middleOfSheet.Y};
        placeSymbol "S2" (Input1(1, None)) {X=middleOfSheet.X-150.;Y=middleOfSheet.Y+100.};
        placeSymbol "MUX1" Mux2 {X=middleOfSheet.X-100.;Y=middleOfSheet.Y-100.};
        placeSymbol "MUX2" Mux2 middleOfSheet;
        flipSymbol "MUX2" (Some SymbolT.FlipType.FlipVertical) >> Ok;
        placeWire (portOf "S2" 0) (portOf "MUX2" 2);
        placeWire (portOf "MUX1" 0) (portOf "MUX2" 0);
        placeWire (portOf "S1" 0) (portOf "MUX2" 1);
        placeWire (portOf "MUX2" 0) (portOf "G1" 0);
        placeWire (portOf "MUX1" 0) (portOf "G1" 1);
    ]
    |> minimalDSL initSheetModel
    |> getOkOrFail

let makeTest2Circuit (model:Model) (andPos:XYPos) = 
    let project = Option.get model.CurrentProj
    let curSheetName = project.OpenFileName
    let sheetNames = 
        project.LoadedComponents 
        |> List.map (fun ldc -> ldc.Name)
        |> List.filter (fun name -> name <> curSheetName)

    let ccSheetName = "custom"
    // CustomCompPorts.printSheetNames model
    // printfn $"{ccSheetName}"

    [
        placeCustomSymbol "CC1" ccType  model {X=middleOfSheet.X-150.;Y=middleOfSheet.Y}
        placeCustomSymbol "CC2" ccType  model {X=middleOfSheet.X-150.;Y=middleOfSheet.Y}
        placeWire (portOf "CC1" 0) (portOf "CC2" 3);
        placeWire (portOf "CC1" 1) (portOf "CC2" 2);
        placeWire (portOf "CC1" 2) (portOf "CC2" 0);
        placeWire (portOf "CC1" 3) (portOf "CC2" 1);
    ]
    |> minimalDSL initSheetModel
    |> getOkOrFail


// ====================== Random Sheet Generator ======================
let makeRandomCircuit (model:Model) (samples: GenCompStates array) =
    let folder acc (sample: GenCompStates) = 
        let (sheetModel, idx) = acc
        let newSheet =
            [
                // place component and set states
                symPlacerDSL idx sample.Comp sample.Pos model;
                flipSymbol (sprintf "S%d" idx) sample.Flip >> Ok;
                rotateSymbol (sprintf "S%d" idx) sample.Rotate >> Ok;
                // set wire connections
                if idx > 0 then 
                    wirePlacerDSL (sprintf "S%d" (idx-1)) (sprintf "S%d" (idx)) 0 0
                else id >> Ok;
            ]
            |> minimalDSL sheetModel
            |> getOkOrFail
        (newSheet, idx+1)
    
    samples
    |> Array.fold folder (initSheetModel, 0)
    |> fst


//-------------------------------------------------------------------------------------//
//-----------------------------D2 Tests on Draw Block code-----------------------------//
//-------------------------------------------------------------------------------------//

module Tests =
    open Displays
    open System
    /// Allow test errors to be viewed in sequence by recording the current error
    /// in the Issie Model (field DrawblockTestState). This contains all Issie persistent state.
    let recordPositionInTest (testNumber: int) (dispatch: Dispatch<Msg>) (result: TestResult<'a>) =
        dispatch <| UpdateDrawBlockTestState(fun _ ->
            match result.TestErrors with
            | [] ->
                printf "Test finished"
                None
            | (numb, _) :: _ ->
                printf $"Sample {numb}"
                Some { LastTestNumber=testNumber; LastTestSampleIndex= numb})
        
    let test1 testNum firstSample dispatch model =
        let displayOnFail = displayAll
        let generator = gridPosGen 1 2
        runTestOnSheets
            "DisplayAll: MUX+AND Unoptimized"
            firstSample
            generator
            makeTest1Circuit
            Asserts.failOnAllTests
            dispatch
            displayOnFail
        |> recordPositionInTest testNum dispatch

    let test2 testNum firstSample dispatch model =
        let sheetMaker = makeTest2Circuit model
        let displayOnFail = displayAll
        let generator = gridPosGen 1 2
        runTestOnSheets
            "DisplayAll: Custom Symbol"
            firstSample
            generator
            sheetMaker
            Asserts.failOnAllTests
            dispatch
            displayOnFail
        |> recordPositionInTest testNum dispatch

    let testRandomComp testNum firstSample dispatch model =
        let nComponents = 10 // note that the position generation is fixed, too large nComponents will not have non-overlapping test cases
        let sheetMaker = makeRandomCircuit model
        let displayOnFail = displayAll
        let generator = nComponents |> chunkShuffledGen (randomComponentSamples) 1
        // this assertion fails on all tests without symbol intersection!
        let assertion (sample: int) (sheetModel: SheetT.Model) = 
            sheetModel
            |> Asserts.failOnSymbolIntersectsSymbol sample
            |> function
                | Some str -> None
                | None -> Some "Random Component Test Failed"
        
        runTestOnSheets
            "DisplayAll: Random N Components"
            firstSample
            generator
            sheetMaker
            assertion
            dispatch
            displayOnFail
        |> recordPositionInTest testNum dispatch

    let testRandomCompNoRotate testNum firstSample dispatch model =
        let nComponents = 10 // note that the position generation is fixed, too large nComponents will not have non-overlapping test cases
        let sheetMaker = makeRandomCircuit model
        let displayOnFail = displayAll
        let generator = nComponents |> chunkShuffledGen (randomComponentSamplesNoRotate) 1
        // this assertion fails on all tests without symbol intersection!
        let assertion (sample: int) (sheetModel: SheetT.Model) = 
            sheetModel
            |> Asserts.failOnSymbolIntersectsSymbol sample
            |> function
                | Some str -> None
                | None -> Some "Random Component Test Failed"
        
        runTestOnSheets
            "DisplayAll: Random N Components No Rotate"
            firstSample
            generator
            sheetMaker
            assertion
            dispatch
            displayOnFail
        |> recordPositionInTest testNum dispatch

    let testD3Regular testNum firstSample dispatch model =
        
        runTestOnSheets
            "DisplayAll D3: Regular Shifts"
            firstSample
            TestDrawBlockWireLabel.GenerateRegularWireLabelReplaceTestPos
            TestDrawBlockWireLabel.makeReplaceWireLabelTestCircuit
            Asserts.failOnAllTests
            dispatch
            displayAll
        |> recordPositionInTest testNum dispatch

    /// <summary>AUTHOR hn621 - Prints out the average time to beautify a sheet of n components, where n is in range [5..5..25]</summary>
    let testBeautifyTimePerformance testNum firstSample dispatch model =
        let sheetMaker = makeRandomCircuit model
        let beautifier = SheetBeautify
        let nGenerator = chunkShuffledGen (randomComponentSamples) 1
        let nComponents = [5..5..25]

        printfn $"Now testing Beautify Time Performance..."

        nComponents
        |> List.map nGenerator
        |> List.map (fun gen -> gen.Data 0)
        |> List.map sheetMaker // prepare the sheetModel before beautify
        |> List.mapi (
            fun idx elm -> 
                recordExecutionTimeStats (sprintf "n_component=%d" (nComponents.[idx])) beautifier elm |> ignore
            )
        |> ignore

        executionStats
        |> Map.toList
        |> List.map (fun (key, value) -> (printfn "%s, avg_time=%.3f ms" key value.Av))
        |> ignore

        executionStats <- Map []
