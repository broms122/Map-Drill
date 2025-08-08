using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit; // For OpCodes

namespace MapDrill
{


        [HarmonyPatch(typeof(CompDeepScanner), "DoFind")]
        public static class TargetMethod_Patch
        {
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codeInstructions)
            {
            var newInstructions = new List<CodeInstruction>(codeInstructions);
            var field = AccessTools.Field(typeof(ThingDef), "deepCountPerCell");

            for (int i = 0; i < newInstructions.Count; i++)
                {

                    if (i>0 && newInstructions[i-1].opcode == OpCodes.Ldloc_2 && newInstructions[i].LoadsField(field)) //OpCodes.Stloc_2
                {

                    MethodInfo myFunc1 = AccessTools.Method(typeof(TargetMethod_Patch), "MyStaticFunction");
                    MethodInfo myFunc3 = AccessTools.Method(typeof(TargetMethod_Patch), "MyStaticFunction3");


                    newInstructions.Insert(i++, new CodeInstruction(OpCodes.Ldloc_2)); // ThingDef
                    newInstructions.Insert(i++, new CodeInstruction(OpCodes.Ldloc_0)); // Comp for Map
                    newInstructions.Insert(i++, new CodeInstruction(OpCodes.Ldloc_S,6)); // intvec3 for cell
                    newInstructions.Insert(i++, new CodeInstruction(OpCodes.Call, myFunc3));

                    break; // Only insert once
                    }
            }
            return newInstructions;
        }

        public static void MyStaticFunction()
        {
            Log.Message("@@@@@@@WHOA IT WORKED@@@@@@@@@@");
            return;
        }

        public static void MyStaticFunction3(ThingDef thingdef, CompDeepScanner comp, IntVec3 intvec3)
        {
            Map map = comp.parent.Map;
            CompDrill_MapDrill.UpdateMapResourceDictionaries(thingdef, map, intvec3);

            //Log.Message($"thingdef found ---- {thingdef.defName}");
            //Log.Message($"map found ---- {map}");
            //Log.Message($"IntVec3 found ---- x:\t{intvec3.x}\t|\tz:\t{intvec3.z}");
            return;
        }
    }
}
