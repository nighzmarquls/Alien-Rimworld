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
        int burnTicks = 0;
        int sitTicks = 0;
        int maxBurnTicks = 3;
        int maxSitTicks = 100;
        float cleanBurnDamage = 5;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref burnTicks, "burnTicks", 0);
            Scribe_Values.Look(ref sitTicks, "sitTicks", 0);

        }
        public override void Tick()
        {
            if (GenTicks.IsTickInterval(125))
            {

                Pawn pawn = this.Position.GetFirstPawn(this.Map);

                if (pawn != null)
                {

                }

                sitTicks++;
                if (XMTUtility.DamageFloors(this.Position, this.Map))
                {
                    FleckMaker.ThrowSmoke(DrawPos, base.Map, 1);
                    burnTicks++;
                }

                if (burnTicks > maxBurnTicks || sitTicks > maxSitTicks)
                {
                    DeSpawn();
                }
            }
        }
    }
}
