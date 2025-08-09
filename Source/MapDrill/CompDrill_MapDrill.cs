using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Unity.Collections;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions.Must;
using Verse;
using Verse.AI;
using Verse.Noise;
using Verse.Sound;
using static HarmonyLib.Code;

namespace MapDrill
{
    [StaticConstructorOnStartup]
    public class CompDrill_MapDrill : ThingComp
    {
        /* todo: 
         * map wide range                       -done
         * consume steel when in use.           -done
         * gizmo selection                      -done
         * make random chance for slag to spawn during drill ticks (expose chance to xml). -tbd
         * make it work unmanned by at a slower rate
         * progress bar like a solar panel?
        */
        public static readonly Texture2D DebugShowVariablesIcon = ContentFinder<Texture2D>.Get("UI/Designators/Claim");
        public static readonly Texture2D GizmoShowMapIcon = ContentFinder<Texture2D>.Get("UI/Commands/ShowMap"); 
        public static readonly Texture2D ChunkSlagSteel = ContentFinder<Texture2D>.Get("Things/Item/Chunk/ChunkSlag/MetalDebrisA");
        public static readonly Texture2D ResetPipeIcon = ContentFinder<Texture2D>.Get("UI/Commands/TempReset"); //PodEject



        public CompProperties_MapDrill Props => (CompProperties_MapDrill)props;
        public CompProperties_Refuelable_MapDrill Props2 => (CompProperties_Refuelable_MapDrill)props;

        private static ThingDef MapDrillDef = DefDatabase<ThingDef>.GetNamed("MapDrill");
        private static ThingDef chunkSlagSteelDef = DefDatabase<ThingDef>.GetNamed("ChunkSlagSteel");
        private static SoundDef chunkSlagSound = DefDatabase<SoundDef>.GetNamed("Standard_Drop");//Building_Deconstructed wasn't bad, neither was Crunch

        private IntVec3 lastCell = new IntVec3(0, 0, 0);
        //public static int targetResourceRemaining = 0; // no longer used - delete
        private ThingDef gizmoTargetResource; // list publically available in case of multiple map drills
        private ThingDef TargetDrillingDef; // local to eachdrill 
        private IntVec3 TargetDrillingCell; // local to eachdrill 
        private IntVec3 activeDrillCell; // local to eachdrill 
        private int TargetDrillingCountRemaining; // local to eachdrill
        private int distanceCellsDug = 0;
        private float distanceToCell; // distance between drill and targetCell
        private int distanceToDig => (int)((distanceToCell - distanceCellsDug < 0) ? 0 : distanceToCell - distanceCellsDug);   //  distance remaining to dig to get to actually drill for 
        private bool drillAutoMode = true; // false = manual , true = automatic
        private float steelConsumed;
        private bool isPawnWorking;
        public Pawn driller;
        public WorkGiverDef mapDrillWorkGiverDef = DefDatabase<WorkGiverDef>.GetNamed("MapDrill");
        private Effecter effecter;
        private int tickCount;
        private int localMapDictionaryCount = 0;
        private bool canDrill = true;

        //public NativeArray Result;


        public static Dictionary<Map, Dictionary<IntVec3, ThingDef>> OuterDictionaryMapIntVec3 = new Dictionary<Map, Dictionary<IntVec3, ThingDef>>();

        private Dictionary<IntVec3, ThingDef> InnerDictionaryIntVec3ThingDef = new Dictionary<IntVec3, ThingDef>();
        private Dictionary<IntVec3, int> InnerDictionaryIntVec3Int = new Dictionary<IntVec3, int>();


        private Dictionary<int, string> drillStatusDictionary = new Dictionary<int, string>()
        {
            {1, "MapDrillStatusActiveManned" },
            {2, "MapDrillStatusActiveUnmanned" }, // to be implemented
            {3, "MapDrillStatusActiveStandby" },
            {4, "MapDrillStatusActiveOffline" },
            {5, "MapDrillStatusActiveDigging" },
        };

        private CompPowerTrader powerComp;
        private CompRefuelable_MapDrill refuelableComp;
        private CompFlickable flickComp;

        private float portionProgress;
        private float portionYieldPct;

        private float digProgress = 0f;
        private int digOrDrill = 0;// 0 = dig mode, 1 = drill mode
        private int drillStatus; // 1 = Active (Drilling), 2 = tbd, 3 = Standy, 4 = Offline, 5 = Digging
        private float fuelRatePerTick;
        private float fuelRatePerHour;
        private float slagChunkEveryNumSteelConsumed;

        private Sustainer sustainer;

        private float ProgressToNextPortionPercent => portionProgress / 10000f;
        private float DigProgressToNextPortionPercent => digProgress / 10000f;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            powerComp = parent.TryGetComp<CompPowerTrader>();
            refuelableComp = parent.TryGetComp<CompRefuelable_MapDrill>();
            flickComp = parent.TryGetComp<CompFlickable>();
            /*distanceToDig = (int)Vector3.Distance(ConvertIntVec3ToVector3(TargetDrillingCell),ConvertIntVec3ToVector3(TargetDrillingCell));*/
            fuelRatePerTick = refuelableComp.ConsumptionRatePerTick;
            fuelRatePerHour = fuelRatePerTick * 2500;
            slagChunkEveryNumSteelConsumed = Props.slagChunkEveryNumSteelConsumed;
            //Log.Message($"fuelRatePerHour 1 = {fuelRatePerHour}");
        }//leave as is

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            SetDefaultTargetMineral();
            lastCell = (lastCell == IntVec3.Invalid ? parent.Position : lastCell);
            activeDrillCell = (activeDrillCell == IntVec3.Invalid ? parent.Position : activeDrillCell);
            //OuterDictionaryMapIntVec3 = (OuterDictionaryMapIntVec3 == null) ? new Dictionary<Map, Dictionary<IntVec3, ThingDef>>() : OuterDictionaryMapIntVec3;
        }//leave as is

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref portionProgress, "portionProgress", 0f);
            Scribe_Values.Look(ref portionYieldPct, "portionYieldPct", 0f);

            Scribe_Values.Look(ref lastCell, "lastCell");
            Scribe_Values.Look(ref distanceToCell, "distanceToCell", 0f);
            //Scribe_Values.Look(ref distanceToDig, "distanceToDig", 0);
            Scribe_Values.Look(ref distanceCellsDug, "distanceCellsDug", 0);
            Scribe_Values.Look(ref digProgress, "digProgress", 0f);
            Scribe_Values.Look(ref drillStatus, "drillStatus", 0);
            Scribe_Values.Look(ref digOrDrill, "digOrDrill", 0);
            Scribe_Values.Look(ref drillAutoMode, "drillAutoMode", true);
            Scribe_Values.Look(ref steelConsumed, "steelConsumed", 0);
            Scribe_Values.Look(ref isPawnWorking, "isPawnWorking", true);
            Scribe_Values.Look(ref tickCount, "tickCount", 0);
            Scribe_Values.Look(ref localMapDictionaryCount, "localMapDictionaryCount", 0);
            Scribe_Values.Look(ref canDrill, "canDrill", true); 


            Scribe_Defs.Look(ref TargetDrillingDef, "TargetDrillingDef");
            Scribe_Values.Look(ref TargetDrillingCell, "TargetDrillingCell", parent.Position);
            Scribe_Values.Look(ref TargetDrillingCountRemaining, "TargetDrillingCountRemaining");
            Scribe_Defs.Look(ref gizmoTargetResource, "gizmoTargetResource");

            Scribe_Collections.Look(ref OuterDictionaryMapIntVec3, "OuterDictionaryMapIntVec3",             LookMode.Reference, LookMode.Value);
            Scribe_Collections.Look(ref InnerDictionaryIntVec3ThingDef, "InnerDictionaryIntVec3ThingDef",   LookMode.Value, LookMode.Def);
            Scribe_Collections.Look(ref InnerDictionaryIntVec3Int, "InnerDictionaryIntVec3Int",             LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && gizmoTargetResource == null)
            {
                SetDefaultTargetMineral();
            }
        }

        private void ShowAllVariables()
        {
            Log.Message($"distanceToCell\t\t= {distanceToCell}");
            Log.Message($"distanceToDig\t\t= {distanceToDig}");
            Log.Message($"distanceCellsDug\t\t= {distanceCellsDug}");
            Log.Message($"lastCell\t\t= {lastCell}");
            Log.Message($"gizmoTargetResource\t\t= {gizmoTargetResource}");
            Log.Message($"TargetDrillingDef\t\t= {TargetDrillingDef}");
            Log.Message($"TargetDrillingCell\t\t= {TargetDrillingCell}");
            Log.Message($"activeDrillCell\t\t= {activeDrillCell}");
            Log.Message($"TargetDrillingCountRemaining\t\t= {TargetDrillingCountRemaining}");
            Log.Message($"drillStatus\t\t= {drillStatus}");
            Log.Message($"portionProgress\t\t= {portionProgress}");
            Log.Message($"portionYieldPct\t\t= {portionYieldPct}");
            Log.Message($"digProgress\t\t= {digProgress}");
            Log.Message($"digOrDrill\t\t= {digOrDrill}");
            Log.Message($"drillAutoMode\t\t= {drillAutoMode}");
            Log.Message($"steelConsumed\t\t= {steelConsumed}");
            Log.Message($"isPawnWorking\t\t= {isPawnWorking}");
            Log.Message($"tickCount\t\t= {tickCount}");
            Log.Message($"localMapDictionaryCount\t\t= {localMapDictionaryCount}");
            Log.Message($"canDrill\t\t= {canDrill}");
            Log.Message("----------------------------------------");
            DisplayDeepResourceDictionary();
        }

        private void SetDefaultTargetMineral()
        {
            gizmoTargetResource = ThingDefOf.MineableGold;
        }

        public static Vector3 ConvertIntVec3ToVector3(IntVec3 intVec)//leave as is
        {
            return new Vector3(intVec.x, intVec.y, intVec.z);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)//leave as is
        {
            portionProgress = 0f;
            portionYieldPct = 0f;
            drillStatus = 4;
            digOrDrill = 0;
            distanceCellsDug = 0;
            lastCell = parent.Position;
            TargetDrillingCell = parent.Position;
            activeDrillCell = parent.Position;
            InnerDictionaryIntVec3Int.Clear();
            InnerDictionaryIntVec3ThingDef.Clear();
            ShutDownEffects();
        }

        public void ClearResourceDictionaries()
        {
            OuterDictionaryMapIntVec3.Clear();
            InnerDictionaryIntVec3Int.Clear();
            InnerDictionaryIntVec3ThingDef.Clear();

            //TODO update this to interate through all instances of InnerDictionaries
            Log.Message("~~~~~~~~\t\tAll dictionaries cleared.\t\t~~~~~~~~");
        }

        public void ScanCurrentMapForUndergroundResources()// Finds location of desired resources manually via gizmo
        {

            if (OuterDictionaryMapIntVec3.ContainsKey(parent.Map))
            {
                OuterDictionaryMapIntVec3[parent.Map].Clear();
            }

            DeepResourceGrid deepResourceGrid = parent.Map.deepResourceGrid;

            //Log.Message(parent.Map.info.Size); // returns 250,1,250... scan x 0 to 250, and z 0 to 250

            int mapX = parent.Map.info.Size.x;
            int mapY = 0;
            int mapZ = parent.Map.info.Size.z;

            //Log.Message($"mapX = {mapX}");
            //Log.Message($"mapZ = {mapZ}");


            //Log.Message("GIZMO - Creating Dictonary");

            if (!OuterDictionaryMapIntVec3.ContainsKey(parent.Map))
            {
                OuterDictionaryMapIntVec3.Add(parent.Map, new Dictionary<IntVec3, ThingDef>());
            }

            for (int i = 0; i < mapX; i++)
                {
                    for (int j = 0; j < mapZ; j++)
                    {
                        IntVec3 temp = new IntVec3(i, mapY, j);
                        if ((deepResourceGrid.ThingDefAt(temp) == gizmoTargetResource.building.mineableThing))
                        {

                        OuterDictionaryMapIntVec3[parent.Map].Add(temp, gizmoTargetResource.building.mineableThing);
                            //Log.Message($"deepResourceGrid.ThingDefAt(temp) = {deepResourceGrid.ThingDefAt(temp)} @ ({i},1,{j})");
                        }
                    }
                }

            //Log.Message("GIZMO - Dictonary Complete");

            canDrill = false;
            //UpdateInstanceTargets();  commented out 8/7/25 .. this needs to be automated later
        }

        public static void UpdateMapResourceDictionaries(ThingDef thingdef, Map map, IntVec3 intvec3)
        {
            //Log.Message($"map = {map}");

            //Map map = comp.parent.Map;

            Log.Message($"Map:\t{map}\t\tIntVec3\tx:\t{intvec3.x}\ty:\t{intvec3.y}\tz:\t{intvec3.z}\t\tThingDef:\t{thingdef}");

            if (!OuterDictionaryMapIntVec3.ContainsKey(map))
            {
                OuterDictionaryMapIntVec3.Add(map, new Dictionary<IntVec3, ThingDef>());
            }

            OuterDictionaryMapIntVec3[map].Add(intvec3, thingdef);
        }

        private void CreateLocalDictionaries()
        {
            InnerDictionaryIntVec3Int.Clear();
            InnerDictionaryIntVec3ThingDef.Clear();
            SortedDictionary<IntVec3, int> tempSortedDictionary = new SortedDictionary<IntVec3, int>();

            //Log.Message($"!!!!!!!");
            foreach (var intVec3ThingDefdictionary in OuterDictionaryMapIntVec3[parent.Map])
            {
                //Log.Message($"######");
                if (intVec3ThingDefdictionary.Value == gizmoTargetResource.building.mineableThing)
                {

                    //Log.Message($"@@@@@@");
                    InnerDictionaryIntVec3ThingDef.Add(intVec3ThingDefdictionary.Key, intVec3ThingDefdictionary.Value);
                    InnerDictionaryIntVec3Int.Add(intVec3ThingDefdictionary.Key, Math.Abs((int)Vector3.Distance(ConvertIntVec3ToVector3(parent.Position), ConvertIntVec3ToVector3(intVec3ThingDefdictionary.Key))));
                    //tempSortedDictionary.Add(intVec3ThingDefdictionary.Key, Math.Abs((int)Vector3.Distance(ConvertIntVec3ToVector3(parent.Position), ConvertIntVec3ToVector3(intVec3ThingDefdictionary.Key))));
                }
                else
                {
                    Log.Message($"{intVec3ThingDefdictionary.Value} is a thingdef mismatch, not added to dictionary");
                }
            }
            InnerDictionaryIntVec3Int.OrderBy(x => x.Value);
/*            foreach (var x  in InnerDictionaryIntVec3Int)
            {
                Log.Message($"int = {x.Value}");    
            }*/
            //InnerDictionaryIntVec3Int = tempSortedDictionary.ToDictionary(x => x.Key, x => x.Value);
            //Log.Message("Created local dictionaries");
        }

        private void ShutDownDrill()
        {
            // 1 = Active (Drilling), 2 = tbd, 3 = Standby, 4 = Offline, 5 = Digging
            ShutDownEffects();

            if (!powerComp.PowerOn || !flickComp.SwitchIsOn || !refuelableComp.HasFuel) // turn it off
            {
                drillStatus = 4;
                return;
            }

            if (localMapDictionaryCount == 0) // powered but idle conditions
            {
                drillStatus = 3;
                return;
            }

        }
        
        private void UpdateLastCell(bool done = false)
        {
            if (lastCell == IntVec3.Zero)
            {
                lastCell = parent.Position;
            }
            if (done)
            {
                lastCell = TargetDrillingCell;
            }
        }

        private void RemoveDeepResourceFromDictionary(IntVec3 cellToRemove)
        {
            if (parent.Map.deepResourceGrid.CountAt(TargetDrillingCell) > 0) 
            {
                Log.Message("No need to remove cell from dicionary - RemoveDeepResourceFromDictionary");
                return; 
            }

            if (OuterDictionaryMapIntVec3[parent.Map].ContainsKey(cellToRemove))
            {
                OuterDictionaryMapIntVec3[parent.Map].Remove(cellToRemove);
                distanceCellsDug = 0;
            }

            if (InnerDictionaryIntVec3ThingDef.ContainsKey(cellToRemove))
            {
                InnerDictionaryIntVec3ThingDef.Remove(cellToRemove);
            }

            if (InnerDictionaryIntVec3Int.ContainsKey(cellToRemove))
            {
                InnerDictionaryIntVec3Int.Remove(cellToRemove);
            }

            //else { Log.Message("Somehow you tried to remove a cell that shouldn't exist @@@@@@@@@@@@ - RemoveDeepResourceFromDictionary"); }
        }

        private void DisplayDeepResourceDictionary()
        {
            int i1 = 0;
            int i2 = 0;
            int i3 = 0;
            foreach (var item in OuterDictionaryMapIntVec3)
            {
                Log.Message($"Outer {i1}:\t\tMap: {item.Key}\tDictionary: {item.Value}");
                int i1_2 = 0;
                foreach(var item2 in item.Value)
                {
                    Log.Message($"\t{i1}-{i1_2}:\t\tMap: {item2.Key}\tDictionary: {item2.Value}");
                    i1_2++;
                }
                i1++;
            }

            foreach (var item in InnerDictionaryIntVec3ThingDef)
            {
                Log.Message($"InnerDictionaryIntVec3ThingDef {i2}:\t\tIntVec3: {item.Key}\tDef: {item.Value}");
                i2++;
            }

            foreach (var item in InnerDictionaryIntVec3Int)
            {
                Log.Message($"InnerDictionaryIntVec3Int {i3}:\t\tIntVec3: {item.Key}\tInt: {item.Value}");
                i3++;
            }
        }

        private void UpdateTargetDrillingCountRemaining()
        {
            var temp = parent.Map.deepResourceGrid.CountAt(TargetDrillingCell);
            if (temp <= 0 || TargetDrillingCell == IntVec3.Zero)
            {
                TargetDrillingCountRemaining = 0;
                //Log.Message("DEBUG UpdateTargetDrillCountRemaining");
            }
            else
            {
                //Log.Message("DEBUG UpdateTargetDrillCountRemaining2");
                TargetDrillingCountRemaining = temp;
            }
        }

        private void TryPlaceMetalScrap()
        {
                Thing placeMetalScrapthing = ThingMaker.MakeThing(chunkSlagSteelDef);
                GenPlace.TryPlaceThing(placeMetalScrapthing, parent.InteractionCell, parent.Map, ThingPlaceMode.Near, null, (IntVec3 p) => p != parent.Position && p != parent.InteractionCell);
                chunkSlagSound.PlayOneShot(placeMetalScrapthing);
            return;
        }

        private void CheckIfSpawnSteelSlagChunk(int delta = 1)
        {
            steelConsumed += delta * fuelRatePerTick;
            //Log.Message($"steelconsumed = {steelConsumed}\tdelta = {delta}\t fuelRatePerTick = {fuelRatePerTick}");

            if (steelConsumed >= slagChunkEveryNumSteelConsumed)
            {
                TryPlaceMetalScrap();
                steelConsumed = (steelConsumed - slagChunkEveryNumSteelConsumed < 0) ? 0 : steelConsumed - slagChunkEveryNumSteelConsumed;
            }
        }

        public bool CanDrillNow() // this checks if we can even do any drilling

        {
            canDrill = true;  // remove this later probably

            /*
            { 1, "MapDrillStatusActiveManned" },
            { 2, "MapDrillStatusActiveUnmanned" }, // to be implemented
            { 3, "MapDrillStatusActiveStandby" },
            { 4, "MapDrillStatusActiveOffline" },
            { 5, "MapDrillStatusActiveDigging" },
            */

            if (!refuelableComp.HasFuel || !powerComp.PowerOn || !flickComp.SwitchIsOn) // could but won't run, shut it down
            {
                //Log.Message("DEBUG OPTION 1!! I added this 8/7/25 @ 5:08pm...");
                ShutDownDrill(); // results in offline (4) or standby (3) drillStatus
                return canDrill = false;
            }

            if (localMapDictionaryCount == 0 || !OuterDictionaryMapIntVec3.ContainsKey(parent.Map)) // nothing to drill. go home
            {
                ShutDownDrill();
                return canDrill = false;
            }

            if (OuterDictionaryMapIntVec3[parent.Map].ContainsValue(gizmoTargetResource.building.mineableThing) && !InnerDictionaryIntVec3ThingDef.ContainsValue(gizmoTargetResource.building.mineableThing)) // skip creation of dictionary if it isn't needed.
            {
                    CreateLocalDictionaries(); // makes local dictionaries targeting gizmoTargetResource from the outerdictionary
                return canDrill = false;
            }


            if (InnerDictionaryIntVec3ThingDef.Count == 0)
            {
                ShutDownDrill();
                return canDrill = false;
            }

            TargetDrillingCell = InnerDictionaryIntVec3Int.FirstOrDefault().Key;
            TargetDrillingDef = gizmoTargetResource.building.mineableThing;
            distanceToCell = InnerDictionaryIntVec3Int.FirstOrDefault().Value;
            UpdateLastCell();

            if (distanceToDig == 0) // can I dig or drill now?
            {
                digOrDrill = 1; // distance is 0, DRILL
                drillStatus = 1;
            }
            else
            {
                digOrDrill = 0; // distance is 0, get digging
                drillStatus = 5;
            }

            return canDrill = true;

        }

        public override void CompTickRare() 
        {
            if (OuterDictionaryMapIntVec3.ContainsKey(parent.Map))
            {
                localMapDictionaryCount = (OuterDictionaryMapIntVec3.ContainsKey(parent.Map)) ? OuterDictionaryMapIntVec3[parent.Map].Count : 0;
            } 

            if (!powerComp.PowerOn || !flickComp.SwitchIsOn)
            {
                ShutDownDrill(); // no power, shut it down
                return;
            }

            if (!CanDrillNow()) // checks for fuel, dictionary targets or in standby, if there are no resource remaining at target cell, remove from dictionary
            {
                ShutDownDrill();
                return;
            }

            isPawnWorking = IsPawnWorkingOnMapDrill();
            if (isPawnWorking)
            {
                if (sustainer != null && !sustainer.Ended)
                {
                    sustainer.End();
                }
                return;
            } 
            if (!isPawnWorking && powerComp.PowerOn && flickComp.SwitchIsOn && refuelableComp.HasFuel && (drillStatus == 3 || drillStatus == 4))
            {
                if (sustainer == null || sustainer.Ended)
                {
                    sustainer = Props.soundDefSustainer.TrySpawnSustainer(SoundInfo.InMap(parent));
                }
                sustainer.Maintain();
                this.parent.GetComp<CompRefuelable_MapDrill>().ConsumeFuel(fuelRatePerTick * 250);
                RunDrill(250);
            }
        }

        public override void CompTick()
        {
            tickCount++;
            if (tickCount >= 250)
            {
                CompTickRare();
                tickCount = 0;
                return;
            }



            if (localMapDictionaryCount == 0)
            {
                drillStatus = 3;
                ShutDownEffects();
                return;
            }

            if (canDrill == false)
            {
                ShutDownEffects();
                return;
            }

            if (!isPawnWorking && powerComp.PowerOn && flickComp.SwitchIsOn && refuelableComp.HasFuel && (drillStatus == 3 || drillStatus == 4))
            {
                //FleckMaker.ThrowDustPuff(this.parent.DrawPos, this.parent.Map, Rand.Range(0.8f, 1.2f));
                if (effecter == null)
                {
                    effecter = Props.effecterDefForAutoMode.SpawnAttached(this.parent, this.parent.Map);
                }
                effecter?.EffectTick(this.parent, this.parent);
            }
            else 
            { ShutDownEffects(); }
        }

        public void ShutDownEffects()
        {
            if (effecter != null) { effecter.Cleanup(); }
            if (sustainer != null) { sustainer.End(); }
        }

        public bool IsPawnWorkingOnMapDrill()
        {
            foreach (Pawn pawn in this.parent.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.IsColonist && pawn.jobs != null && pawn.jobs.curJob != null)
                {
                    Job currentJob = pawn.jobs.curJob;
                    if (currentJob.targetA.Thing == this.parent && currentJob.workGiverDef == mapDrillWorkGiverDef)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void RunDrill(int delta = 1, Pawn driller = null) //called by JobDriver_OperateMapDrill
        {
            if (!canDrill) { return; }
            if (gizmoTargetResource.building.mineableThing != TargetDrillingDef) { return; }

            /*
            if (driller == null)
            {
                drillAutoMode = true;
            }
            else
            {
                drillAutoMode = false;
            }*/

            float DrillSpeed;
            float DrillYield;
            float finalDrillSpeed;

            // pawn drill speed 1 at lvl 10 mining, delta = 1 if watching pawn, 15 is not watching... thinking autominer should be delta 60

            if (driller == null)
            {
                DrillSpeed = Props.drillAutoModeSpeed;
                DrillYield = Props.drillAutoModeYield;

            } 
            else
            {
                DrillSpeed = driller.GetStatValue(StatDefOf.DeepDrillingSpeed);
                DrillYield = driller.GetStatValue(StatDefOf.MiningYield);
            }

            finalDrillSpeed = DrillSpeed * (float)delta;
            //Log.Message($"DrillSpeed = {DrillSpeed}\tfinalDrillSpeed = {finalDrillSpeed}\t delta = {delta}\t DrillYield = {DrillYield}");

            var tempBool = (drillStatus == 3 || drillStatus == 5);

            CheckIfSpawnSteelSlagChunk(delta);

            if (digOrDrill == 0 && distanceToDig != 0 && tempBool) // need to dig to next cell
            {
                UpdateLastCell(true);
                drillStatus = 5;
                //Log.Message("@@@@@@@@@@@@@@@@@ DEBUG 2 @@@@@@@@@@@@@@@");
                digProgress += finalDrillSpeed;

                if (digProgress > 10000f)
                {
                    digProgress = 1f;
                    distanceCellsDug ++;
                }
            }

            if (distanceCellsDug == distanceToDig) 
            {
                drillStatus = 1;
                //Log.Message("@@@@@@@@@@@@@@@@@ DEBUG 3 RunDrill");
                digOrDrill = 1;
                digProgress = 1f;
            };

            if (digOrDrill == 1) // already dug to cell, begin the drilling
            {
                UpdateLastCell(true);
                digProgress = 1f;
                portionProgress += finalDrillSpeed;
                portionYieldPct += finalDrillSpeed * DrillYield / 10000f;

                if (portionProgress > 10000f)
                {
                    if (driller == null)
                    {
                        TryProducePortion(portionYieldPct);
                    }
                    else
                    {

                        TryProducePortion(portionYieldPct, driller);
                    }
                    portionProgress = 0f;
                    portionYieldPct = 0f;
                }
            }
        }
        
        private void TryProducePortion(float yieldPct, Pawn driller = null)
        {

            if (gizmoTargetResource.building.mineableThing != TargetDrillingDef) { return; }
            UpdateTargetDrillingCountRemaining();
            Log.Message("updated from TryProducePortion");

            if (TargetDrillingCountRemaining == 0) 
            {
                RemoveDeepResourceFromDictionary(TargetDrillingCell);
                Log.Message("no resources at cell - TryProductPortion");
                return;
            }
            int maxAmountInCell = Mathf.Min(TargetDrillingCountRemaining, TargetDrillingDef.deepCountPerPortion);

            parent.Map.deepResourceGrid.SetAt(TargetDrillingCell, TargetDrillingDef, TargetDrillingCountRemaining - maxAmountInCell);

            //UpdateTargetDrillingCountRemaining();

            int stackCount = Mathf.Max(1, GenMath.RoundRandom((float)maxAmountInCell * yieldPct));
            Thing thing = ThingMaker.MakeThing(TargetDrillingDef);
            thing.stackCount = stackCount;
            GenPlace.TryPlaceThing(thing, parent.InteractionCell, parent.Map, ThingPlaceMode.Near, null, (IntVec3 p) => p != parent.Position && p != parent.InteractionCell);
            if (driller != null)
            {
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.Mined, driller.Named(HistoryEventArgsNames.Doer)));
            }

            if (TargetDrillingCountRemaining - maxAmountInCell == 0)
            {
                digOrDrill = 0;
                digProgress = 0f;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()//leave as is
        {
             foreach (Gizmo item in base.CompGetGizmosExtra())
            {
                yield return item;
            }

            ThingDef mineableThing = gizmoTargetResource.building.mineableThing;
            Command_Action command_Action = new Command_Action();
            command_Action.defaultLabel = "CommandSelectMineralToScanFor".Translate() + ": " + mineableThing.LabelCap;
            command_Action.defaultDesc = "CommandSelectMineralToScanForDesc".Translate();
            command_Action.icon = mineableThing.uiIcon;
            command_Action.iconAngle = mineableThing.uiIconAngle;
            command_Action.iconOffset = mineableThing.uiIconOffset;
            command_Action.action = delegate
            {
                List<ThingDef> mineables = ((GenStep_PreciousLump)GenStepDefOf.PreciousLump.genStep).mineables;
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                foreach (ThingDef item3 in mineables)
                {
                    ThingDef localD = item3;
                    FloatMenuOption item = new FloatMenuOption(localD.building.mineableThing.LabelCap, delegate
                    {
                        foreach (object selectedObject in Find.Selector.SelectedObjects)
                        {
                            if (selectedObject is Thing thing)
                            {
                                CompDrill_MapDrill comp = thing.TryGetComp<CompDrill_MapDrill>();
                                if (comp != null)
                                {
                                    comp.gizmoTargetResource = localD;
                                }
                            }
                        }
                    }, MenuOptionPriority.Default, null, null, 29f, (Rect rect) => Widgets.InfoCardButton(rect.x + 5f, rect.y + (rect.height - 24f) / 2f, localD.building.mineableThing));
                    list.Add(item);
                }
                Find.WindowStack.Add(new FloatMenu(list));

                //CHANGETARGET
            };
            yield return command_Action;


            //WIP TESTING GIZMOS

            Command_Action command_ActionFindGizmo = new Command_Action();
            command_ActionFindGizmo.defaultLabel = "command_ActionFindGizmo".Translate(); // "Scan Map"
            command_ActionFindGizmo.defaultDesc = "command_ActionFindGizmoDesc".Translate(); // "Scan Map for existing underground resource nodes from a ground-penetrating scanner."
            command_ActionFindGizmo.icon = GizmoShowMapIcon;
            //command_ActionFindGizmo.iconAngle = mineableThing.uiIconAngle;
            //command_ActionFindGizmo.iconOffset = mineableThing.uiIconOffset;
            command_ActionFindGizmo.action = delegate
            {
                ScanCurrentMapForUndergroundResources();
                SoundDefOf.Designate_Claim.PlayOneShot(new TargetInfo(parent.Position, parent.Map));//Tick_High wasn't bad
            };
            yield return command_ActionFindGizmo;


            Command_Action command_resetPipelineGizmo = new Command_Action();
            command_resetPipelineGizmo.defaultLabel = "command_resetPipelineGizmo".Translate(); //"Reset Steel Pipe";
            command_resetPipelineGizmo.defaultDesc = "command_resetPipelineGizmoDesc".Translate(); //"This resets the "drilling from last location to the drill itself, useful if you need to start digging to something closer to the drill than the last place you drilled from.";
            command_resetPipelineGizmo.icon = ResetPipeIcon;
            command_resetPipelineGizmo.action = delegate
            {
                lastCell = parent.Position;
                canDrill = false;
                distanceToCell = (int)Vector3.Distance(ConvertIntVec3ToVector3(parent.Position), ConvertIntVec3ToVector3(TargetDrillingCell));
                distanceCellsDug = 0;
                SoundDefOf.Designate_Claim.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
            };
            yield return command_resetPipelineGizmo;




            //WIP TESTING GIZMOS


            if (DebugSettings.ShowDevGizmos)
            {


                Command_Action command_ActionDEV = new Command_Action();
                command_ActionDEV.defaultLabel = "DEV: Produce portion (100% yield)";
                command_ActionDEV.action = delegate
                {
                    TryProducePortion(1f);
                };
                yield return command_ActionDEV;

                Command_Action command_ActionDEV2 = new Command_Action();
                command_ActionDEV2.defaultLabel = "DEV: Attempt to make Scrap";
                command_ActionDEV2.icon = ChunkSlagSteel;
                command_ActionDEV2.action = delegate
                {
                    TryPlaceMetalScrap();
                };
                yield return command_ActionDEV2;

                Command_Action command_ActionFindGizmo4 = new Command_Action();
                command_ActionFindGizmo4.defaultLabel = "SHOW VARIABLES";
                command_ActionFindGizmo4.icon = DebugShowVariablesIcon;
                command_ActionFindGizmo4.action = delegate
                {
                    ShowAllVariables();
                };
                yield return command_ActionFindGizmo4;

                Command_Action command_ActionFindGizmo3 = new Command_Action();
                command_ActionFindGizmo3.defaultLabel = "DisplayDeepResourceDictionary";
                command_ActionFindGizmo3.icon = DebugShowVariablesIcon;
                command_ActionFindGizmo3.action = delegate
                {
                    DisplayDeepResourceDictionary();
                };
                yield return command_ActionFindGizmo3;
            }

        }

        public override string CompInspectStringExtra()// this needs to be rewritten
                                                       // conversion to make:
                                                       // targetCell -> TargetDrillingCell
                                                       // countPresent & targetResourceRemaining -> TargetDrillingCountRemaining
                                                       // resDef -> TargetDrillingDef
        /*
            { 1, "MapDrillStatusActiveManned" },
            { 2, "MapDrillStatusActiveUnmanned" }, // to be implemented
            { 3, "MapDrillStatusActiveStandby" },
            { 4, "MapDrillStatusActiveOffline" },
            { 5, "MapDrillStatusActiveDigging" },
        */
        {
            if (parent.Spawned)
            {
                //GetNextResource(out var resDef, out var countPresent, out targetCell);

                if (drillStatus == 1 || drillStatus == 5)
                {
                    
                    CompProperties_Refuelable_MapDrill comp = MapDrillDef.GetCompProperties<CompProperties_Refuelable_MapDrill>();
                    string PROPFuelLabel = comp.fuelLabel.Translate();

                    String tempDef;

                    if (TargetDrillingDef == null)
                    {
                        tempDef = "Pending";
                    } else 
                    {
                        tempDef = TargetDrillingDef.LabelCap;
                    }


                    return
                        drillStatusDictionary[drillStatus].Translate() + /*"\tDrillStatus (0/1 dig/drill) = " + digOrDrill + */"\n" +
                        "TargetMapDrillResource".Translate() + tempDef + "\t\t | " + "TargetMapDrillResourceRemaining".Translate() + TargetDrillingCountRemaining + "\n" +
                        "MapDrillProgress".Translate() + ProgressToNextPortionPercent.ToStringPercent("F0") + "\t\t | " + "MapDrillDigProgressString".Translate() + DigProgressToNextPortionPercent.ToStringPercent("F0") + "\n" +
                        "MapDrillDistanceString".Translate() + (int)distanceToCell + "\t | " + "MapDrillDistanceRemainingString".Translate() + (int)(distanceToDig) + "\n" +
                        PROPFuelLabel + "MapDrillSteelConsumptionRateString".Translate() + fuelRatePerHour;

                }
            }
            return null;
        }

        public static IntVec3 FindCellAtDistance(IntVec3 startCell, Map map, float targetDistance)
        {
            int searchRadius = (int)targetDistance + 2;

            for (int x = startCell.x - searchRadius; x <= startCell.x + searchRadius; x++)
            {
                for (int z = startCell.z - searchRadius; z <= startCell.z + searchRadius; z++)
                {
                    IntVec3 currentCell = new IntVec3(x, 0, z); 

                    if (currentCell.InBounds(map))
                    {
                        float currentDistance = currentCell.DistanceTo(startCell);

                        if (Mathf.Abs(currentDistance - targetDistance) < 0.1f) // Adjust epsilon as needed
                        {
                            return currentCell; 
                        }
                    }
                }
            }
            return IntVec3.Invalid;
        }

        public static IntVec3 FindCellAtDistanceBetweenTwoCells(IntVec3 startCell, IntVec3 endCell, float targetDistance)
        {
            float m = (endCell.y - startCell.y) / (endCell.x - startCell.x);
            //IntVec3 result = new IntVec3();


            if (m >= 0) // moving to right
            {
                for (int x = startCell.x; startCell.x < endCell.x; x++)
                {

                }
            }




            return IntVec3.Invalid;
        }

        public override void PostDrawExtraSelectionOverlays()
                                                             // this needs to be rewritten. called by JobDriver_OperateMapDrill
                                                             // conversion to make:
                                                             // targetCell -> TargetDrillingCell
                                                             // countPresent & targetResourceRemaining -> TargetDrillingCountRemaining
                                                             // resDef -> TargetDrillingDef
        {

            SimpleColor simpleColorRed = SimpleColor.Red;
            SimpleColor simpleColorBlue = SimpleColor.Blue;

            Vector3 lastCellPosition = lastCell.ToVector3Shifted();
            Vector3 targetPosition = TargetDrillingCell.ToVector3Shifted();
            Vector3 parentPosition = parent.Position.ToVector3Shifted();
            GenDraw.DrawCircleOutline(targetPosition, 1f, simpleColorRed);
            GenDraw.DrawLineBetween(targetPosition, parentPosition, simpleColorRed); // to do: harmony patch on GenDraw to make the parent.position in the center of the drill
            GenDraw.DrawLineBetween(targetPosition, lastCellPosition, simpleColorBlue); // to do: harmony patch on GenDraw to make the parent.position in the center of the drill

            TargetDrillingCell = (TargetDrillingCell == IntVec3.Invalid ? parent.Position : TargetDrillingCell);
            lastCell = (lastCell == IntVec3.Invalid ? parent.Position : lastCell);
            
            /*
            IntVec3 digSpot = FindCellAtDistance(parent.Position, parent.Map, distanceCellsDug);
            if (digSpot != IntVec3.Invalid)
            {
                //Log.Message($"lastCell = {lastCell}");
                //Log.Message($"distanceCellsDug = {distanceCellsDug}");
                GenDraw.DrawCircleOutline(digSpot.ToVector3Shifted(), 0.5f, simpleColorBlue);
            }*/

            //GenDraw.DrawCircleOutline(lastCellPosition, 0.5f, simpleColorBlue);
        }

    }
}
