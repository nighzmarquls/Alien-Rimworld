
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class IncidentWorker_HuntingXenomorph : IncidentWorker
    {
        private const float PointsFactor = 1f;

        private const int AnimalsStayDurationMin = 60000;

        private const int AnimalsStayDurationMax = 120000;

        public override float ChanceFactorNow(IIncidentTarget target)
        {
            return XenoformingUtility.ChanceByXenoforming(base.ChanceFactorNow(target));
        }
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!XenoformingUtility.XenoformingMeets(10))
            {
                return false;
            }

            if(XMTUtility.QueenIsPlayer())
            {
                return false;
            }

            Map map = (Map)parms.target;

            if (map.skyManager.CurSkyGlow > 0.45f)
            {
                return false;
            }

            IntVec3 result;

            return RCellFinder.TryFindRandomPawnEntryCell(out result, map, CellFinder.EdgeRoadChance_Animal);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!XenoformingUtility.XenoformingMeets(10))
            {
                return false;
            }

            if (XMTSettings.LogWorld)
            {
                Log.Message("Executing Xenomorph Hunt Incident.");
            }

            Map map = (Map)parms.target;
            PawnKindDef animalKind = def.pawnKind;
            if (animalKind == null)
            {
                return false;
            }


            if (XMTSettings.LogWorld)
            {
                Log.Message("Pawnkind found hunting pack with " + animalKind);
            }

            IntVec3 result = parms.spawnCenter;
            if (!result.IsValid && !RCellFinder.TryFindRandomPawnEntryCell(out result, map, CellFinder.EdgeRoadChance_Animal))
            {
                return false;
            }

            if (XMTSettings.LogWorld)
            {
                Log.Message("Found position to spawn from");
            }

            List<Pawn> list = AggressiveAnimalIncidentUtility.GenerateAnimals(animalKind, map.Tile, parms.points * PointsFactor, parms.pawnCount);
            Rot4 rot = Rot4.FromAngleFlat((map.Center - result).AngleFlat);
            for (int i = 0; i < list.Count; i++)
            {
                Pawn pawn = list[i];
                IntVec3 loc = CellFinder.RandomClosewalkCellNear(result, map, 10);
                QuestUtility.AddQuestTag(GenSpawn.Spawn(pawn, loc, map, rot), parms.questTag);
                pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);
                pawn.mindState.exitMapAfterTick = Find.TickManager.TicksGame + Rand.Range(AnimalsStayDurationMin, AnimalsStayDurationMax);
            }

            if (XMTSettings.LogWorld)
            {
                Log.Message("Generated animals");
            }

            if (XMTUtility.QueenIsPlayer())
            {
                SendStandardLetter("LetterLabelManhunterPackArrived".Translate(), "ManhunterPackArrived".Translate(animalKind.GetLabelPlural()), LetterDefOf.PositiveEvent, parms, list[0]);
            }
            else
            {
                SendStandardLetter("LetterLabelManhunterPackArrived".Translate(), "ManhunterPackArrived".Translate(animalKind.GetLabelPlural()), LetterDefOf.ThreatBig, parms, list[0]);
                Find.TickManager.slower.SignalForceNormalSpeedShort();
            }
            return true;
        }
    }
}
