
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class WorkGiver_ExtractAcid: WorkGiver_XMTContainedPawn
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForUndefined();

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return pawn?.Map == null || XenoGeneDefOf.XMT_Acid_Utilization?.IsFinished != true;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (ShouldSkip(pawn))
            {
                yield break;
            }
            foreach (Thing holder in base.PotentialWorkThingsGlobal(pawn))
            {
                yield return holder;
            }
            foreach (CompAcidPit pit in AcidDisposalUtility.PitsOnMap(pawn.Map))
            {
                if (pit.parent.Faction == pawn.Faction && pit.AllowAcidExtraction && pit.HarvestReady)
                {
                    yield return pit.parent;
                }
            }
        }

        private bool ValidTarget(Pawn pawn, Thing target, bool forced)
        {
            if (target?.TryGetComp<CompAcidPit>() is CompAcidPit pit)
            {
                return pit.parent.Faction == pawn.Faction && pit.HarvestReady && (forced || pit.AllowAcidExtraction);
            }
            return GetEntity(target) != null;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (XenoGeneDefOf.XMT_Acid_Utilization?.IsFinished != true || !ValidTarget(pawn, t, forced)
                || t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (XenoGeneDefOf.XMT_Acid_Utilization?.IsFinished != true || !ValidTarget(pawn, t, forced))
            {
                return null;
            }

            return JobMaker.MakeJob(XenoWorkDefOf.XMT_ExtractAcid, t);
        }

        protected override Pawn GetEntity(Thing potentialPlatform)
        {
            Pawn heldPawn = XMTContainmentUtility.HeldPawn(potentialPlatform);
            if (heldPawn != null)
            {
                if (!XMTUtility.IsXenomorph(heldPawn))
                {
                    return null;
                }

                if (heldPawn.Info()?.extractAcid != true)
                {
                    return null;
                }

                if(!BioUtility.PawnHasEnoughForExtraction(heldPawn, false))
                {
                    return null;
                }
                
                return heldPawn;
            }

            return null;
        }
    }
}
