using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class InteractionWorker_AdvancedTameAttempt : InteractionWorker
    {
        public const float BaseResistanceReductionPerInteraction = 1f;


        private const float TameChanceFactor_Bonded = 4f;

        private const float ChanceToDevelopBondRelationOnTamed = 0.01f;

        private const int MenagerieTaleThreshold = 5;

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;

            int num = recipient.relations?.OpinionOf(initiator) ?? 0;
            bool inspired = initiator.InspirationDef == InspirationDefOf.Inspired_Recruitment;

            CompMatureMorph morph = recipient.GetMorphComp();

            if(morph == null)
            {
                TaggedString taggedString = "TextMote_TameFail".Translate(0);
                MoteMaker.ThrowText((initiator.DrawPos + recipient.DrawPos) / 2f, initiator.Map, taggedString, 8f);
                extraSentencePacks.Add(RulePackDefOf.Sentence_RecruitAttemptRejected);
                return;
            }

            float displayAmount = morph.Taming*2;

            if (morph.Tamed)
            {
                if (recipient.Faction != initiator.Faction && !recipient.IsSlave)
                {
                    DoRecruit(initiator, recipient, out letterLabel, out letterText, useAudiovisualEffects: true, sendLetter: false);
                    if (!letterLabel.NullOrEmpty())
                    {
                        letterDef = LetterDefOf.PositiveEvent;
                    }

                    lookTargets = new LookTargets(recipient, initiator);
                    extraSentencePacks.Add(RulePackDefOf.Sentence_RecruitAttemptAccepted);
                }

                if (recipient.IsOnHoldingPlatform)
                {
                    if (recipient.ParentHolder is Building_HoldingPlatform platform)
                    {
                        if (platform.TryGetComp(out CompEntityHolder comp))
                        {
                            comp.EjectContents();
                        }
                    }
                }
            }
            else
            {
                TaggedString taggedString = "XMT_TextMote_TameFail".Translate(displayAmount.ToStringPercent());
                MoteMaker.ThrowText((initiator.DrawPos), initiator.Map, taggedString, 8f);
                extraSentencePacks.Add(RulePackDefOf.Sentence_RecruitAttemptRejected);
            }
        }

        public static void DoRecruit(Pawn recruiter, Pawn recruitee, bool useAudiovisualEffects = true)
        {
            DoRecruit(recruiter, recruitee, out var _, out var _, useAudiovisualEffects);
        }

        public static void DoRecruit(Pawn recruiter, Pawn recruitee, out string letterLabel, out string letter, bool useAudiovisualEffects = true, bool sendLetter = true)
        {
            letterLabel = null;
            letter = null;
            string text = recruitee.LabelIndefinite();
            Faction faction = recruiter?.Faction ?? Faction.OfPlayer;
            if (recruiter == null)
            {
                sendLetter = false;
                useAudiovisualEffects = false;
            }

            bool flag = recruitee.Name != null;
            if (recruitee.GetMorphComp() is CompMatureMorph morph)
            {
                if (ModsConfig.IdeologyActive)
                {
                    if(morph.Integrated)
                    {
                        RecruitUtility.Recruit(recruitee, faction, recruiter);
                    }
                    else
                    {
                        bool everEnslaved = recruitee.guest.EverEnslaved;
                        recruitee.guest.SetGuestStatus(recruiter.Faction, GuestStatus.Slave);
                        Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.EnslavedPrisoner, recruiter.Named(HistoryEventArgsNames.Doer)));
                    }
                }
                else
                {
                    RecruitUtility.Recruit(recruitee, faction, recruiter);
                }

                if (recruiter != null)
                {
                    recruiter.records.Increment(RecordDefOf.AnimalsTamed);
                    if (Rand.Chance(Mathf.Lerp(0.02f, 1f, recruitee.GetStatValue(StatDefOf.Wildness))))
                    {
                        TaleRecorder.RecordTale(TaleDefOf.TamedAnimal, recruiter, recruitee);
                    }

                    if (PawnsFinder.AllMapsWorldAndTemporary_Alive.Count((Pawn p) => p.playerSettings != null && p.playerSettings.Master == recruiter) >= 5)
                    {
                        TaleRecorder.RecordTale(TaleDefOf.IncreasedMenagerie, recruiter, recruitee);
                    }
                }
            }
           
            if (useAudiovisualEffects)
            {
                if (!flag)
                {
                    Messages.Message("MessageTameAndNameSuccess".Translate(recruiter.LabelShort, text, recruitee.Name.ToStringFull, recruiter.Named("RECRUITER"), recruitee.Named("RECRUITEE")).AdjustedFor(recruitee), recruitee, MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("MessageTameSuccess".Translate(recruiter.LabelShort, text, recruiter.Named("RECRUITER")), recruitee, MessageTypeDefOf.PositiveEvent);
                }

                if (recruiter.Spawned && recruitee.Spawned)
                {
                    MoteMaker.ThrowText((recruiter.DrawPos + recruitee.DrawPos) / 2f, recruiter.Map, "TextMote_TameSuccess".Translate(), 8f);
                }
            }
            
            if (recruitee.caller != null)
            {
                recruitee.caller.DoCall();
            }
        }
    }
}
