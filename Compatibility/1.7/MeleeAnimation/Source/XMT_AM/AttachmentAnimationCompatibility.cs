using AM;
using AM.Grappling;
using Verse;

namespace XMT_AM
{
    [StaticConstructorOnStartup]
    public static class AttachmentAnimationCompatibility
    {
        static AttachmentAnimationCompatibility()
        {
            // Ordinary idle/attack visuals do not register pawns with AnimRenderer.
            Xenomorphtype.AttachmentUtility.ParticipantBusyEvaluator = pawn => pawn.IsInAnimation()
                || GrabUtility.IsBeingTargetedForGrapple(pawn);
        }
    }
}
