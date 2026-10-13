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
    public class PassableRoomborder : Building_Door
    {
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (def.drawerType == DrawerType.RealtimeOnly || !Spawned)
            {
                Graphic.Draw(drawLoc, flip ? Rotation.Opposite : Rotation, this);
            }

            SilhouetteUtility.DrawGraphicSilhouette(this, drawLoc);
        }

        protected override void Tick()
        {
            openInt = true;
            holdOpenInt = true;
            base.Tick();
           
        }
    }
}
