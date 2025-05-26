using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.Noise;

namespace Xenomorphtype
{
    public class AcidBurnFilth : Filth
    {
        int sitTicks = 0;
        int maxSitTicks = 100;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sitTicks, "sitTicks", 0);

        }
        public override void Tick()
        {
            if (GenTicks.IsTickInterval(125))
            {

                Pawn pawn = this.Position.GetFirstPawn(this.Map);

                if (pawn != null)
                {
                    XMTUtility.AcidBurn(pawn);
                }

                sitTicks++;
                if (XMTUtility.DamageFloors(this.Position, this.Map))
                {
                    FleckMaker.ThrowSmoke(DrawPos, base.Map, 1);
                    DeSpawn();
                }

                if (sitTicks > maxSitTicks)
                {
                    DeSpawn();
                }
            }
        }
    }
}
