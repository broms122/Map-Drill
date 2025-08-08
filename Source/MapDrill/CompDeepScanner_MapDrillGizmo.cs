using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MapDrill
{
    [StaticConstructorOnStartup]
    public class CompDeepScanner_MapDrillGizmo : ThingComp
    {

        //private static SoundDef Designate_Cancel = DefDatabase<SoundDef>.GetNamed("Designate_Cancel");//Building_Deconstructed wasn't bad, neither was Crunch
        public static readonly Texture2D GizmoShowMapIcon = ContentFinder<Texture2D>.Get("UI/Commands/ShowMap");

        public override IEnumerable<Gizmo> CompGetGizmosExtra()//leave as is
        {

            foreach (Gizmo item in base.CompGetGizmosExtra())
            {
                yield return item;
            }


            if (DebugSettings.ShowDevGizmos)
            {
                Command_Action command_ActionGizmo = new Command_Action();
                command_ActionGizmo.defaultLabel = "command_resetAllDictionary".Translate(); // "Scan Map"
                command_ActionGizmo.defaultDesc = "command_resetAllDictionaryDesc".Translate(); // "Scan Map for existing underground resource nodes from a ground-penetrating scanner."
                command_ActionGizmo.icon = GizmoShowMapIcon;
                //command_ActionGizmo.iconAngle = mineableThing.uiIconAngle;
                //command_ActionGizmo.iconOffset = mineableThing.uiIconOffset;
                command_ActionGizmo.action = delegate
                {
                    CompDrill_MapDrill comp = new CompDrill_MapDrill();
                    comp.ClearResourceDictionaries();
                    SoundDefOf.Designate_Cancel.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
                };
                yield return command_ActionGizmo;
            }
        }



    }
}
