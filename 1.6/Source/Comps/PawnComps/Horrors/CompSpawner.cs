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
        protected CompSpawnerProperties Props => props as CompSpawnerProperties;
        protected int nextSpawnTick = -1;

        protected Pawn Parent => parent as Pawn;
        public override void CompTick()
        {
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
                                IEnumerable<Thing> spawnedPawn = Parent.Map.listerThings.ThingsOfDef(Props.pawnKindSpawned.race);

                                if (spawnedPawn.Count() >= Props.maxPawnCount)
                                {
                                    return;
                                }

                                PawnGenerationRequest request = new PawnGenerationRequest(Props.pawnKindSpawned, null);
                                request.FixedBiologicalAge = 0;
                                Pawn spawn = PawnGenerator.GeneratePawn(request);
                                ApplyLineageIfSupported(spawn);
                                GenSpawn.Spawn(spawn, Parent.PositionHeld, Parent.MapHeld);
                                return;
                            }
                            if (Props.thingSpawned != null)
                            {
                                Thing spawnedThing = ThingMaker.MakeThing(Props.thingSpawned);
                                spawnedThing.stackCount = Props.spawnStackCount;
                                ApplyLineageIfSupported(spawnedThing);
                                GenSpawn.Spawn(spawnedThing, Parent.PositionHeld, Parent.MapHeld);
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
                    if (Props.pawnKindSpawned != null)
                    {
                        PawnGenerationRequest request = new PawnGenerationRequest(Props.pawnKindSpawned, null);
                        request.FixedBiologicalAge = 0;
                        Pawn spawn = PawnGenerator.GeneratePawn(request);
                        ApplyLineageIfSupported(spawn);
                        GenSpawn.Spawn(spawn, parent.PositionHeld, parent.MapHeld);
                        return;
                    }

                    if (Props.thingSpawned != null)
                    {
                        Thing spawnedThing = ThingMaker.MakeThing(Props.thingSpawned);
                        spawnedThing.stackCount = Props.spawnStackCount;
                        ApplyLineageIfSupported(spawnedThing);
                        GenSpawn.Spawn(spawnedThing, parent.PositionHeld, parent.MapHeld);
                    }
                }
            }
        }

        private void ApplyLineageIfSupported(Thing product)
        {
            if (product?.TryGetComp<CompHiveGeneHolder>() == null)
            {
                return;
            }

            HorrorGenePayload payload = BioUtility.CaptureHorrorGenePayload(parent);
            BioUtility.TryApplyHorrorGenePayload(product, payload);
        }
    }

    public class CompSpawnerProperties : CompProperties
    {
        public ThingDef thingSpawned = null;
        public PawnKindDef pawnKindSpawned = null;
        public float spawnIntervalHours = 1;
        public int   spawnStackCount = 1;
        public float foodCost = 0.25f;
        public int   maxPawnCount = 40;
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
