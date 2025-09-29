using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class StarbeastCorpse : Corpse
    {

        public override IEnumerable<Thing> ButcherProducts(Pawn butcher, float efficiency)
        {
            Log.Message(butcher + " butcher a starbeast corpse");
            foreach (Thing item in InnerPawn.ButcherProducts(butcher, efficiency))
            {
                if (InnerPawn.ageTracker.Adult)
                {
                    yield return item;
                }
                else
                {
                    if(item.def != InnerPawn.RaceProps.leatherDef)
                    {
                        yield return item;
                    }
                    else
                    {
                        item.Discard();
                    }
                }
            }

            if (InnerPawn.health != null)
            {
                if(InnerPawn.health.hediffSet.TryGetHediff(XenoGeneDefOf.XMT_ThrumboHorn, out Hediff horn))
                {
                    yield return ThingMaker.MakeThing(ExternalDefOf.ThrumboHorn);
                }
            }

            FilthMaker.TryMakeFilth(butcher.Position, butcher.Map, InternalDefOf.Starbeast_Filth_Resin, InnerPawn.LabelIndefinite());
            
            if (InnerPawn.RaceProps.Humanlike)
            {
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.ButcheredHuman, new SignalArgs(butcher.Named(HistoryEventArgsNames.Doer), InnerPawn.Named(HistoryEventArgsNames.Victim))));
                TaleRecorder.RecordTale(TaleDefOf.ButcheredHumanlikeCorpse, butcher);
            }

            ResearchUtility.ProgressCryptobioTech(10, butcher);
        }
    }
}
