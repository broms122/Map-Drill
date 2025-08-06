using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MapDrill
{
    /*
    [HarmonyPatch(typeof(CompDeepScanner), "DoFind")] // Targeting the DoFind method of CompDeepScanner
    public static class CompDeepScanner_DoFind_Replacement_Patch
    {
        public static bool Prefix(CompDeepScanner __instance, Pawn worker) // Prefix method matching DoFind's signature
        {
            Map map = __instance.parent.Map;
            if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(10, (IntVec3 x) => CanScatterAt(x, map), map, out var result))
            {
                Log.Error("Could not find a center cell for deep scanning lump generation!");
            }
            ThingDef thingDef = ChooseLumpThingDef();
            int numCells = Mathf.CeilToInt(thingDef.deepLumpSizeRange.RandomInRange);
            foreach (IntVec3 item in GridShapeMaker.IrregularLump(result, map, numCells))
            {
                if (CanScatterAt(item, map) && !item.InNoBuildEdgeArea(map))
                {
                    map.deepResourceGrid.SetAt(item, thingDef, thingDef.deepCountPerCell);
                }
            }
            string key = ("LetterDeepScannerFoundLump".CanTranslate() ? "LetterDeepScannerFoundLump" : ((!"DeepScannerFoundLump".CanTranslate()) ? "LetterDeepScannerFoundLump" : "DeepScannerFoundLump"));
            Find.LetterStack.ReceiveLetter("LetterLabelDeepScannerFoundLump".Translate() + ": " + thingDef.LabelCap, key.Translate(thingDef.label, worker.Named("FINDER")), LetterDefOf.PositiveEvent, new LookTargets(result, map));

            return false; // This is crucial: It prevents the original DoFind method from executing.
        }
    }*/
}