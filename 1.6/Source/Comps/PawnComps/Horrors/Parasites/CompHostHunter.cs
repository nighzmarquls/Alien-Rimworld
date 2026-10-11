using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype 
{
    public class CompHostHunter : ThingComp
    {
        int nextHuntCheckTick = -1;
        protected CompHostHunterProperties Props => props as CompHostHunterProperties;

        protected Pawn Parent => parent as Pawn;

        public virtual bool IsValidMissionTarget(Pawn target)
        {
            return NemesisMissionUtility.ValidImplantTarget(target);
        }

        public virtual bool ShouldHunt()
        {
            return !NemesisMissionUtility.IsSwarmMember(Parent)
                && Parent.CurJobDef != XenoWorkDefOf.XMT_ImplantHunt
                && Parent.CurJobDef != XenoWorkDefOf.XMT_SubvertTurret && !Parent.Downed;
        }

        public virtual void StartHunt(Pawn prey)
        {
            if (AttachmentUtility.ParticipantIsBusy(Parent) || AttachmentUtility.ParticipantIsBusy(prey))
            {
                return;
            }
            Job attachJob = JobMaker.MakeJob(XenoWorkDefOf.XMT_ImplantHunt, prey);
            Parent.jobs.StartJob(attachJob, JobCondition.InterruptForced);
        }

        public virtual Pawn GetPreyTarget()
        {
            List<Pawn> listOfPawn = parent.Map.mapPawns.AllPawnsSpawned.ToList();
            listOfPawn.Shuffle();
            foreach (Pawn pawn in listOfPawn)
            {
                if (XMTUtility.IsAcidBlooded(pawn))
                {
                    continue;
                }

                if (XMTUtility.IsInorganic(pawn))
                {
                    continue;
                }

                return pawn;
            }
            return null;
        }

        public virtual Thing GetHuntTarget()
        {
            return GetPreyTarget();
        }

        public virtual void StartHuntTarget(Thing target)
        {
            if (target is Pawn prey)
            {
                StartHunt(prey);
            }
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!Parent.Spawned)
            {
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (tick > nextHuntCheckTick)
            {

                nextHuntCheckTick = tick + Mathf.CeilToInt(Props.huntCheckHourInterval * 2500);

                if (ShouldHunt())
                {
                    Thing prey = GetHuntTarget();

                    if (prey != null)
                    {
                        StartHuntTarget(prey);
                    }
                }

            }
        }

        public virtual void PreattachTarget(Pawn target)
        {

        }

        protected bool CanContinueAttach(Pawn target)
        {
            return AttachmentUtility.CanAttach(Parent, target);
        }

        public virtual bool TryResist(Pawn target)
        {
            if (target.apparel != null)
            {
                target.meleeVerbs.TryMeleeAttack(parent);
                if (!CanContinueAttach(target))
                {
                    return true;
                }
            }

            if (Rand.Chance(XMTUtility.GetDefendGrappleChance(Parent, target)))
            {
                return true;
            }

            return false;
        }
        public virtual List<BodyPartRecord> GetTargetBodyParts(Pawn target)
        {
            if (target.RaceProps.Humanlike)
            {
                List<BodyPartRecord> list = (from x in target.health.hediffSet.GetNotMissingParts()
                                             where x.def == BodyPartDefOf.Head
                                             select x).ToList();

                if (list.Any())
                {
                    return list;
                }
            }

            return (from x in target.health.hediffSet.GetNotMissingParts()
                                           where x.depth == BodyPartDepth.Outside
                                           select x).ToList();
        }
        public void TryAttachToHost(Pawn target)
        {
            AttemptAttachToHost(target);
        }

        protected virtual bool CanAttemptAttachment(Pawn target)
        {
            return CanContinueAttach(target);
        }

        protected virtual bool PrepareAttachmentAttempt(Pawn target)
        {
            if (target.Faction != null && !target.Faction.IsPlayer)
            {
                if (Parent.Faction != null)
                {
                    //Blame the source of the thing.
                    //TODO: Ideology check if they venerate xenomorphs.
                    target.Faction.TryAffectGoodwillWith(Parent.Faction, -25, reason: HistoryEventDefOf.AttackedMember);
                }
                else
                {
                    //Blame the Player

                    target.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -25, reason: HistoryEventDefOf.AttackedMember);
                }

                target.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee);
            }

            if (XMTUtility.IsXenomorph(parent))
            {
                XMTUtility.WitnessLarva(target.PositionHeld, target.MapHeld, 0.01f, 0.1f, knowledgeProfile: Props.attackKnowledgeProfile);
            }
            return true;
        }

        protected virtual bool ShouldAllowDefense(Pawn target, bool unwilling)
        {
            return unwilling && !XMTUtility.IsTargetImmobile(target);
        }

        protected virtual float GetAttachmentDodgeChance(Pawn target)
        {
            return XMTUtility.GetDodgeChance(target, true);
        }

        protected virtual void NotifyDefenseResult(AttachmentAttemptResult result)
        {
        }

        protected virtual void NotifyFailedAttachment(Pawn target, AttachmentAttemptResult result)
        {
        }

        protected virtual BodyPartRecord GetApparelAccessPart(Pawn target, BodyPartRecord part, Apparel apparel)
        {
            return apparel.def.apparel.CoversBodyPart(part) ? part : null;
        }

        private AttachmentAttemptResult CheckApparelAccess(Pawn target, BodyPartRecord part)
        {
            float damage = Props.breachDamage;
            float penetration = Props.breachPenetration;
            if (target.apparel == null)
            {
                return AttachmentAttemptResult.Attached;
            }
            foreach (Apparel apparel in target.apparel.WornApparel.AsEnumerable().Reverse().ToList())
            {
                BodyPartRecord accessPart = GetApparelAccessPart(target, part, apparel);
                if (accessPart == null)
                {
                    continue;
                }
                string apparelLabel = apparel.Label;
                PenetrationRequest request = new PenetrationRequest(apparel, accessPart, DamageDefOf.Cut, damage, penetration);
                PenetrationResult result = damage > 0f ? PenetrationUtility.Resolve(request)
                    : new PenetrationResult(false, DamageDefOf.Cut, 0f, 0f, 0f);
                PenetrationUtility.ApplyApparelDamage(request, result, Parent);
                string key = apparel.Destroyed ? "XMT_AttachmentApparelDestroyed"
                    : result.Penetrated ? "XMT_AttachmentApparelBreached" : "XMT_AttachmentApparelBlocked";
                if (!CanContinueAttach(target))
                {
                    return AttachmentAttemptResult.Interrupted;
                }
                MoteMaker.ThrowText(target.DrawPos, target.MapHeld, key.Translate(Parent.Named("PAWN"), apparelLabel.Named("APPAREL")));
                if (!result.Penetrated && !apparel.Destroyed)
                {
                    return AttachmentAttemptResult.ApparelBlocked;
                }
                damage = result.Damage;
                penetration = result.Penetration;
            }
            return AttachmentAttemptResult.Attached;
        }

        protected virtual void InitializeAttachment(Hediff hediff, Pawn target)
        {
        }

        protected virtual void NotifyAttached(Pawn target)
        {
            PreattachTarget(target);
            if (Props.thoughtGivenOnAttach != null)
            {
                XMTUtility.GiveMemory(target, Props.thoughtGivenOnAttach);
            }
            NemesisMissionUtility.NotifyAttackerAttached(Parent, target);
        }

        public virtual AttachmentAttemptResult AttemptAttachToHost(Pawn target)
        {
            if (AttachmentUtility.ParticipantIsBusy(Parent) || AttachmentUtility.ParticipantIsBusy(target))
            {
                return AttachmentAttemptResult.AnimationBusy;
            }
            if (!CanAttemptAttachment(target))
            {
                return AttachmentAttemptResult.InvalidTarget;
            }
            List<BodyPartRecord> source = GetTargetBodyParts(target);
            if (source == null || !source.Any())
            {
                return AttachmentAttemptResult.NoBodyPart;
            }
            source.Shuffle();
            BodyPartRecord targetPart = source.First();
            if (target.health.hediffSet.hediffs.Any(hediff => hediff.def == Props.parasiteHediff && hediff.Part == targetPart))
            {
                return AttachmentAttemptResult.AlreadyAttached;
            }

            bool unwilling = PrepareAttachmentAttempt(target);
            if (!CanContinueAttach(target))
            {
                return AttachmentAttemptResult.Interrupted;
            }

            if (ShouldAllowDefense(target, unwilling))
            {
                if (Rand.Chance(GetAttachmentDodgeChance(target)))
                {
                    MoteMaker.ThrowText(target.DrawPos, target.Map, "TextMote_Dodge".Translate(), 1.9f);
                    NotifyDefenseResult(AttachmentAttemptResult.Dodged);
                    NotifyFailedAttachment(target, AttachmentAttemptResult.Dodged);
                    return AttachmentAttemptResult.Dodged;
                }
                bool resisted = TryResist(target);
                if (!CanContinueAttach(target))
                {
                    return AttachmentAttemptResult.Interrupted;
                }
                if (resisted)
                {
                    MoteMaker.ThrowText(target.DrawPos, target.Map, "Resisted".Translate());
                    NotifyDefenseResult(AttachmentAttemptResult.Resisted);
                    NotifyFailedAttachment(target, AttachmentAttemptResult.Resisted);
                    return AttachmentAttemptResult.Resisted;
                }
            }

            AttachmentAttemptResult access = CheckApparelAccess(target, targetPart);
            if (access != AttachmentAttemptResult.Attached)
            {
                if (access == AttachmentAttemptResult.ApparelBlocked)
                {
                    NotifyDefenseResult(access);
                    NotifyFailedAttachment(target, access);
                }
                return access;
            }

            if (target.jobs != null)
            {
                target.jobs.StopAll();
            }

            if (!CanContinueAttach(target) || target.health.hediffSet.PartIsMissing(targetPart))
            {
                return AttachmentAttemptResult.Interrupted;
            }

            Hediff hediff = HediffMaker.MakeHediff(Props.parasiteHediff, target, targetPart);
            HediffComp_PawnAttachement attachment = hediff.TryGetComp<HediffComp_PawnAttachement>();
            if (attachment == null)
            {
                Log.Error($"[XMT][Attachment] {Props.parasiteHediff} has no pawn attachment comp for {Parent}.");
                return AttachmentAttemptResult.TransferFailed;
            }

            try
            {
                InitializeAttachment(hediff, target);
                target.health.AddHediff(hediff, targetPart, new DamageInfo(DamageDefOf.Stun, 10, 999, instigator: Parent));
            }
            catch (Exception exception)
            {
                Log.Error($"[XMT][Attachment] Failed to add {hediff.def} to {target} for {Parent}: {exception}");
                attachment.removed = true;
                attachment.PawnRelease();
                if (target.health.hediffSet.hediffs.Contains(hediff))
                {
                    target.health.RemoveHediff(hediff);
                }
                return AttachmentAttemptResult.TransferFailed;
            }

            if (!target.health.hediffSet.hediffs.Contains(hediff) || attachment.attachedPawn != Parent)
            {
                attachment.removed = true;
                attachment.PawnRelease();
                if (target.health.hediffSet.hediffs.Contains(hediff))
                {
                    target.health.RemoveHediff(hediff);
                }
                return AttachmentAttemptResult.TransferFailed;
            }

            NotifyAttached(target);
            return AttachmentAttemptResult.Attached;
        }
    }

    public class CompHostHunterProperties : CompProperties
    {
        public HediffDef parasiteHediff;
        public float huntCheckHourInterval = 1;
        public ThoughtDef thoughtGivenOnAttach;
        public KnowledgeProfileDef attackKnowledgeProfile;
        public float breachDamage;
        public float breachPenetration;
        public CompHostHunterProperties()
        {
            this.compClass = typeof(CompHostHunter);
        }

        public CompHostHunterProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
