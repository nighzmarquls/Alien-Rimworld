﻿using RimWorld;
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
        int maxSitTicks = 240;

        int burnTicks = 0;
        int maxBurnTicks = 2;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sitTicks, "sitTicks", 0);
            Scribe_Values.Look(ref burnTicks, "burnTicks", 0);

        }
        protected override void Tick()
        {
            if (GenTicks.IsTickInterval(60))
            {

                Pawn pawn = this.Position.GetFirstPawn(this.Map);

                if (pawn != null)
                {
                    XMTUtility.AcidBurn(pawn);
                    burnTicks++;
                }

                sitTicks+= 60;
                if (XMTUtility.DamageFloors(this.Position, this.Map))
                {
                    burnTicks++;

                    FleckMaker.ThrowSmoke(DrawPos, base.Map, 1);
                    if(burnTicks >= maxBurnTicks)
                    {
                        DeSpawn();
                    }
                }

                if (sitTicks >= maxSitTicks)
                {
                    DeSpawn();
                }
            }
        }
    }
}
