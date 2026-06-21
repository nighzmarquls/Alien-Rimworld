using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal static class InorganicSubversionUtility
    {
        public static int RequiredSubversionLoad(Pawn host)
        {
            if (host == null || !XMTUtility.IsInorganic(host))
            {
                return int.MaxValue;
            }

            if (!ModsConfig.BiotechActive || StatDefOf.BandwidthCost == null)
            {
                return 1;
            }

            return Mathf.Max(1, Mathf.CeilToInt(host.GetStatValue(StatDefOf.BandwidthCost)));
        }

        public static bool HasSufficientSubversionLoad(Pawn host)
        {
            return host != null && host.InorganicSubversionLoad() >= RequiredSubversionLoad(host);
        }

        public static bool IsSubverted(Pawn host)
        {
            return host?.health?.hediffSet?.HasHediff(InternalDefOf.XMT_InorganicSubverted) == true;
        }

        public static bool IsValidSubverterTarget(Pawn subverter, Pawn target)
        {
            if (subverter == null || target == null || target == subverter || target.Dead)
            {
                return false;
            }

            if (!target.Spawned || subverter.MapHeld == null || target.MapHeld != subverter.MapHeld)
            {
                return false;
            }

            if (!XMTUtility.IsInorganic(target))
            {
                return false;
            }

            if (IsSubverted(target) || HasSufficientSubversionLoad(target))
            {
                return false;
            }

            Pawn queen = XMTUtility.GetQueen();
            if (queen != null)
            {
                if (target == queen || MechanitorUtility.GetOverseer(target) == queen)
                {
                    return false;
                }

                if (target.Faction != null && target.Faction == queen.Faction)
                {
                    return false;
                }
            }

            return true;
        }

        public static void NotifySubverterLoadChanged(Pawn host, bool recalculateLoad = true, HediffComp_InorganicSubverterAttachment ignoredAttachment = null)
        {
            Log.Message(host + " is being subverted");
            if (host == null || host.Dead || host.health?.hediffSet == null)
            {
                return;
            }

            if (!XMTUtility.IsInorganic(host))
            {
                RestoreAllSuppressedAttachmentBandwidth(host);
                host.UpdateInorganicSubversionLoad(0f);
                return;
            }

            if (recalculateLoad)
            {
                RecalculateSubversionLoad(host, ignoredAttachment);
            }

            Hediff controlHediff = host.health.hediffSet.GetFirstHediffOfDef(InternalDefOf.XMT_InorganicSubverted);
            bool sufficientLoad = HasSufficientSubversionLoad(host);
            if (sufficientLoad)
            {
                if (controlHediff == null)
                {
                    BeginSubversion(host);
                }
                else
                {
                    Log.Message(host + " is maintaining subversion");
                    MaintainSubversion(host, controlHediff);
                }
            }
            else if (controlHediff != null)
            {
                EndSubversion(host, controlHediff);
            }
            else
            {
                RestoreAllSuppressedAttachmentBandwidth(host);
            }
        }

        public static void RecalculateSubversionLoad(Pawn host, HediffComp_InorganicSubverterAttachment ignoredAttachment = null)
        {
            if (host == null)
            {
                return;
            }

            float load = 0f;
            foreach (HediffComp_InorganicSubverterAttachment attachment in GetSubverterAttachments(host))
            {
                if (attachment == ignoredAttachment)
                {
                    continue;
                }

                load += attachment.SubversionInfluence;
            }

            host.UpdateInorganicSubversionLoad(load);
        }

        private static void BeginSubversion(Pawn host)
        {
            Pawn queen = XMTUtility.GetQueen();
            if (queen == null)
            {
                return;
            }

            Hediff controlHediff = HediffMaker.MakeHediff(InternalDefOf.XMT_InorganicSubverted, host);
            host.health.AddHediff(controlHediff);
            HediffComp_InorganicSubvertedControl control = controlHediff.TryGetComp<HediffComp_InorganicSubvertedControl>();
            control?.StoreOriginalState();

            if (XMTUtility.QueenIsPlayer())
            {
                CompOverseerSubject subjectComp = host.GetComp<CompOverseerSubject>();

                

                if (queen.mechanitor == null)
                {
                    host.health.RemoveHediff(controlHediff);
                    return;
                }

                control?.NotifyPlayerSubversion(queen);

                if (host.OverseerSubject == null)
                {
                    if (ModsConfig.IdeologyActive && host.ideo != null)
                    {
                        host.ideo.SetIdeo(queen.ideo.Ideo);
                    }
                    if (host.Faction != queen.Faction)
                    {
                        host.SetFaction(queen.Faction);
                    }
                    if (host.relations != null && !host.relations.DirectRelationExists(PawnRelationDefOf.Parent, queen))
                    {
                        host.relations.AddDirectRelation(PawnRelationDefOf.Parent, queen);
                    }

                    XMTUtility.GiveMemory(host, HorrorMoodDefOf.XMT_CommuneWithQueen);

                    return;
                }

                SuppressAllSubverterBandwidth(host);

                if (MechanitorUtility.GetOverseer(host) != queen)
                {
                    AssignHostToQueen(host, queen);
                }
            }
            else
            {
                StartSubvertedBerserk(host);
            }
        }

        private static void MaintainSubversion(Pawn host, Hediff controlHediff)
        {
            Pawn queen = XMTUtility.GetQueen();
            if (queen == null)
            {
                return;
            }

            if (XMTUtility.QueenIsPlayer())
            {
                SuppressAllSubverterBandwidth(host);
                if (MechanitorUtility.GetOverseer(host) != queen)
                {
                    AssignHostToQueen(host, queen);
                }
            }
            else
            {
                StartSubvertedBerserk(host);
            }
        }

        private static void EndSubversion(Pawn host, Hediff controlHediff)
        {
            RestoreAllSuppressedAttachmentBandwidth(host);
            HediffComp_InorganicSubvertedControl control = controlHediff.TryGetComp<HediffComp_InorganicSubvertedControl>();
            control?.RestoreOriginalState();
            if (host.health.hediffSet.hediffs.Contains(controlHediff))
            {
                host.health.RemoveHediff(controlHediff);
            }
        }

        private static void StartSubvertedBerserk(Pawn host)
        {
            if (host?.mindState?.mentalStateHandler == null || XenoMentalStateDefOf.XMT_InorganicSubvertedBerserk == null)
            {
                return;
            }

            if (host.MentalStateDef == XenoMentalStateDefOf.XMT_InorganicSubvertedBerserk)
            {
                return;
            }

            host.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_InorganicSubvertedBerserk, "", forced: true, forceWake: true, false);
        }

        public static void StopSubvertedBerserk(Pawn host)
        {
            if (host?.MentalStateDef == XenoMentalStateDefOf.XMT_InorganicSubvertedBerserk)
            {
                host.MentalState?.RecoverFromState();
            }
        }
        private static void ReleaseAllSubverter(Pawn host)
        {
            foreach (HediffComp_InorganicSubverterAttachment attachment in GetSubverterAttachments(host))
            {
                attachment.PawnRelease();
            }
        }
        private static void SuppressAllSubverterBandwidth(Pawn host)
        {
            foreach (HediffComp_InorganicSubverterAttachment attachment in GetSubverterAttachments(host))
            {
                if (!attachment.BandwidthSuppressed)
                {
                    SuppressAttachmentBandwidth(attachment);
                }

            }
            
        }

        public static void RestoreAllSuppressedAttachmentBandwidth(Pawn host)
        {
            foreach (HediffComp_InorganicSubverterAttachment attachment in GetSubverterAttachments(host))
            {
                if (attachment.BandwidthSuppressed)
                {
                    RestoreAttachmentBandwidth(attachment);
                }
            }
        }

        private static IEnumerable<HediffComp_InorganicSubverterAttachment> GetSubverterAttachments(Pawn host)
        {
            if (host?.health?.hediffSet == null)
            {
                yield break;
            }

            foreach (HediffComp_InorganicSubverterAttachment attachment in host.health.hediffSet.GetHediffComps<HediffComp_InorganicSubverterAttachment>())
            {
                if (attachment?.attachedPawn != null)
                {
                    yield return attachment;
                }
            }
        }

        public static bool SuppressAttachmentBandwidth(HediffComp_InorganicSubverterAttachment attachment)
        {
            Pawn subverter = attachment?.attachedPawn;
            if (subverter == null || attachment.BandwidthSuppressed)
            {
                return false;
            }

            RemoveOverseerRelationSilently(subverter);
            attachment.SetBandwidthSuppressed(true);
            XMTUtility.GetQueen()?.mechanitor?.Notify_BandwidthChanged();
            return true;
        }

        public static void RestoreAttachmentBandwidth(HediffComp_InorganicSubverterAttachment attachment)
        {
            Pawn subverter = attachment?.attachedPawn;
            Pawn queen = XMTUtility.GetQueen();
            if (subverter == null || queen == null)
            {
                attachment?.SetBandwidthSuppressed(false);
                return;
            }

            if (XMTUtility.QueenIsPlayer() && queen.mechanitor != null && subverter.OverseerSubject != null)
            {
                if (queen.Faction != null && subverter.Faction != queen.Faction)
                {
                    subverter.SetFaction(queen.Faction);
                }
                AddOverseerRelation(queen, subverter);
                queen.mechanitor.AssignPawnControlGroup(subverter, MechWorkModeDefOf.Work);
                queen.mechanitor.Notify_BandwidthChanged();
            }

            attachment.SetBandwidthSuppressed(false);
        }

        public static bool AssignMinorMechToQueen(Pawn mech, Pawn queen)
        {
            if (mech == null || queen?.mechanitor == null || mech.OverseerSubject == null)
            {
                return false;
            }

            if (queen.Faction != null && mech.Faction != queen.Faction)
            {
                mech.SetFaction(queen.Faction);
            }

            AddOverseerRelation(queen, mech);
            queen.mechanitor.Notify_BandwidthChanged();
            return true;
        }

        public static bool AssignHostToQueen(Pawn host, Pawn queen)
        {
            if (host == null || queen?.mechanitor == null || host.OverseerSubject == null)
            {
                return false;
            }

            RemoveOverseerRelationSilently(host);
            if (queen.Faction != null && host.Faction != queen.Faction)
            {
                host.SetFaction(queen.Faction);
            }

            AddOverseerRelation(queen, host);
            queen.mechanitor.Notify_BandwidthChanged();
            return true;
        }

        public static void RemoveOverseerRelationSilently(Pawn subject)
        {
            Pawn overseer = MechanitorUtility.GetOverseer(subject);
            if (overseer == null)
            {
                return;
            }

            overseer.mechanitor?.UnassignPawnFromAnyControlGroup(subject);
            overseer.relations?.TryRemoveDirectRelation(PawnRelationDefOf.Overseer, subject);
            overseer.mechanitor?.Notify_BandwidthChanged();
        }

        public static void AddOverseerRelation(Pawn overseer, Pawn subject)
        {
            if (overseer?.relations == null || subject == null)
            {
                return;
            }

            if (!overseer.relations.DirectRelationExists(PawnRelationDefOf.Overseer, subject))
            {
                overseer.relations.AddDirectRelation(PawnRelationDefOf.Overseer, subject);
            }
        }
    }
}
