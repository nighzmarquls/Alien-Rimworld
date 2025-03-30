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
    public class CompSpawner : ThingComp
    {
        CompSpawnerProperties Props => props as CompSpawnerProperties;
        int nextSpawnTick = -1;

        Pawn Parent => parent as Pawn;
        public override void CompTick()
        {
            base.CompTick();
            if (Parent != null && Parent.Spawned)
            {
                if (Parent.Awake())
                {
                    int currentTick = Find.TickManager.TicksGame;
                    if (Find.TickManager.TicksGame > nextSpawnTick)
                    {
                        nextSpawnTick = currentTick + Mathf.CeilToInt(Props.spawnIntervalHours * 2500);

                        if(Parent?.needs?.food?.CurLevel >= Props.foodCost)
                        {
                            Parent.needs.food.CurLevel -= Props.foodCost;
                            if(Props.pawnKindSpawned != null)
                            {
                                PawnGenerationRequest request = new PawnGenerationRequest(Props.pawnKindSpawned, null);
                                request.FixedBiologicalAge = 0;
                                Pawn spawn = PawnGenerator.GeneratePawn(request);
                                GenSpawn.Spawn(spawn, Parent.PositionHeld, Parent.MapHeld);
                                return;
                            }
                            if (Props.thingSpawned != null)
                            {
                                GenSpawn.Spawn(Props.thingSpawned, Parent.PositionHeld, Parent.MapHeld);
                            }
                        }
                    }
                }
            }
            else if(parent.Spawned)
            {
                int currentTick = Find.TickManager.TicksGame;
                if (Find.TickManager.TicksGame > nextSpawnTick)
                {
                    nextSpawnTick = currentTick + Mathf.CeilToInt(Props.spawnIntervalHours * 2500);
                    if (Props.thingSpawned != null)
                    {
                        GenSpawn.Spawn(Props.thingSpawned, Parent.PositionHeld, Parent.MapHeld);
                    }
                }
            }
        }
    }

    public class CompSpawnerProperties : CompProperties
    {
        public ThingDef thingSpawned = null;
        public PawnKindDef pawnKindSpawned = null;
        public float spawnIntervalHours = 1;
        public float foodCost = 0.25f;
        public CompSpawnerProperties()
        {
            this.compClass = typeof(CompSpawner);
        }
        public CompSpawnerProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
