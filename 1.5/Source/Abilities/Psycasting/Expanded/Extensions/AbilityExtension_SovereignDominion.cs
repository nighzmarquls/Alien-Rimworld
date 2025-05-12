using RimWorld;
using RimWorld.Planet;
using Verse;
using VFECore.Abilities;
using Ability = VFECore.Abilities.Ability;

namespace Xenomorphtype
{
    public class AbilityExtension_SovereignDominion : AbilityExtension_AbilityMod
    {
        private void HumanDominateHuman(Pawn subject, Pawn caster)
        {

            bool psychicTestPass = HivecastUtility.PsychicConnectionTest(subject, caster);

            if (psychicTestPass)
            {
                if (subject.guest != null)
                {
                    subject.guest.SetGuestStatus(caster.Faction, GuestStatus.Slave);
                }
                else
                {
                    if (TameUtility.CanTame(subject))
                    {
                        subject.SetFaction(caster.Faction);
                    }
                }
            }
            else
            {
                if (subject.TargetCurrentlyAimingAt.Pawn == caster)
                {
                    float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);
                    subject.TakeDamage(new DamageInfo(DamageDefOf.Stun, 2 * casterSensitivity));
                }
            }
        }
        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {
            base.Cast(targets, ability);
            Pawn caster = ability.pawn;
            bool xenomorphCaster = XMTUtility.IsXenomorph(caster);
            foreach (GlobalTargetInfo target in targets)
            {
                
                if (target.Thing == caster)
                {
                    continue;
                }

                if (target.Thing is Pawn subject)
                {
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
                                bool psychicTestPass = HivecastUtility.PsychicChallengeTest(subject, caster);

                                if (info.IsObsessed() || psychicTestPass)
                                {
                                    if (subject.guest != null)
                                    {
                                        subject.guest.SetGuestStatus(caster.Faction, GuestStatus.Slave);
                                    }
                                    else
                                    {
                                        if (TameUtility.CanTame(subject))
                                        {
                                            subject.SetFaction(caster.Faction);
                                        }
                                    }
                                    MoteMaker.ThrowText(subject.DrawPos, subject.Map, "Dominated");
                                }
                                else
                                {
                                    MoteMaker.ThrowText(subject.DrawPos, subject.Map, "Resisted");
                                    float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);
                                    subject.TakeDamage(new DamageInfo(DamageDefOf.Stun, 2 * casterSensitivity));
                                    info.WitnessPsychicHorror(0.2f);
                                    info.GainObsession(0.25f);
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
                                }
                                else
                                {
                                    HumanDominateHuman(subject, caster);
                                }
                            }
                            else
                            {

                                bool failedPsycastCheck = HivecastUtility.PsychicChallengeTest(caster, queen);

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
                                        HumanDominateHuman(subject, caster);
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
