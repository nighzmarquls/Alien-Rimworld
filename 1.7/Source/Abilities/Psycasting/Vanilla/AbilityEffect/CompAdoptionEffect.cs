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
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (target.HasThing)
            {
                base.Apply(target, dest);
                if (target.Thing is Pawn subject)
                {
                    bool xenomorphCaster = XMTUtility.IsXenomorph(caster);
                    bool xenomorphTarget = XMTUtility.IsXenomorph(subject);
                    CompPawnInfo info = subject.Info();

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
                                        info.GainObsession(1f);
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
                                    info.GainObsession(0.1f);
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
}
