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
    internal class Slumberer : XMTBase_Building
    {
        CompSlumberer slumberer;

        public float bodySize => slumberer.TotalBodySize();

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic.drawSize = slumberer.DisplaySize;
            base.DrawAt(drawLoc, flip);
            Graphic.drawSize = def.graphic.drawSize;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (slumberer == null)
            {
                slumberer = this.GetComp<CompSlumberer>();
            }

        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
        }

        public override void TransformedFrom(Pawn pawn, Pawn instigator)
        {
            slumberer.SetProgenitor(pawn);
        }
    }
}
