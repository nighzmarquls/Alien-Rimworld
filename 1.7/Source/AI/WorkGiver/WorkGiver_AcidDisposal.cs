using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{

    // Mature morphs enter through nest tending, never the human work scanner.
    public class WorkGiver_AcidDisposal : WorkGiver_Haul
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false) => pawn?.GetComp<CompMatureMorph>() != null || !AcidDisposalUtility.AnyReadyPit(pawn);
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForUndefined();

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) => ShouldSkip(pawn) ? System.Array.Empty<Thing>() : AcidDisposalUtility.HaulCandidates(pawn);
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => pawn?.GetComp<CompMatureMorph>() == null && AcidDisposalUtility.TryFindPitFor(pawn, t, forced, out _);
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) => pawn?.GetComp<CompMatureMorph>() == null && AcidDisposalUtility.TryFindPitFor(pawn, t, forced, out CompAcidPit pit) ? AcidDisposalUtility.MakeJob(pawn, t, pit) : null;
    }

}
