using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static UnityEngine.Scripting.GarbageCollector;
using Verse.Noise;

namespace Xenomorphtype
{
    public class MeatballLarder : XMTBase_Building
    {
        CompMeatBall meatBall;

        public float bodySize => meatBall.TotalBodySize();
        internal float harvestWork => meatBall.harvestWork;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic.drawSize = meatBall.DisplaySize;
            Graphic.color = meatBall.progenitorSkinColor;
            base.DrawAt(drawLoc, flip);
            Graphic.drawSize = def.graphic.drawSize;
            Graphic.color = def.graphic.color;
        }

        public bool CanBePruned()
        {
            return meatBall.CanBePruned();
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (meatBall == null)
            {
                meatBall = this.GetComp<CompMeatBall>();
            }
            HiveUtility.AddLarder(this, map);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            HiveUtility.RemoveLarder(this, this.Map);
            base.DeSpawn(mode);
        }
        
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (meatBall != null && Spawned)
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
