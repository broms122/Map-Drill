using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using static HarmonyLib.Code;

namespace MapDrill
{
    public class JobDriver_OperateMapDrill : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            this.FailOnThingHavingDesignation(TargetIndex.A, DesignationDefOf.Uninstall);
            this.FailOn(() => !job.targetA.Thing.TryGetComp<CompDrill_MapDrill>().CanDrillNow());   //here is what needs to be updated
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil work = ToilMaker.MakeToil("MakeNewToils");

            work.tickIntervalAction = delegate (int delta)
            {
                Pawn actor = work.actor;
                ((Building)actor.CurJob.targetA.Thing).GetComp<CompRefuelable_MapDrill>();

                var drill = DefDatabase<ThingDef>.GetNamed("MapDrill");
                CompProperties_Refuelable_MapDrill comp = drill.GetCompProperties<CompProperties_Refuelable_MapDrill>();
                var amount = comp.fuelConsumptionRate / 60000f;


                ((Building)actor.CurJob.targetA.Thing).GetComp<CompDrill_MapDrill>().RunDrill(delta, actor); //here is what needs to be updated
                ((Building)actor.CurJob.targetA.Thing).GetComp<CompRefuelable_MapDrill>().ConsumeFuel(amount);
                actor.skills?.Learn(SkillDefOf.Mining, 0.065f * (float)delta);
            };
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithEffect(EffecterDefOf.Drill, TargetIndex.A);
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            work.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            work.activeSkill = () => SkillDefOf.Mining;


            //Toil work2 = ToilMaker.MakeToil("MakeNewToils");







            yield return work;



        }
    }

}
