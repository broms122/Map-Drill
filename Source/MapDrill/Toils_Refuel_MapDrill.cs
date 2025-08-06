using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace MapDrill
{
    public class Toils_Refuel_MapDrill
    {
        public static Toil FinalizeRefueling(TargetIndex refuelableInd, TargetIndex fuelInd)
        {
            Toil toil = ToilMaker.MakeToil("FinalizeRefueling");
            toil.initAction = delegate
            {
                Job curJob = toil.actor.CurJob;
                Thing thing = curJob.GetTarget(refuelableInd).Thing;
                if (toil.actor.CurJob.placedThings.NullOrEmpty())
                {
                    thing.TryGetComp<CompRefuelable_MapDrill>().Refuel(new List<Thing> { curJob.GetTarget(fuelInd).Thing });
                }
                else
                {
                    thing.TryGetComp<CompRefuelable_MapDrill>().Refuel(toil.actor.CurJob.placedThings.Select((ThingCountClass p) => p.thing).ToList());
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }
    }
}
