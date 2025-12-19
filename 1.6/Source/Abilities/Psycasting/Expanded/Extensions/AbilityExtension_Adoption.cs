using AlienRace;
using RimWorld;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace Xenomorphtype
{
    internal class AbilityExtension_Adoption : AbilityExtension_AbilityMod
    {
        private void HumanAdoptHuman(Pawn subject, Pawn caster)
        {

            bool psychicTestPass = HivecastUtility.PsychicConnectionTest(subject, caster);

            if (psychicTestPass)
            {
                if (subject.relations != null && !subject.relations.DirectRelationExists(PawnRelationDefOf.Parent, caster))
                {
                    subject.relations.AddDirectRelation(PawnRelationDefOf.Parent, caster);
                }
                else if (ModsConfig.IdeologyActive && subject.ideo != null)
                {
                    if (caster.ideo.Certainty > subject.ideo.Certainty)
                    {
                        subject.ideo.IdeoConversionAttempt(0.1f, caster.ideo.Ideo);
                    }
                }
                else if (subject.Faction != caster.Faction)
                {

                    subject.SetFaction(caster.Faction);
                }
            }
            else
            {
                if (subject.relations != null && !subject.relations.DirectRelationExists(PawnRelationDefOf.Kin, caster))
                {
                    subject.relations.AddDirectRelation(PawnRelationDefOf.Kin, caster);
                }
            }
        }
        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {

            base.Cast(targets, ability);
            bool xenomorphCaster = XMTUtility.IsXenomorph(ability.pawn);
            Pawn caster = ability.pawn;
            foreach (GlobalTargetInfo targetInfo in targets)
            {
                if (targetInfo.Pawn == null)
                {
                    continue;
                }
                Pawn subject = targetInfo.Pawn;
                bool xenomorphTarget = XMTUtility.IsXenomorph(targetInfo.Pawn);
                if (xenomorphCaster)
                {
                    
                   
                    if (xenomorphTarget)
                    {
                        
                        if (ModsConfig.IdeologyActive && subject.ideo != null)
                        {
                            subject.ideo.SetIdeo(caster.ideo.Ideo);
                        }
                        if (subject.Faction != caster.Faction)
                        {
                            subject.SetFaction(caster.Faction);
                        }
                        if (subject.relations != null && !subject.relations.DirectRelationExists(PawnRelationDefOf.Parent, caster))
                        {
                            subject.relations.AddDirectRelation(PawnRelationDefOf.Parent, caster);
                        }
                        XMTUtility.GiveMemory(subject, HorrorMoodDefOf.XMT_CommuneWithQueen);
                    }
                    else
                    {
                        CompPawnInfo info = subject.Info();
                        if (info != null)
                        {

                            if (info.IsObsessed())
                            {
                                if (ModsConfig.IdeologyActive && subject.ideo != null)
                                {
                                    subject.ideo.SetIdeo(caster.ideo.Ideo);
                                }
                                if (subject.Faction != caster.Faction)
                                {
                                    subject.SetFaction(caster.Faction);
                                }
                                if (subject.relations != null && !subject.relations.DirectRelationExists(PawnRelationDefOf.Parent, caster))
                                {
                                    subject.relations.AddDirectRelation(PawnRelationDefOf.Parent, caster);
                                }

                            }
                            else
                            {
                                info.GainObsession(1f);
                            }
                        }
                    }
                }
                else
                {
                    CompPawnInfo casterInfo = caster.Info();

                    if (casterInfo != null)
                    {
                        Pawn queen = XMTUtility.GetQueen();
                        if (queen == null)
                        {
                            if (xenomorphTarget)
                            {
                                if (ModsConfig.IdeologyActive && subject.ideo != null)
                                {
                                    subject.ideo.SetIdeo(caster.ideo.Ideo);
                                }
                                if (subject.Faction != caster.Faction)
                                {
                                    subject.SetFaction(caster.Faction);
                                }
                                if (subject.relations != null && !subject.relations.DirectRelationExists(PawnRelationDefOf.Parent, caster))
                                {
                                    subject.relations.AddDirectRelation(PawnRelationDefOf.Parent, caster);
                                }
                                subject.GetMorphComp().tamingSocializing += 0.25f;
                                casterInfo.GainObsession(0.01f);
                            }
                            else
                            {
                                HumanAdoptHuman(subject, caster);
                            }
                        }
                        else
                        {

                            bool failedPsycastCheck = HivecastUtility.PsychicChallengeTest(caster, queen);

                            if (failedPsycastCheck)
                            {
                                failedPsycastCheck = true;
                                casterInfo.WitnessPsychicHorror(1f);
                                casterInfo.GainObsession(1f);
                            }

                            if (xenomorphTarget)
                            {
                                if (failedPsycastCheck)
                                {
                                    if (ModsConfig.IdeologyActive && subject.ideo != null)
                                    {
                                        caster.ideo.SetIdeo(queen.ideo.Ideo);
                                    }

                                    if (subject.relations != null && !caster.relations.DirectRelationExists(PawnRelationDefOf.Parent, queen))
                                    {
                                        caster.relations.AddDirectRelation(PawnRelationDefOf.Parent, queen);
                                    }

                                    if (casterInfo.IsObsessed())
                                    {
                                        if (caster.Faction != queen.Faction)
                                        {
                                            caster.SetFaction(queen.Faction);
                                        }
                                    }
                                }
                                else
                                {
                                    casterInfo.WitnessPsychicHorror(1f);
                                    casterInfo.ApplyThreatPheromone(queen, 1, 2);
                                    caster.stances.stunner.StunFor(2f.SecondsToTicks(), queen, addBattleLog: false);
                                }
                            }
                            else
                            {
                                if (failedPsycastCheck)
                                {
                                    if (ModsConfig.IdeologyActive && subject.ideo != null)
                                    {
                                        subject.ideo.SetIdeo(queen.ideo.Ideo);
                                    }
                                    if (subject.Faction != queen.Faction)
                                    {
                                        subject.SetFaction(queen.Faction);
                                    }
                                    if (subject.relations != null && !subject.relations.DirectRelationExists(PawnRelationDefOf.Parent, queen))
                                    {
                                        subject.relations.AddDirectRelation(PawnRelationDefOf.Parent, queen);
                                    }
                                }
                                else
                                {
                                    HumanAdoptHuman(subject, caster);
                                }

                            }
                        }
                    }
                }
            }
        }
    }
}
