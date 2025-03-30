using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompAdoptionEffect : CompAbilityEffect
    {
        Pawn caster => parent.pawn;

        private void HumanAdoptHuman(Pawn subject, Pawn caster)
        {
            float subjectSensitivity = subject.GetStatValue(StatDefOf.PsychicSensitivity);
            float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);

            float cumulativeSensitvity = (casterSensitivity*subjectSensitivity)/2;

            bool psychicTestPass = false;

            if(Rand.Chance(cumulativeSensitvity))
            {
                psychicTestPass = true;
            }

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
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (target.HasThing)
            {
                base.Apply(target, dest);
                if (target.Thing is Pawn subject)
                {
                    bool xenomorphCaster = XMTUtility.IsXenomorph(caster);
                    bool xenomorphTarget = XMTUtility.IsXenomorph(subject);
                    CompPawnInfo info = subject.GetComp<CompPawnInfo>();

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
                            if (info != null)
                            {
                                Gene_PsychicBonding bonding = caster.genes.GetFirstGeneOfType<Gene_PsychicBonding>();
                                if (bonding != null)
                                {
                                    if (bonding.CanBondToNewPawn)
                                    {
                                        caster.interactions.TryInteractWith(subject, InteractionDefOf.RomanceAttempt);
                                        info.GainObsession(0.12f);
                                    }
                                }

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
                                    info.WitnessPsychicHorror(0.1f);
                                    info.GainObsession(0.05f);
                                }
                            }
                        }
                    }
                    else
                    {
                        CompPawnInfo casterInfo = caster.GetComp<CompPawnInfo>();

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

                                    casterInfo.WitnessPsychicHorror(0.25f);
                                    casterInfo.GainObsession(0.125f);
                                }
                                else
                                {
                                    HumanAdoptHuman(subject, caster);
                                }
                            }
                            else
                            {
                                float queenSensitivity = queen.GetStatValue(StatDefOf.PsychicSensitivity);
                                float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);

                                float difference = queenSensitivity - casterSensitivity;
                                bool failedPsycastCheck = false;

                                if (difference > casterSensitivity)
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
}
