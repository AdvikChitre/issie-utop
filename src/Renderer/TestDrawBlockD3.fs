module TestDrawBlockD3

open GenerateData
open Elmish
open TestDrawBlock.HLPTick3
open TestDrawBlock.TestLib
open TestDrawBlock.HLPTick3.Builder
open TestDrawBlock.HLPTick3.Asserts
open TestDrawBlock.HLPTick3.Tests
open CommonTypes
open DrawModelType


type wireLabelReplaceXYPos = {
    Demux4Pos: XYPos;
    Mux21Pos: XYPos;
    Mux22Pos: XYPos;
    Mux4Pos: XYPos;
};
let GenerateRegularWireLabelReplaceTestPos: Gen<wireLabelReplaceXYPos>=
    let Mux21PosList = 
        [200]
        |> List.map (fun n -> middleOfSheet + {X=float n; Y= -300.})
    let Mux4PosList = 
        [80..20..1000]
        |> List.map (fun n -> middleOfSheet + {X=float n; Y= 0.})
    let Mux22PosList = 
        [200]
        |> List.map (fun n -> middleOfSheet + {X=float n; Y= 500.})
    List.allPairs Mux21PosList Mux4PosList
    |> List.allPairs Mux22PosList
    |> List.map (fun (mux22Pos, (mux21Pos, mux4Pos)) -> 
        {Demux4Pos = middleOfSheet; Mux21Pos = mux21Pos; Mux4Pos = mux4Pos; Mux22Pos = mux22Pos})
    |> fromList
    
let makeReplaceWireLabelTestCircuit(pos: wireLabelReplaceXYPos): SheetT.Model =
    let unoptimizedSheet =
        initSheetModel
        |> placeSymbol "DM" ComponentType.Demux4 pos.Demux4Pos
        |> Result.bind (fun sheet -> placeSymbol "M21" ComponentType.Mux2 pos.Mux21Pos sheet)
        |> Result.bind (fun sheet -> placeSymbol "M4" ComponentType.Mux4 pos.Mux4Pos sheet)
        |> Result.bind (fun sheet -> placeSymbol "M22" ComponentType.Mux2 pos.Mux22Pos sheet)
        |> Result.bind (placeWire (portOf "DM" 0) (portOf "M21" 0))
        |> Result.bind (placeWire (portOf "DM" 1) (portOf "M21" 1))
        |> Result.bind (placeWire (portOf "DM" 1) (portOf "M22" 0))
        |> Result.bind (placeWire (portOf "DM" 2) (portOf "M22" 1))
        |> Result.bind (placeWire (portOf "DM" 0) (portOf "M4" 0))
        |> Result.bind (placeWire (portOf "DM" 1) (portOf "M4" 1))
        |> Result.bind (placeWire (portOf "DM" 2) (portOf "M4" 2))
        |> Result.bind (placeWire (portOf "DM" 3) (portOf "M4" 3))
        |> getOkOrFail
    unoptimizedSheet
        |> SheetBeautifyD3.wireLabelBeautify 
        
let makeReplaceWireLabelTest2Circuit(pos: wireLabelReplaceXYPos): SheetT.Model =
    let unoptimizedSheet =
        initSheetModel
        |> placeSymbol "DM" ComponentType.Demux4 pos.Demux4Pos
        |> Result.bind (fun sheet -> placeSymbol "M21" ComponentType.Mux2 pos.Mux21Pos sheet)
        |> Result.bind (fun sheet -> placeSymbol "M4" ComponentType.Mux4 pos.Mux4Pos sheet)
        |> Result.bind (fun sheet -> placeSymbol "M22" ComponentType.Mux2 pos.Mux22Pos sheet)
        |> Result.bind (placeWire (portOf "DM" 0) (portOf "M21" 0))
        |> Result.bind (placeWire (portOf "DM" 1) (portOf "M21" 1))
        |> Result.bind (placeWire (portOf "DM" 1) (portOf "M22" 0))
        |> Result.bind (placeWire (portOf "DM" 2) (portOf "M22" 1))
        |> Result.bind (placeWire (portOf "DM" 0) (portOf "M4" 0))
        |> Result.bind (placeWire (portOf "DM" 1) (portOf "M4" 1))
        |> Result.bind (placeWire (portOf "DM" 2) (portOf "M4" 2))
        |> Result.bind (placeWire (portOf "DM" 3) (portOf "M4" 3))
        |> getOkOrFail
    unoptimizedSheet
        //|> SheetBeautifyD3.wireLabelBeautify 
