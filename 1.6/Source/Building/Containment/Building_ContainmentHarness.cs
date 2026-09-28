using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class Building_ContainmentHarness : Building_Casket, IThingHolderWithDrawnPawn, IThingHolderTickable
    {
        [Unsaved(false)]
        private Graphic backGraphic;

        [Unsaved(false)]
        private float heldPawnDrawPosY;

        private static Texture2D CaptureIcon => ContentFinder<Texture2D>.Get(
            ModsConfig.AnomalyActive ? "UI/Commands/CaptureEntity" : "UI/Commands/ForPrisoners");
        public CompXMT_Containment ContainmentComp => GetComp<CompXMT_Containment>();
        public float HeldPawnDrawPos_Y => heldPawnDrawPosY;
        public float HeldPawnBodyAngle => Rotation.AsAngle +
            (ContainmentComp?.Props.DrawOffsetsFor(Rotation)?.pawnRotationOffset ?? 0f);
        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundNormal;
        public bool ShouldTickContents => true;
        public override bool CanOpen => Spawned && HasAnyContents;

        public override bool Accepts(Thing thing)
        {
            return ContainedThing == null && thing is Pawn pawn &&
                ContainmentComp?.CanContain(pawn, requireDowned: false) == true;
        }

        public bool TryAcceptPawn(Pawn pawn)
        {
            return TryAcceptThing(pawn, allowSpecialEffects: false);
        }

        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            bool accepted = base.TryAcceptThing(thing, allowSpecialEffects);
            if (accepted && thing is Pawn pawn)
            {
                pawn.GetComp<CompStealth>()?.ForceVisible();
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                ContainmentComp?.Notify_ContentsChanged();
            }
            return accepted;
        }

        public override void EjectContents()
        {
            base.EjectContents();
            ContainmentComp?.Notify_ContentsChanged();
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
            if (phase != DrawPhase.Draw && ContainedThing is Pawn pawn)
            {
                Vector3 pawnDrawLoc = PawnDrawLoc(drawLoc);
                heldPawnDrawPosY = pawnDrawLoc.y;
                pawn.Drawer.renderer.DynamicDrawPhaseAt(phase, pawnDrawLoc, Rotation, neverAimWeapon: true);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            ContainmentHarnessDrawOffsets offsets = ContainmentComp?.Props.DrawOffsetsFor(Rotation);
            Vector3 backDrawLoc = drawLoc;
            backDrawLoc.y += offsets?.backDepthOffset ?? -0.02f;
            backGraphic ??= def.building.gibbetCageTopGraphicData.GraphicColoredFor(this);
            backGraphic.Draw(backDrawLoc, Rotation, this);

            if (ContainedThing is Pawn pawn)
            {
                Vector3 pawnDrawLoc = PawnDrawLoc(drawLoc);
                heldPawnDrawPosY = pawnDrawLoc.y;
                pawn.Drawer.renderer.DynamicDrawPhaseAt(DrawPhase.Draw, pawnDrawLoc, Rotation, neverAimWeapon: true);
            }

            base.DrawAt(drawLoc, flip);
        }

        private Vector3 PawnDrawLoc(Vector3 drawLoc)
        {
            ContainmentHarnessDrawOffsets offsets = ContainmentComp?.Props.DrawOffsetsFor(Rotation);
            if (offsets == null)
            {
                return drawLoc;
            }

            return drawLoc + new Vector3(
                offsets.spatialOffset.x,
                offsets.bodyDepthOffset,
                offsets.spatialOffset.y);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn))
            {
                yield return option;
            }

            if (selPawn?.Faction != Faction.OfPlayer || !selPawn.RaceProps.Humanlike || ContainedThing != null)
            {
                yield break;
            }

            FloatMenuOption enterOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_EnterContainmentHarness".Translate(), delegate
                {
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_EnterContainmentHarness, this);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }, MenuOptionPriority.Default), selPawn, this);

            if (ContainmentComp?.CanContain(selPawn, requireDowned: false) != true)
            {
                enterOption.Disabled = true;
                enterOption.tooltip = "XMT_ContainmentHarnessTargetTooLarge".Translate(selPawn.LabelShort);
            }
            yield return enterOption;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                if (gizmo is Command command && command.defaultLabel == "CommandPodEject".Translate())
                {
                    continue;
                }
                yield return gizmo;
            }

            if (Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (ContainedThing == null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "XMT_CaptureInHarness".Translate(),
                    defaultDesc = "XMT_CaptureInHarnessDesc".Translate(),
                    icon = CaptureIcon,
                    action = BeginCaptureTargeting
                };
            }
            else
            {
                Command_Action release = XMTContainedPawnTransferUtility.MakeReleaseCommand(this);
                if (release != null)
                {
                    yield return release;
                }

                Command_Action transfer = XMTContainedPawnTransferUtility.MakeTransferCommand(this);
                if (transfer != null)
                {
                    yield return transfer;
                }
            }
        }

        private void BeginCaptureTargeting()
        {
            TargetingParameters parameters = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = target => target.Thing is Pawn pawn &&
                    ContainmentComp?.CanContain(pawn, requireDowned: false) == true
            };

            Find.Targeter.BeginTargeting(parameters, target => OrderCapture(target.Pawn));
        }

        private void OrderCapture(Pawn target)
        {
            if (target == null || target.Map != Map || ContainedThing != null ||
                ContainmentComp?.CanContain(target, requireDowned: false) != true)
            {
                return;
            }

            if (target.Faction == Faction.OfPlayer && target.CanReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                target.jobs.TryTakeOrderedJob(JobMaker.MakeJob(XenoWorkDefOf.XMT_EnterContainmentHarness, this), JobTag.Misc);
                return;
            }

            List<Pawn> carriers = Map.mapPawns.FreeColonistsSpawned
                .Where(carrier => CanOrderCapture(carrier, target, out _)).ToList();
            if (carriers.Count == 0)
            {
                Messages.Message("XMT_NoHarnessCapturer".Translate(target.Named("PAWN")), target,
                    MessageTypeDefOf.RejectInput);
                return;
            }

            if (carriers.Count == 1)
            {
                StartCaptureJob(carriers[0], target);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(carriers.Select(carrier => new FloatMenuOption(
                carrier.LabelShortCap, () => StartCaptureJob(carrier, target))).ToList()));
        }

        private bool CanOrderCapture(Pawn carrier, Pawn target, out string reason)
        {
            reason = null;
            if (carrier == null || carrier.Dead || carrier.Downed ||
                !carrier.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                !carrier.CanReserveAndReach(target, PathEndMode.Touch, Danger.Deadly) ||
                !carrier.CanReserveAndReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                return false;
            }

            if (target.Downed || target.IsPrisonerOfColony)
            {
                return true;
            }

            bool canArrest = GenAI.CanBeArrestedBy(target, carrier);
            if (!canArrest)
            {
                reason = "XMT_CannotArrestForHarness".Translate(target.Named("PAWN"));
            }
            return canArrest;
        }

        private void StartCaptureJob(Pawn carrier, Pawn target)
        {
            if (!CanOrderCapture(carrier, target, out string reason))
            {
                if (!reason.NullOrEmpty())
                {
                    Messages.Message(reason, target, MessageTypeDefOf.RejectInput);
                }
                return;
            }

            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_LoadPawnIntoContainmentHarness, target, this);
            job.count = 1;
            carrier.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }
}
