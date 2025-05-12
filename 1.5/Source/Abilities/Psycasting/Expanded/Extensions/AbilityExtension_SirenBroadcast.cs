using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using VFECore.Abilities;

using Ability = VFECore.Abilities.Ability;

namespace Xenomorphtype
{
    public class AbilityExtension_SirenBroadcast : AbilityExtension_AbilityMod
    {
        public int goodwillImpactForHostile = -25;

        public int goodwillImpactForFriendly = 25;

        public List<PreceptDef> friendlyPrecepts = null;

        public List<PreceptDef> hostilePrecepts = null;

        public int worldRangeTiles;
        public JobDef mesmerizedJobDef;

        private List<Faction> affectedFactions;

        private static List<IntVec3> cachedRadiusCells = new List<IntVec3>();

        private static IntVec3? cachedRadiusCellsTarget = null;

        private static Map cachedRadiusCellsMap = null;

        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {
            if (affectedFactions == null)
            {
                affectedFactions = new List<Faction>();
            }
            else
            {
                affectedFactions.Clear();
            }

            if (!XMTUtility.IsXenomorph(ability.pawn))
            {
                CompPawnInfo info = ability.pawn.GetComp<CompPawnInfo>();
                if (info != null)
                {
                    info.WitnessPsychicHorror(0.1f);
                    info.GainObsession(0.05f);
                    if (info.IsObsessed())
                    {
                        ability.pawn.needs.joy.GainJoy(0.12f, InternalDefOf.Communion);
                    }
                }
            }

            foreach (Pawn pawn in ability.pawn.Map.mapPawns.AllPawnsSpawned)
            {
                ApplyToPawn(pawn, ability);
            }

            foreach (Map map in Find.Maps)
            {
                if (map == ability.pawn.Map || Find.WorldGrid.TraversalDistanceBetween(map.Tile, ability.pawn.Map.Tile, passImpassable: true, worldRangeTiles + 1) > worldRangeTiles)
                {
                    continue;
                }

                foreach (Pawn pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.Faction != null)
                    {
                        affectedFactions.AddDistinct(pawn.HomeFaction);
                    }
                }
            }

            foreach (Caravan caravan in Find.WorldObjects.Caravans)
            {
                if (Find.WorldGrid.TraversalDistanceBetween(caravan.Tile, ability.pawn.Map.Tile, passImpassable: true, worldRangeTiles + 1) > worldRangeTiles)
                {
                    continue;
                }

                foreach (Pawn pawn in caravan.pawns)
                {
                    if (pawn.Faction != null)
                    {
                        affectedFactions.AddDistinct(pawn.HomeFaction);
                    }
                }
            }

            foreach (Settlement settlement in Find.WorldObjects.Settlements)
            {
                if (Find.WorldGrid.TraversalDistanceBetween(settlement.Tile, ability.pawn.Map.Tile, passImpassable: true, worldRangeTiles + 1) > worldRangeTiles)
                {
                    continue;
                }

                affectedFactions.AddDistinct(settlement.Faction);
            }



            if (ability.pawn.Faction == Faction.OfPlayer)
            {
                foreach (Faction faction in affectedFactions)
                {
                    if (Faction.OfPlayer == faction)
                    {
                        continue;
                    }

                    if (IsFriendlyIdeo(faction))
                    {
                        int goodwillChange = goodwillImpactForFriendly;
                        Faction.OfPlayer.TryAffectGoodwillWith(faction, goodwillChange, canSendMessage: true, canSendHostilityLetter: true, HistoryEventDefOf.RitualDone);
                    }
                    else
                    {
                        int goodwillChange = goodwillImpactForHostile;
                        Faction.OfPlayer.TryAffectGoodwillWith(faction, goodwillChange, canSendMessage: true, canSendHostilityLetter: true, HistoryEventDefOf.UsedHarmfulAbility);
                    }

                    BroadcastReprisal(faction, ability.pawn.Map, ability);
                }
            }

            affectedFactions.Clear();
        }

        private void ApplyToPawn(Pawn pawn, Ability ability)
        {

            if (CanApplyEffects(pawn) && !pawn.Fogged())
            {
                CompPawnInfo info = pawn.GetComp<CompPawnInfo>();
                if (info != null)
                {
                    if (!info.IsObsessed())
                    {
                        float casterSensitivity = ability.pawn.GetStatValue(StatDefOf.PsychicSensitivity);
                        pawn.TakeDamage(new DamageInfo(DamageDefOf.Stun, 20 * casterSensitivity));

                        info.WitnessPsychicHorror(0.1f);
                        info.GainObsession(0.05f);

                        return;
                    }

                    info.WitnessPsychicHorror(0.1f);
                    info.GainObsession(0.1f);
                    if (pawn.needs.joy != null)
                    {
                        pawn.needs.joy.GainJoy(0.12f, InternalDefOf.Communion);
                    }
                }

                Job job = JobMaker.MakeJob(mesmerizedJobDef, new LocalTargetInfo(ability.pawn));


                job.expiryInterval = ability.GetDurationForPawn();
                job.mote = MoteMaker.MakeThoughtBubble(pawn, ability.def.iconPath, maintain: true);
                RestUtility.WakeUp(pawn);
                pawn.jobs.StopAll();
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }
        }

        private bool CanApplyEffects(Pawn p)
        {
            if (p.kindDef.isBoss)
            {
                return false;
            }

            if (!p.Dead && !p.Suspended)
            {
                if (XMTUtility.IsXenomorph(p))
                {
                    return false;
                }

                return p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon;
            }

            return false;
        }

        private bool IsFriendlyIdeo(Faction faction)
        {
            int score = 0;

            if (ModsConfig.IdeologyActive)
            {
                if (faction.ideos != null)
                {
                    if (faction.ideos.PrimaryIdeo is Ideo factionIdeo)
                    {
                        foreach (Precept precept in factionIdeo.PreceptsListForReading)
                        {
                            foreach (PreceptDef friendPrecept in friendlyPrecepts)
                            {
                                if (precept.def == friendPrecept)
                                {
                                    score += 1;
                                    break;
                                }
                            }
                            foreach (PreceptDef hostilePrecept in hostilePrecepts)
                            {
                                if (precept.def == hostilePrecept)
                                {
                                    score -= 1;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            return score > 0;
        }
        private void BroadcastReprisal(Faction faction, Map map, Ability ability)
        {
            if (!ability.pawn.Faction.HostileTo(faction))
            {
                TraderKindDef localTk = faction.def.caravanTraderKinds.RandomElement();
                IncidentParms parms = new IncidentParms
                {
                    target = map,
                    faction = faction,
                    traderKind = localTk,
                    forced = true
                };
                float TicksToArrive = Rand.Range(30000, 60000);
                Find.Storyteller.incidentQueue.Add(IncidentDefOf.TraderCaravanArrival, Find.TickManager.TicksGame + Mathf.FloorToInt(TicksToArrive), parms, 120000);
                if (XMTSettings.LogWorld)
                {
                    Log.Message(faction + " is sending a trade caravan to investigate the Siren Broadcast. Arriving in " + TicksToArrive / 2400 + " hours");
                }
            }
            else
            {
                IncidentParms parms = new IncidentParms
                {
                    target = map,
                    faction = faction,
                    forced = true,
                    points = StorytellerUtility.DefaultThreatPointsNow(map) * 4
                };
                float TicksToArrive = Rand.Range(30000, 60000);
                Find.Storyteller.incidentQueue.Add(IncidentDefOf.RaidEnemy, Find.TickManager.TicksGame + Mathf.FloorToInt(TicksToArrive), parms, 120000);
                if (XMTSettings.LogWorld)
                {
                    Log.Message(faction + " is sending a raid of points: " + parms.points + " to investigate the Siren Broadcast. Arriving in " + TicksToArrive / 2400 + " hours");
                }
            }

        }
    }


}
