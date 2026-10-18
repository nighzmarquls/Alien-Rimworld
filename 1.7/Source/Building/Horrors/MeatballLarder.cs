
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class MeatballLarder : XMTAnimatedBuilding
    {
        CompMeatBall meatBall => GetComp<CompMeatBall>();
        protected override Color AnimationColor => meatBall.progenitorSkinColor;
        static private Texture2D jellyTexture => ContentFinder<Texture2D>.Get("UI/Abilities/JellyWell");
        public float bodySize => meatBall.TotalBodySize();
        internal float harvestWork => meatBall.harvestWork;

        public bool CanBePruned()
        {
            return meatBall.CanBePruned();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            XMTHiveUtility.AddLarder(this, map);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            XMTHiveUtility.RemoveLarder(this, this.Map);
            base.DeSpawn(mode);
        }
        
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!SuppressTransformationEffects && meatBall != null && Spawned)
            {
                int stackTotal = HitPoints;
                meatBall.DropMeat(stackTotal);
            }

            base.Destroy(mode);
        }

        public override void TransformedFrom(Pawn pawn, Pawn instigator)
        {
            meatBall.SetProgenitor(pawn); 
        }
    }
}
