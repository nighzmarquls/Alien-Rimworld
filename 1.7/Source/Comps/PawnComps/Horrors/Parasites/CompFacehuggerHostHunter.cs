using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class CompFacehuggerHostHunter : CompHostHunter
    {
        private CompLarvalGenes Larva => Parent.GetComp<CompLarvalGenes>();
        private new CompProperties_FacehuggerHostHunter Props => (CompProperties_FacehuggerHostHunter)props;
        private static Texture2D Implant => ContentFinder<Texture2D>.Get("UI/Abilities/Implant");

        public override bool ShouldHunt()
        {
            return Larva != null && !Larva.latched && !Larva.spent && base.ShouldHunt();
        }

        // The existing job giver owns nearby hunting. Do not also start map-wide rare-tick hunts.
        public override void CompTickRare()
        {
        }

        public override Pawn GetPreyTarget()
        {
            if (!Parent.Spawned || !ShouldHunt() || AttachmentUtility.ParticipantIsBusy(Parent))
            {
                return null;
            }
            return GenRadial.RadialDistinctThingsAround(Parent.Position, Parent.Map, Props.leapRange, true)
                .OfType<Pawn>().FirstOrDefault(target => !AttachmentUtility.ParticipantIsBusy(target) && XMTUtility.TriggersOvomorph(target));
        }

        protected override bool CanAttemptAttachment(Pawn target)
        {
            return Larva != null && !Larva.latched && !Larva.spent && base.CanAttemptAttachment(target);
        }

        public override List<BodyPartRecord> GetTargetBodyParts(Pawn target)
        {
            return target.health.hediffSet.GetNotMissingParts().Where(XMTUtility.IsPartHead).ToList();
        }

        protected override bool PrepareAttachmentAttempt(Pawn target)
        {
            bool unwilling = true;

            if (target.Faction != null && !target.Faction.IsPlayer)
            {
                int goodWill = -25;

                if (ModsConfig.IdeologyActive)
                {
                    if (target.Faction.ideos != null)
                    {
                        goodWill += target.Faction.ideos.PrimaryIdeo.HasPrecept(XenoPreceptDefOf.XMT_Biomorph_Worship) ? 20 : 0;
                        goodWill += target.Faction.ideos.PrimaryIdeo.HasPrecept(XenoPreceptDefOf.XMT_Parasite_Revered) ? 5 : 0;
                        goodWill += target.Faction.ideos.PrimaryIdeo.HasPrecept(XenoPreceptDefOf.XMT_Parasite_Reincarnation) ? 30 : 0;
                        if(goodWill >= 0)
                        {
                            unwilling = false;
                        }
                        goodWill += target.Faction.ideos.PrimaryIdeo.HasPrecept(XenoPreceptDefOf.XMT_Biomorph_Study) ? 15 : 0;
                    }
                }

                if (Parent.Faction != null)
                {
                    //Blame the source of the thing.

                    target.Faction.TryAffectGoodwillWith(Parent.Faction, goodWill, reason: XenoPreceptDefOf.XMT_Parasite_Attached);
                }
                else
                {
                    //Blame the Player

                    target.Faction.TryAffectGoodwillWith(Faction.OfPlayer, goodWill, reason: XenoPreceptDefOf.XMT_Parasite_Attached);
                }

            }

            XMTUtility.WitnessLarva(Parent.PositionHeld, Parent.MapHeld, 0.5f, false, knowledgeProfile: Props.attackKnowledgeProfile);

            return unwilling && KnowledgeUtility.GetAssessment(target).obsessionSeverity != ObsessionSeverity.Enthralled;
        }

        protected override bool ShouldAllowDefense(Pawn target, bool unwilling)
        {
            return base.ShouldAllowDefense(target, unwilling) && !Parent.IsPsychologicallyInvisible();
        }

        protected override float GetAttachmentDodgeChance(Pawn target)
        {
            KnowledgeAssessment assessment = KnowledgeUtility.GetAssessment(target);
            float chance = XMTUtility.GetDodgeChance(target, false)
                + KnowledgeUtility.GetEffectiveKnowledge(target, KnowledgeDefOf.XMT_Knowledge_Larva) / 2f;
            return AttachmentDefenseUtility.AdjustDefenseChance(chance, assessment.trauma, assessment.obsessionSeverity);
        }

        public override bool TryResist(Pawn target)
        {
            KnowledgeAssessment assessment = KnowledgeUtility.GetAssessment(target);
            return !CanContinueAttach(target) || Rand.Chance(AttachmentDefenseUtility.AdjustDefenseChance(
                XMTUtility.GetDefendGrappleChance(Parent, target), assessment.trauma, assessment.obsessionSeverity));
        }

        protected override BodyPartRecord GetApparelAccessPart(Pawn target, BodyPartRecord part, Apparel apparel)
        {
            if (apparel.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead))
            {
                return part;
            }
            // Open upper-head helmets do not obstruct the face; mouth-covering masks do.
            BodyPartGroupDef mouth = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail("Mouth");
            return mouth == null ? null : target.health.hediffSet.GetNotMissingParts().FirstOrDefault(candidate =>
                candidate.IsInGroup(mouth) && apparel.def.apparel.CoversBodyPart(candidate));
        }

        private bool IsIdeologicallyWilling(Pawn target)
        {
            Ideo ideology = ModsConfig.IdeologyActive && target.Faction?.IsPlayer == false
                ? target.Faction.ideos?.PrimaryIdeo : null;
            return ideology != null && ((ideology.HasPrecept(XenoPreceptDefOf.XMT_Biomorph_Worship) ? 20 : 0)
                + (ideology.HasPrecept(XenoPreceptDefOf.XMT_Parasite_Revered) ? 5 : 0)
                + (ideology.HasPrecept(XenoPreceptDefOf.XMT_Parasite_Reincarnation) ? 30 : 0) >= 25);
        }

        protected override void NotifyFailedAttachment(Pawn target, AttachmentAttemptResult result)
        {
            if (!CanContinueAttach(target) || XMTUtility.IsTargetImmobile(target) || target.Drafted
                || target.InMentalState || target.jobs == null || target.CurJob?.playerForced == true
                || (target.IsColonist && target.playerSettings?.hostilityResponse == HostilityResponseMode.Ignore)
                || IsIdeologicallyWilling(target)
                || KnowledgeUtility.GetAssessment(target).obsessionSeverity >= ObsessionSeverity.Obsessed)
            {
                return;
            }
            JobDef current = target.CurJobDef;
            if (current == JobDefOf.AttackMelee || current == JobDefOf.AttackStatic || current == JobDefOf.Flee
                || current == JobDefOf.FleeAndCower || current == JobDefOf.FleeAndCowerShort || current == JobDefOf.Wait_Combat)
            {
                return;
            }
            bool retaliate = !target.WorkTagIsDisabled(WorkTags.Violent)
                && !(target.IsColonist && target.playerSettings?.hostilityResponse == HostilityResponseMode.Flee);
            Job response;
            if (retaliate)
            {
                response = JobMaker.MakeJob(JobDefOf.AttackMelee, Parent);
            }
            else
            {
                response = FleeUtility.FleeJob(target, Parent, 12)
                    ?? JobMaker.MakeJob(JobDefOf.FleeAndCower, target.Position, Parent);
                if (response.def == JobDefOf.Flee)
                {
                    response.def = JobDefOf.FleeAndCower;
                }
                response.exitMapOnArrival = false;
            }
            if (response != null)
            {
                response.expiryInterval = 180;
                response.checkOverrideOnExpire = true;
                target.jobs.StartJob(response, JobCondition.InterruptForced);
            }
        }

        protected override void NotifyDefenseResult(AttachmentAttemptResult result)
        {
            float duration = result == AttachmentAttemptResult.Dodged ? 20f : 60f;
            Parent.TakeDamage(new DamageInfo(DamageDefOf.Stun, duration));
        }

        protected override void InitializeAttachment(Hediff hediff, Pawn target)
        {
            HediffComp_LarvalAttachment attachment = hediff.TryGetComp<HediffComp_LarvalAttachment>();
            if (attachment == null)
            {
                throw new InvalidOperationException("Facehugger attachment lacks HediffComp_LarvalAttachment.");
            }
            attachment.mother = Larva.mother;
            attachment.father = Larva.father;
            attachment.genes = Larva.CaptureGeneSet();
            attachment.removedDefaultGenes = new System.Collections.Generic.List<GeneDef>(Larva.removedDefaultGenes);
            attachment.kind = Parent.kindDef;
            attachment.name = Parent.Name?.ToString();
            attachment.age = Parent.ageTracker.AgeBiologicalYearsFloat;
        }

        protected override void NotifyAttached(Pawn target)
        {
            base.NotifyAttached(target);
            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Parasite_Attached,
                target.Named(HistoryEventArgsNames.Doer), Parent.Named(HistoryEventArgsNames.Victim)), true);
            if (target.Faction != null && !target.Faction.IsPlayer && !IsIdeologicallyWilling(target)
                && KnowledgeUtility.GetAssessment(target).obsessionSeverity < ObsessionSeverity.Obsessed)
            {
                target.mindState?.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!ShouldHunt() || !XMTUtility.PlayerXenosOnMap(Parent.MapHeld))
            {
                yield break;
            }

            TargetingParameters ImplantParameters = TargetingParameters.ForPawns();

            ImplantParameters.validator = delegate (TargetInfo target)
            {
                if (target.Thing == Parent)
                {
                    return false;
                }

                if(!XMTUtility.IsHost(target.Thing))
                {
                    return false;
                }

                return target.Map.reachability.CanReach(parent.Position, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly);
            };

            Command_Action ImplantHost_Action = new Command_Action();
            ImplantHost_Action.defaultLabel = "XMT_Implant".Translate();
            ImplantHost_Action.defaultDesc = "XMT_ImplantDescription".Translate();
            ImplantHost_Action.icon = Implant;
            ImplantHost_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(ImplantParameters, delegate (LocalTargetInfo target)
                {
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ImplantHunt, target);
                    Parent.Reserve(target, job);
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);

                });

            };

            yield return ImplantHost_Action;
        }

    }

    public class CompProperties_FacehuggerHostHunter : CompHostHunterProperties
    {
        public float leapRange = 4f;

        public CompProperties_FacehuggerHostHunter() : base(typeof(CompFacehuggerHostHunter))
        {
        }
    }
}
