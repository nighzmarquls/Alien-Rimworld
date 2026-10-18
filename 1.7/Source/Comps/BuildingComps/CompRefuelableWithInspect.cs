using RimWorld;

namespace Xenomorphtype
{
    public class CompProperties_RefuelableWithInspect : CompProperties_Refuelable
    {
        public bool showFuelInspect = true;

        public CompProperties_RefuelableWithInspect()
        {
            compClass = typeof(CompRefuelableWithInspect);
        }
    }

    public class CompRefuelableWithInspect : CompRefuelable
    {
        public override string CompInspectStringExtra()
        {
            return ((CompProperties_RefuelableWithInspect)props).showFuelInspect
                ? base.CompInspectStringExtra() : null;
        }
    }
}
