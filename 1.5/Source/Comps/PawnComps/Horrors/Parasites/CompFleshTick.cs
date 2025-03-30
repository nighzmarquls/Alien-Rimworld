using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;
using Verse;
using Verse.AI;
using static HarmonyLib.Code;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    public class CompFleshTick : CompHostHunter
    {
        public override bool ShouldHunt()
        {
            if (Parent?.needs?.food != null)
            {
                if (Parent.needs.food.CurCategory == HungerCategory.Fed)
                {
                    return false;
                }
            }
            return base.ShouldHunt();
        }

        public override List<BodyPartRecord> GetTargetBodyParts(Pawn target)
        {
            return (from x in target.health.hediffSet.GetNotMissingParts()
                    where x.depth == BodyPartDepth.Outside
                    select x).ToList();
        }
    }
}
