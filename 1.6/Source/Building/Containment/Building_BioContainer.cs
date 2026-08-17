using RimWorld;
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
        public float HeldPawnBodyAngle => BioContainerComp?.Props.occupantDrawAngle ?? 0f;
        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;
        public override bool CanOpen => Spawned && HasAnyContents;

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

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
            if (phase != DrawPhase.Draw && ContainedThing is Pawn pawn)
            {
                Vector3 CameraFoward = Find.Camera.transform.forward;
                Vector3 pawnDrawLoc = drawLoc + BioContainerComp.Props.occupantDrawOffset - CameraFoward * 0.01f;
                heldPawnDrawPosY = pawnDrawLoc.y;
                pawn.Drawer.renderer.DynamicDrawPhaseAt(phase, pawnDrawLoc, Rot4.East, neverAimWeapon: true);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            Vector3 CameraFoward = Find.Camera.transform.forward;
            if (ContainedThing is Pawn pawn)
            {
                Vector3 pawnDrawLoc = drawLoc + BioContainerComp.Props.occupantDrawOffset - CameraFoward * 0.01f;
                heldPawnDrawPosY = pawnDrawLoc.y;
                if (!Spawned)
                {
                    pawn.Drawer.renderer.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, pawnDrawLoc, Rot4.East, neverAimWeapon: true);
                    pawn.Drawer.renderer.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, pawnDrawLoc, Rot4.East, neverAimWeapon: true);
                }
                pawn.Drawer.renderer.DynamicDrawPhaseAt(DrawPhase.Draw, pawnDrawLoc, Rot4.East, neverAimWeapon: true);
            }

            topGraphic ??= def.building.gibbetCageTopGraphicData.GraphicColoredFor(this);
            topGraphic.Draw(drawLoc - CameraFoward * 0.02f, Rotation, this);
        }
    }
}
