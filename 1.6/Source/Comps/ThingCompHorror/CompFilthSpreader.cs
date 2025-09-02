using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;

namespace Xenomorphtype
{
    public class CompFilthSpreader : ThingComp
    {
        CompFilthSpreaderProperties Props => props as CompFilthSpreaderProperties;
        int tickCheck = 0;
        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
            {
                return;
            }
            if (Props.filth == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (Find.TickManager.TicksGame > tickCheck)
            {
                tickCheck = currentTick + Mathf.FloorToInt(Props.hourInterval * 2500);

                FilthMaker.TryMakeFilth(parent.PositionHeld, parent.MapHeld, Props.filth);
            }

        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref tickCheck, "tickCheck", 0);
        }
    }
    public class CompFilthSpreaderProperties : CompProperties
    {
        public ThingDef filth;
        public float hourInterval = 2;
        public CompFilthSpreaderProperties()
        {
            this.compClass = typeof(CompFilthSpreader);
        }

        public CompFilthSpreaderProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
