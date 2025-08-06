using RimWorld;
using Verse;

namespace MapDrill
{
    public class CompProperties_MapDrill : CompProperties
    {

        public float drillAutoModeYield;
        public float drillAutoModeSpeed;
        public SoundDef soundDefSustainer;
        public EffecterDef effecterDefForAutoMode;
        public float slagChunkEveryNumSteelConsumed;

        public CompProperties_MapDrill()
        {
            compClass = typeof(CompDrill_MapDrill);
        }
    }

}
