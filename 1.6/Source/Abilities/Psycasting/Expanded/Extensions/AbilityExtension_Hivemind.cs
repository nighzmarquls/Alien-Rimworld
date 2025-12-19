
using RimWorld;
using RimWorld.Planet;

using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;

namespace Xenomorphtype
{
    internal class AbilityExtension_Hivemind : AbilityExtension_AbilityMod
    {
        public float psychicHorrorGain = 0.1f;
        public float obsessionGain = 0.1f;
        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {

            base.Cast(targets, ability);
            bool xenomorphCaster = XMTUtility.IsXenomorph(ability.pawn);

            Pawn queen = XMTUtility.GetQueen();
            CompPawnInfo casterInfo = ability.pawn.Info();

            foreach (GlobalTargetInfo targetInfo in targets)
            {
                if(targetInfo.Pawn == null)
                {
                    continue;
                }

                bool xenomorphTarget = XMTUtility.IsXenomorph(targetInfo.Pawn);

                if (!xenomorphTarget && xenomorphCaster)
                {
                    CompPawnInfo subjectInfo = targetInfo.Pawn.Info();

                    if (ability.pawn.genes != null)
                    {
                        Gene_PsychicBonding bonding = ability.pawn.genes.GetFirstGeneOfType<Gene_PsychicBonding>();
                        if (bonding != null)
                        {
                            if (bonding.CanBondToNewPawn)
                            {
                                ability.pawn.interactions.TryInteractWith(targetInfo.Pawn, InteractionDefOf.RomanceAttempt);
                                subjectInfo.GainObsession(1f);
                            }
                        }
                    }
                    bool failedTargetPsycastCheck = HivecastUtility.PsychicChallengeTest( targetInfo.Pawn,ability.pawn);
                    if(failedTargetPsycastCheck)
                    {
                        
                        subjectInfo.WitnessPsychicHorror(psychicHorrorGain);
                        subjectInfo.GainObsession(obsessionGain);

                    }
                }
                else if(xenomorphTarget && !xenomorphCaster)
                {
                    bool failedTargetPsycastCheck = HivecastUtility.PsychicChallengeTest(ability.pawn, targetInfo.Pawn);
                    if (failedTargetPsycastCheck)
                    {
                        if (targetInfo.Pawn.genes != null)
                        {
                            Gene_PsychicBonding bonding = targetInfo.Pawn.genes.GetFirstGeneOfType<Gene_PsychicBonding>();
                            if (bonding != null)
                            {
                                if (bonding.CanBondToNewPawn)
                                {
                                    targetInfo.Pawn.interactions.TryInteractWith(ability.pawn, InteractionDefOf.RomanceAttempt);
                                    casterInfo.GainObsession(obsessionGain);
                                }
                            }
                        }
                        casterInfo.WitnessPsychicHorror(psychicHorrorGain);
                        casterInfo.GainObsession(obsessionGain);
                    }
                    else
                    {
                        targetInfo.Pawn.GetMorphComp().tamingSocializing += 0.1f;
                    }
                }
            }

            if(xenomorphCaster)
            {
                return;
            }

            if (queen == null)
            {
                return;
            }

            bool failedPsycastCheck = HivecastUtility.PsychicChallengeTest(ability.pawn, queen);

            if (failedPsycastCheck)
            {
                float queenPsychicPower = queen.GetStatValue(StatDefOf.PsychicSensitivity);
                casterInfo.WitnessPsychicHorror(queenPsychicPower);
                casterInfo.GainObsession(queenPsychicPower);
            }
            
        }
    }
}
