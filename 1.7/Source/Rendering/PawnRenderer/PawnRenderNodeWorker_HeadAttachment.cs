

using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class PawnRenderNodeWorker_HeadAttachment : PawnRenderNodeWorker_Eye
    {
        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 result = base.OffsetFor(node, parms, out pivot);
            if (parms.pawn.RaceProps.headPosPerRotation != null)
            {
                if (parms.pawn.RaceProps.headPosPerRotation.Count > 0) {
                    result += parms.pawn.RaceProps.headPosPerRotation[parms.facing.AsInt]*(parms.pawn.DrawSize.magnitude);
                }
            }
            else
            {
                if (TryGetWoundAnchor(node.bodyPart?.woundAnchorTag, parms, out var anchor))
                {
                    PawnDrawUtility.CalcAnchorData(parms.pawn, anchor, parms.facing, out var anchorOffset, out var _);
                    result += anchorOffset;
                }
            }
            if (parms.pawn.def == null)
            {
                Log.Message(parms.pawn + " has no def?!");
            }

            if(parms.facing == null)
            {
                Log.Message(parms.pawn + " has no facing?!");
            }
                
            result += InternalDefOf.XMT_HeadAttachmentOffsets.OffsetByRace(parms.facing, parms.pawn.def);
            
            return result;
        }
    }
}
