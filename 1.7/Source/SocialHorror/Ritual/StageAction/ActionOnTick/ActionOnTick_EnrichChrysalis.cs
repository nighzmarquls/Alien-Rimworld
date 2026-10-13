

using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class ActionOnTick_EnrichChrysalis : ActionOnTick
    {
        public Pawn pawn;

        public int jellyCost = 75;

        public override void Apply(LordJob_Ritual ritual)
        {
            if (ritual.lord.ownedPawns.Contains(pawn) && pawn.carryTracker.CarriedCount(InternalDefOf.Starbeast_Jelly) >= jellyCost)
            {
                pawn.carryTracker.DestroyCarriedThing();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref jellyCost, "jellyCost", 0);
        }
    }
}
