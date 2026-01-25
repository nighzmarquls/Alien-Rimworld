

using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Xenomorphtype
{
    public class IncidentWorker_QueenQuest : IncidentWorker_GiveQuest
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {

            QuestScriptDef questScriptDef = def.questScriptDef ?? parms.questScriptDef;
            if (questScriptDef != null && !questScriptDef.CanRun(parms.points, parms.target))
            {
                return false;
            }

            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoSuspended.Any();

        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            QuestScriptDef questScriptDef = def.questScriptDef ?? parms.questScriptDef ?? NaturalRandomQuestChooser.ChooseNaturalRandomQuest(parms.points, parms.target);
            if (questScriptDef == null)
            {
                return false;
            }

            parms.questScriptDef = questScriptDef;
            GiveQuest(parms, questScriptDef);
            return true;
        }

        
    }
}
