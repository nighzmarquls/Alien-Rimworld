using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Xenomorphtype
{
    public class RitualOutcomeEffectWorker_MatureQueen : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_MatureQueen()
        {
        }

        public RitualOutcomeEffectWorker_MatureQueen(RitualOutcomeEffectDef def)
            : base(def)
        {
        }
        public override bool SupportsAttachableOutcomeEffect => false;

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            float quality = GetQuality(jobRitual, progress);
            Pawn pawn = jobRitual.PawnWithRole("ascendant");

            if(progress >= 1)
            {
                if (XMTUtility.TransformPawnIntoPawn(pawn, InternalDefOf.XMT_RoyaltyKind, out Pawn queen))
                {
                    if (ModsConfig.RoyaltyActive)
                    {
                        FleckMaker.Static(queen.Position, queen.Map, FleckDefOf.PsycastAreaEffect, 10f);
                        SoundDefOf.PsycastPsychicPulse.PlayOneShot(new TargetInfo(queen));
                    }

                    queen.needs.joy.GainJoy(0.5f, InternalDefOf.Communion);

                    foreach (Pawn subject in queen.Map.mapPawns.AllHumanlikeSpawned)
                    {
                        if(subject == queen)
                        {
                            continue;
                        }
                        bool IsXenomorph = XMTUtility.IsXenomorph(subject);

                        if(ModsConfig.IdeologyActive)
                        {
                            if(IsXenomorph)
                            {
                                subject.ideo.SetIdeo(queen.ideo.Ideo);
                            }
                        }

                        if(IsXenomorph)
                        {
                            if (subject.Faction != queen.Faction)
                            {
                                subject.SetFaction(queen.Faction);
                            }
                            if (!subject.relations.DirectRelationExists(PawnRelationDefOf.Parent, queen))
                            {
                                subject.relations.AddDirectRelation(PawnRelationDefOf.Parent, queen);
                            }

                            subject.needs.joy.GainJoy(0.5f, InternalDefOf.Communion);
                            XMTUtility.GiveMemory(subject, HorrorMoodDefOf.XMT_CommuneWithQueen);
                        }
                    }
                    string text = queen + " has finished her metamorphosis into a queen!";

                    text = text + "\n\n" + OutcomeQualityBreakdownDesc(quality, progress, jobRitual);
                    Find.LetterStack.ReceiveLetter("Ascension Complete!", text, LetterDefOf.RitualOutcomePositive, new LookTargets(queen, jobRitual.selectedTarget.Thing));
                    CompQueen compQueen = queen.GetComp<CompQueen>();
                    if (compQueen != null)
                    {
                        compQueen.RecieveProgress(Mathf.Max(1, quality * 3f));
                    }
                }
            }
            if (progress >= 0.25f)
            {
                XMTUtility.DropAmountThing(InternalDefOf.Starbeast_Resin, 150, jobRitual.selectedTarget.Cell, jobRitual.selectedTarget.Map, InternalDefOf.Starbeast_Filth_Resin);
                FilthMaker.TryMakeFilth(jobRitual.selectedTarget.Cell, jobRitual.selectedTarget.Map, InternalDefOf.Starbeast_Filth_Resin, count: 100);
                jobRitual.selectedTarget.Thing.Destroy();
            }
        }
    }
}
