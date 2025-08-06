using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MapDrill
{
    public class WorkGiver_EjectFuel_MapDrill : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Designation item in pawn.Map.designationManager.SpawnedDesignationsOfDef(RimWorld.DesignationDefOf.EjectFuel))
            {
                yield return item.target.Thing;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(RimWorld.DesignationDefOf.EjectFuel);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (pawn.Map.designationManager.DesignationOn(t, RimWorld.DesignationDefOf.EjectFuel) == null)
            {
                return false;
            }
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            if (!t.TryGetComp(out CompRefuelable_MapDrill comp))
            {
                return false;
            }
            AcceptanceReport acceptanceReport = comp.CanEjectFuel();
            if (!acceptanceReport.Accepted)
            {
                JobFailReason.Is(acceptanceReport.Reason);
                return false;
            }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(JobDefOf.EjectFuel_MapDrill, t);
        }
    }

}
