﻿/// RAM display in waveform simulator
module WaveSimRams

//---------------------------------------------------------------------------------------//
//-------Functions to implement the RAM display in the waveform simulator----------------//
//---------------------------------------------------------------------------------------//

open Fulma
open Fable.React
open Fable.React.Props

open CommonTypes
open ModelType
open ModelHelpers
open WaveSimStyle
open WaveSimHelpers
open TopMenuView
open SimulatorTypes
open NumberHelpers
open DrawModelType
open WaveSimNavigation
open WaveSimSelect
open DiagramStyle


/// Table row that shows the address and data of a RAM component.
let ramTableRow ((addr, data,rowType): string * string * RamRowType): ReactElement =

    tr [ Style <| ramTableRowStyle rowType ] [
        td [] [ str addr ]
        td [] [ str data ]
    ]

/// Table showing contents of a RAM component.
let ramTable (wsModel: WaveSimModel) ((ramId, ramLabel): FComponentId * string) : ReactElement =
    let wanted = calcWaveformAndScrollBarHeight wsModel
    let maxHeight = max (screenHeight() - (min wanted (screenHeight()/2.)) - 300.) 30.
    let fs = Simulator.getFastSim()
    match Map.tryFind ramId fs.FComps with
    | None -> div [] []
    | Some fc -> 
        let step = wsModel.CurrClkCycle
        FastRun.runFastSimulation None step fs |> ignore // not sure why this is needed

        // in some cases fast sim is run for one cycle less than currClockCycle
        let memData =
            match fc.FType with
            | ROM1 mem
            | AsyncROM1 mem -> mem
            | RAM1 mem
            | AsyncRAM1 mem -> 
                match FastRun.extractFastSimulationState fs wsModel.CurrClkCycle ramId with
                |RamState mem -> mem
                | x -> failwithf $"What? Unexpected state {x} from cycle {wsModel.CurrClkCycle} \
                        in RAM component '{ramLabel}'. FastSim step = {fs.ClockTick}"
            | _ -> failwithf $"Given a component {fc.FType} which is not a vaild RAM"
        let aWidth,dWidth = memData.AddressWidth,memData.WordWidth

        let print w (a:bigint) = NumberHelpers.valToPaddedString w wsModel.Radix (((1I <<< w) - 1I) &&& a)

        let lastLocation = (1I <<< memData.AddressWidth - 1) - 1I

        /// print a single 0 location as one table row
        let print1 (a:bigint,b:bigint,rw:RamRowType) = $"{print aWidth a}",$"{print dWidth b}",rw
        /// print a range of zero locations as one table row

        let print2 (a1:bigint) (a2:bigint) (d:bigint) = $"{print aWidth (a1+1I)} ... {print aWidth (a2-1I)}", $"{print dWidth d}",RAMNormal

        /// output info for one table row filling the given zero memory gap or arbitrary size, or no line if there is no gap.
        let printGap (gStart:bigint) (gEnd:bigint) =
            let gapSize = gStart - gEnd
            if gapSize = 1I then []            
            elif gapSize = 2I then  [print1 ((gEnd + gStart) / 2I, 0I, RAMNormal)]
            elif  gapSize > 2I then [print2 gStart gEnd 0I]
            else
                failwithf $"What? gEnd={gEnd},gStart={gStart}: negative or zero gaps are impossible..."

        /// transform Sparse RAM info into strings to print in a table, adding extra lines for zero gaps
        /// line styling is controlled by a RamRowtype value and added later when the table row react is generated
        let addGapLines (items: (bigint*bigint*RamRowType) list) = 
            let startItem =
                match (items[0]) with
                | gapStart,_,_ when gapStart = -1I -> []
                | gStart,dStart,rw-> [print1 (gStart,dStart,rw)]
            List.pairwise items
            |> List.collect (fun ((gStart,_,_),(gEnd,dEnd,rwe)) -> 
                let thisItem = if gEnd = lastLocation + 1I then [] else [print1 (gEnd,dEnd,rwe)]
                [printGap gStart gEnd; thisItem])
            |> List.concat

        /// Add a RAMNormal RamRowType value to every location in mem.
        /// Add in additional locations for read and/or write if needed.
        /// Set RamRowValue type to RAMWritten or RAMRead for thse locations.
        /// Write is always 1 cycle after WEN=1 and address.
        /// Read is 1 (0) cycles after address for sync (asynch) memories.
        let addReadWrite (fc:FastComponent) (step:int) (mem: Map<bigint,bigint>) =
            let getBigint (a: IOArray) step : bigint =
                let w = a.Width
                match w with
                | w when w > 32 -> a.BigIntStep[step]
                | _ -> a.UInt32Step[step] |> bigint

            let readStep =
                match fc.FType with
                | AsyncROM1 _ | AsyncRAM1 _ -> step
                | ROM1 _ | RAM1 _ -> step - 1
                | _ -> failwithf $"What? {fc.FullName} should be a memory component"

            let addrSteps step = getBigint fc.InputLinks[0] step

            let readOpt =
                match step, fc.FType with
                | 0,ROM1 _ | 0, RAM1 _ -> None
                | _ -> 
                    addrSteps readStep
                    |> Some
            let writeOpt =
                match step, fc.FType with
                | _, ROM1 _ 
                | _, AsyncROM1 _
                | 0, _ -> None
                | _, RAM1 _ | _, AsyncRAM1 _ when getBigint fc.InputLinks[2] (step-1) = 1I -> 
                    addrSteps (step-1)
                    |> Some
                | _ ->  
                    None

            /// Mark addr in memory map as being rType
            /// if addr does not exist - create it
            let addToMap rType addr mem:Map<bigint,bigint*RamRowType> =
                match Map.tryFind addr mem with
                | Some (d,_) -> Map.add addr (d,rType) mem
                | None  ->  Map.add addr (0I,rType) mem
    

            Map.map (fun k v -> v,RAMNormal) mem
            |> (fun mem ->
                match readOpt with
                | Some addr -> addToMap RAMRead addr mem
                | None -> mem
                |> (fun mem ->
                    match writeOpt with // overwrite RAMRead here is need be
                    | Some addr -> addToMap RAMWritten addr mem
                    | None -> mem))
 

        /// add fake locations beyong normal address range so that
        /// addGapLines fills these (if need be). These locations are then removed
        let addEndPoints (items:(bigint*bigint*RamRowType) list)  =
            let ad (a,d,rw) = a
            match items.Length with
            | 0 -> [-1I,0I,RAMNormal;  lastLocation, 0I, RAMNormal]
            | _ ->
                if ad items[0] < 0I then items else List.insertAt 0 (-1I,-1I,RAMNormal) items
                |> (fun items ->
                    if ad items[items.Length-1] = lastLocation then 
                        items else 
                    List.insertAt items.Length (lastLocation+1I,0I,RAMNormal) items)
    

        let lineItems =
            memData.Data
            |> addReadWrite fc step
            |> Map.toList
            |> List.map (fun (a,(d,rw)) -> a,d,rw)
            |> List.filter (fun (a,d,rw) -> d<>0I || rw <> RAMNormal)
            |> List.sort
            |> addEndPoints 
            |> addGapLines
        


        Level.item [
            Level.Item.Option.Props ramTableLevelProps
            Level.Item.Option.HasTextCentered
        ] [
            Heading.h6 [
                Heading.Option.Props [ centerAlignStyle ]
            ] [ str ramLabel ]
            div [Style [MaxHeight maxHeight;OverflowY OverflowOptions.Auto]] [
            Table.table [
                Table.IsFullWidth
                Table.IsBordered
            ] [ thead [] [
                    tr [] [
                        th [ centerAlignStyle ] [ str "Address"]
                        th [ centerAlignStyle ] [ str "Data"; sub [Style [MarginLeft "2px"; FontSize "10px"]] [str (string wsModel.CurrClkCycle)]]
                    ]
                ]
                tbody []
                    (List.map ramTableRow lineItems) 
            ] ]
            br []
        ]

/// Bulma Level component of tables showing RAM contents.
let ramTables (wsModel: WaveSimModel) : ReactElement =
    let inlineStyle (styles:CSSProp list) = div [Style (Display DisplayOptions.Inline :: styles)]
    let start = TimeHelpers.getTimeMs ()
    let selectedRams = Map.toList wsModel.SelectedRams
    if List.length selectedRams > 0 then
        let tables = 
            let headerRow =
                ["read", RAMRead; "overwritten",RAMWritten]
                |> List.map (fun (op, opStyle) -> inlineStyle [Margin "0px"] [inlineStyle (ramTableRowStyle  opStyle) [str op]])
                |> function 
                    | [a;b] -> [str "Key: Memory location is " ; a; str ", or " ;b; str ". Click waveforms or use cursor control to change current cycle."] 
                    | _ -> failwithf "What? Can't happen!"
            List.map (fun ram -> td [Style [BorderColor "white"]] [ramTable wsModel ram])  selectedRams
            |> (fun tables -> [tbody [] [tr [] [th [ColSpan selectedRams.Length] [inlineStyle [] headerRow]]; tr [Style [Border "10px"]] tables]])
            |> Fulma.Table.table [
                Table.TableOption.Props ramTablesLevelProps;
                Table.IsFullWidth;
                Table.IsBordered;
                ]
        div [HTMLAttr.Id "TablesDiv"] [ hr [ Style [ Margin "5px"]]; br [ Style [ Margin "0px"]]; tables]
    else div [] []
    |> TimeHelpers.instrumentInterval "ramTables" start


