using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class Slumberer : XMTAnimatedBuilding
    {
        CompSlumberer slumberer => GetComp<CompSlumberer>();

        public float bodySize => slumberer.TotalBodySize();

        public override void TransformedFrom(Pawn pawn, Pawn instigator)
        {
            slumberer.SetProgenitor(pawn);
        }

        public override void TransformingInto(Pawn pawn, Pawn instigator = null)
        {
            CompBuildingOrganism awakenedSlumberer = pawn?.GetComp<CompBuildingOrganism>();
            if (awakenedSlumberer != null)
            {
                awakenedSlumberer.InitializeFromBuilding(bodySize);
            }

        }
    }
}
