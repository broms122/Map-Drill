using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace MapDrill
{
    public class WorkGiver_Refuel_MapDrill : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(ThingDefOf.MapDrill);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public virtual JobDef JobStandard => JobDefOf.Refuel_MapDrill;

        public virtual JobDef JobAtomic => JobDefOf.Refuel_MapDrillAtomic;

        public virtual bool CanRefuelThing(Thing t)
        {
            return !(t is Building_Turret);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (CanRefuelThing(t))
            {
                return RefuelWorkGiverUtility_MapDrill.CanRefuel(pawn, t, forced);
            }
            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return RefuelWorkGiverUtility_MapDrill.RefuelJob(pawn, t, forced, JobStandard, JobAtomic);
        }
    }

}
