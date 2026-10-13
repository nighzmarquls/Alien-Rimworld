using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class CompHorrifyThing : ThingComp
    {
        CompHorrifyThingProperties Props => props as CompHorrifyThingProperties;
        int horrorCheckTick = 0;

        public override void CompTick()
        {
            base.CompTick();
            if(!parent.Spawned)
            {
                return;
            }
            int currentTick = Find.TickManager.TicksGame;
            if (Find.TickManager.TicksGame > horrorCheckTick)
            {
                horrorCheckTick = currentTick + Props.horrifyInterval;

                IEnumerable<Pawn> victims =  from x in GenRadial.RadialDistinctThingsAround(parent.PositionHeld, parent.MapHeld, Props.horrorRadius, false).OfType<Pawn>()
                where x.def == Props.thingHorrifiedByThis select x;
                if (victims.Any())
                {
                    foreach (Pawn victim in victims)
                    {
                        if (victim.Downed)
                        {
                            continue;
                        }

                        if (victim.Awake())
                        {
                            if (Rand.Chance(0.5f))
                            {
                                victim.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, "", forced: true, forceWake: true, false);
                            }
                            else
                            {
                                victim.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee, "", forced: true, forceWake: true, false);
                            }
                        }
                    }
                }
            }
        }
    }

    public class CompHorrifyThingProperties : CompProperties
    {
        public ThingDef thingHorrifiedByThis;
        public float    horrorRadius = 4.5f;
        public int      horrifyInterval = 600;
        public CompHorrifyThingProperties()
        {
            this.compClass = typeof(CompHorrifyThing);
        }
        public CompHorrifyThingProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
