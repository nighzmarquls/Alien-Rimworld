using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class Building_BioContainer : Building_Casket, ISuspendableThingHolder, IThingHolderWithDrawnPawn
    {
        [Unsaved(false)]
        private Graphic topGraphic;

        [Unsaved(false)]
        private bool acceptingMedicalExtraction;

        [Unsaved(false)]
        private float heldPawnDrawPosY;

        public CompBioContainer BioContainerComp => GetComp<CompBioContainer>();
        public bool IsContentsSuspended => BioContainerComp?.Props.suspendContents ?? true;
        public float HeldPawnDrawPos_Y => heldPawnDrawPosY;
        public float HeldPawnBodyAngle => BioContainerComp?.DrawAngleFor(ContainedThing as Pawn) ?? 0f;
        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;
        public override bool CanOpen => Spawned && HasAnyContents;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (Faction == null && map?.IsPlayerHome == true)
            {
                SetFaction(Faction.OfPlayer);
            }
        }

        public override bool Accepts(Thing thing)
        {
            return ContainedThing == null && thing is Pawn pawn && BioContainerComp?.CanContain(pawn, acceptingMedicalExtraction) == true;
        }

        public bool TryAcceptPawn(Pawn pawn, bool medicalExtraction)
        {
            acceptingMedicalExtraction = medicalExtraction;
            try
            {
                return TryAcceptThing(pawn, allowSpecialEffects: false);
            }
            finally
            {
                acceptingMedicalExtraction = false;
            }
        }

        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            bool accepted = base.TryAcceptThing(thing, allowSpecialEffects);
            if (accepted && thing is Pawn pawn)
            {
                BioContainerComp?.Notify_Accepted(pawn, acceptingMedicalExtraction);
                pawn.GetComp<CompStealth>()?.ForceVisible();
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            }
            return accepted;
        }

        public override void EjectContents()
        {
            base.EjectContents();
            BioContainerComp?.Notify_Emptied();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Faction == Faction.OfPlayer && ContainedThing is Pawn)
            {
                Command_Action transfer = XMTContainedPawnTransferUtility.MakeTransferCommand(this);
                if (transfer != null)
                {
                    yield return transfer;
                }
            }
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
            if (phase != DrawPhase.Draw && ContainedThing is Pawn pawn)
            {
                Vector3 pawnDrawLoc = PawnDrawLoc(drawLoc, pawn);
                heldPawnDrawPosY = pawnDrawLoc.y;
                pawn.Drawer.renderer.DynamicDrawPhaseAt(phase, pawnDrawLoc, Rot4.East, neverAimWeapon: true);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Pawn pawn = ContainedThing as Pawn;
            ContainmentHarnessDrawOffsets offsets = BioContainerComp?.DrawOffsetsFor(pawn);

            // Biocontainers author their normal graphic as the back shell, opposite the harness.
            Vector3 backDrawLoc = drawLoc;
            backDrawLoc.y += offsets?.backDepthOffset ?? -0.02f;
            base.DrawAt(backDrawLoc, flip);

            if (pawn != null)
            {
                Vector3 pawnDrawLoc = PawnDrawLoc(drawLoc, pawn);
                heldPawnDrawPosY = pawnDrawLoc.y;
                if (!Spawned)
                {
                    pawn.Drawer.renderer.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, pawnDrawLoc, Rot4.East, neverAimWeapon: true);
                    pawn.Drawer.renderer.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, pawnDrawLoc, Rot4.East, neverAimWeapon: true);
                }
                pawn.Drawer.renderer.DynamicDrawPhaseAt(DrawPhase.Draw, pawnDrawLoc, Rot4.East, neverAimWeapon: true);
            }

            // gibbetCageTopGraphicData is the tube's front shell and closes the sandwich.
            topGraphic ??= def.building.gibbetCageTopGraphicData.GraphicColoredFor(this);
            topGraphic.Draw(drawLoc, Rotation, this);
        }

        private Vector3 PawnDrawLoc(Vector3 drawLoc, Pawn pawn)
        {
            ContainmentHarnessDrawOffsets offsets = BioContainerComp?.DrawOffsetsFor(pawn);
            if (offsets == null)
            {
                return drawLoc;
            }

            return drawLoc + new Vector3(
                offsets.spatialOffset.x,
                offsets.bodyDepthOffset,
                offsets.spatialOffset.y);
        }
    }
}
