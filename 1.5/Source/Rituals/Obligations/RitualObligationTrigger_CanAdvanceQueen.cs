using RimWorld;

using System.Collections.Generic;
using System.Linq;

using Verse;


namespace Xenomorphtype
{
    internal class RitualObligationTrigger_CanAdvanceQueen : RitualObligationTrigger
    {
        bool unNotified = true;
        int nextCheckTick = 0;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref unNotified, "unNotified", true);
            Scribe_Values.Look(ref nextCheckTick, "nextCheckTick", 0);
        }

        public override void Tick()
        {
            base.Tick();
            int tick = Find.TickManager.TicksGame;
            if (tick > nextCheckTick)
            {
                Faction playerFaction = Find.FactionManager.OfPlayer;
                nextCheckTick = tick + 2500;
                if (!playerFaction.ideos.Has(ritual.ideo))
                {
                    return;
                }

                if (!playerFaction.ideos.IsPrimary(ritual.ideo))
                {
                    return;
                }

                if (!XMTUtility.NoQueenPresent())
                {
                    Map map = Find.CurrentMap;
                    if(map == null)
                    {
                        return;
                    }
                    bool found = false;
                    FillableChrysalis chosen = null;
                    IEnumerable<FillableChrysalis> candidates = map.listerBuildings.AllBuildingsColonistOfClass<FillableChrysalis>();
                    if (!candidates.Any())
                    {
                        return;
                    }
                    foreach (FillableChrysalis candidate in candidates)
                    {
                        if (candidate.Filled)
                        {
                            chosen = candidate;
                            found = true;
                            break;
                        }
                    }
                    
                    if ((ritual.activeObligations == null || ritual.activeObligations.Count == 0) && found)
                    {
                        if (map != null)
                        {
                            ritual.AddObligation(new RitualObligation(ritual));
                            string text = "A metamorphic Chrysalis is ready!";

                            text = text + "\n\n" + "The Chrysalis is filled, advance the queen.";
                            Find.LetterStack.ReceiveLetter("Chrysalis Ready!", text, LetterDefOf.RitualOutcomePositive, new LookTargets(chosen));
                            
                        }
                    }
                }
                else
                {
                    if(XMTSettings.LogRituals)
                    {
                        Log.Message("No Queen Found For Advancement Ritual");
                    }
                }
            }
        }
    }

    public class RitualObligationTrigger_CanAdvanceQueenProperties : RitualObligationTriggerProperties
    {
        public RitualObligationTrigger_CanAdvanceQueenProperties()
        {
            triggerClass = typeof(RitualObligationTrigger_CanAdvanceQueen);
        }
    }
}
