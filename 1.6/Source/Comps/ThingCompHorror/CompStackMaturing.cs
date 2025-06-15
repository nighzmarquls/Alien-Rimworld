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
    public class CompStackMaturing : ThingComp
    {
        CompStackMaturingProperties Props => props as CompStackMaturingProperties;
        int tickCheck = -1;
        public override void CompTick()
        {
            base.CompTick();
            if (parent.stackCount >= Props.minStackToMature)
            {
                int currentTick = Find.TickManager.TicksGame;
                if(tickCheck < 0)
                {
                    tickCheck = currentTick + Mathf.FloorToInt(Props.maturationHours * 2500);
                }
                else if (currentTick > tickCheck)
                {
                    Thing spawnThing;

                    if(Props.pawnMaturation != null)
                    {
                        PawnGenerationRequest request = new PawnGenerationRequest(Props.pawnMaturation, null);
                        request.FixedBiologicalAge = 0;

                        spawnThing = PawnGenerator.GeneratePawn(request);
                    }
                    else
                    {
                        spawnThing = ThingMaker.MakeThing(Props.thingMaturation);
                    }

                    GenSpawn.Spawn(spawnThing,parent.PositionHeld,parent.MapHeld, WipeMode.VanishOrMoveAside);

                    parent.Destroy();
                }
            }
            else
            {
                tickCheck = -1;
            }
           

        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref tickCheck, "tickCheck", 0);
        }
    }

    public class CompStackMaturingProperties : CompProperties
    {
        public PawnKindDef  pawnMaturation;
        public ThingDef     thingMaturation;
        public float        maturationHours = 1;
        public int          minStackToMature = 100;

        public CompStackMaturingProperties()
        {
            this.compClass = typeof(CompStackMaturing);
        }

        public CompStackMaturingProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
