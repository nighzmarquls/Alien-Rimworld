using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{ 
    public class CompCloner  : CompSpawner
    {
        static private Texture2D SampleTexture => ContentFinder<Texture2D>.Get("UI/Abilities/GeneOvomorph");
        PawnKindDef sampleKindDef = null;
        GeneSet sampleGenes = null;
        int eggMaturationTicks = 2000;
        int eggProductionTicks = 2000;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref sampleKindDef, "sampleKindDef");
            Scribe_Deep.Look(ref sampleGenes, "sampleGenes");
            Scribe_Values.Look(ref eggMaturationTicks, "eggMaturationTicks");
            Scribe_Values.Look(ref eggProductionTicks, "eggProductionTicks");
        }

        public void SamplePawn(Pawn pawn)
        {
            RaceProperties race = pawn.RaceProps;
            eggMaturationTicks =    Mathf.CeilToInt(Mathf.Max(2000,race.gestationPeriodDays*30000));
            eggProductionTicks =    Mathf.CeilToInt(Mathf.Max(2000,race.gestationPeriodDays*15000));
            if (pawn.genes != null)
            {
                sampleGenes = new GeneSet();
                foreach(Gene gene in pawn.genes.Endogenes)
                {
                    sampleGenes.AddGene(gene.def);
                }
            }
            else
            {
                sampleGenes = null;
            }

            sampleKindDef = pawn.kindDef;
            int currentTick = Find.TickManager.TicksGame;
            nextSpawnTick = currentTick + Mathf.CeilToInt(eggProductionTicks);
            if (XMTSettings.LogBiohorror)
            {
                Log.Message(parent + " has sampled " + pawn + " as a " + sampleKindDef);
            }
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (Parent.Downed)
            {
                yield break;
            }

            TargetingParameters SamplePawn = TargetingParameters.ForPawns();

            SamplePawn.validator = delegate (TargetInfo target)
            {
                if (target.Cell.GetEdifice(target.Map) != null)
                {
                    return false;
                }

                if(!target.HasThing)
                {
                    return false;
                }

                if (target.Thing is Pawn testPawn)
                {
                    if (XMTUtility.IsInorganic(testPawn) || XMTUtility.IsXenomorph(testPawn))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                return target.Map.reachability.CanReach(parent.Position, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly);
            };

            Command_Action SamplePawn_Action = new Command_Action();
            SamplePawn_Action.defaultLabel = "XMT_Cloner_Sample".Translate();
            SamplePawn_Action.defaultDesc = "XMT_Cloner_Sample_Description".Translate();
            SamplePawn_Action.icon = SampleTexture;
            SamplePawn_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(SamplePawn, delegate (LocalTargetInfo target)
                {
                    Parent.Map.reservationManager.ReleaseAllForTarget(target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_SampleForClone, target);
                    job.count = 1;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);
                });

            };
            yield return SamplePawn_Action;
        }
        public override void CompTick()
        {
            if(sampleKindDef == null)
            {
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message(parent + " has no saved kind def for cloning ");
                }
                return;
            }
            int currentTick = Find.TickManager.TicksGame;
            if (Parent != null && Parent.Spawned)
            {
                if (Parent.Awake())
                {
                    if (currentTick > nextSpawnTick)
                    {
                        nextSpawnTick = currentTick + Mathf.CeilToInt(eggProductionTicks);

                        if(Parent?.needs?.food?.CurLevel >= Props.foodCost)
                        {
                            Parent.needs.food.CurLevel -= Props.foodCost;
                            if(Props.pawnKindSpawned != null)
                            {
                                PawnGenerationRequest request = new PawnGenerationRequest(Props.pawnKindSpawned, null);
                                request.FixedBiologicalAge = 0;
                                Pawn spawn = PawnGenerator.GeneratePawn(request);
                                spawn.SetFaction(Parent.Faction);
                                spawn = GenSpawn.Spawn(spawn, Parent.PositionHeld, Parent.MapHeld) as Pawn;

                                CompCloningEgg cloningEgg = spawn.GetComp<CompCloningEgg>();

                                if (XMTSettings.LogBiohorror)
                                {
                                    Log.Message(parent + " spawned a cloning egg as " + spawn + " the comp is " + cloningEgg);
                                }
                                if (cloningEgg != null)
                                {
                                    cloningEgg.SetupKindtoHatch(sampleKindDef, sampleGenes, eggMaturationTicks);
                                }
                                FilthMaker.TryMakeFilth(parent.PositionHeld, parent.MapHeld, InternalDefOf.Starbeast_Filth_Resin, count: 8);
                                return;
                            }
                        }
                    }
                }
            }
            else if(parent.Spawned)
            {
                if (currentTick > nextSpawnTick)
                {
                    nextSpawnTick = currentTick + Mathf.CeilToInt(Props.spawnIntervalHours * 2500);
                    if (Props.pawnKindSpawned != null)
                    {
                        PawnGenerationRequest request = new PawnGenerationRequest(Props.pawnKindSpawned, null);
                        request.FixedBiologicalAge = 0;
                        Pawn spawn = PawnGenerator.GeneratePawn(request);
                        spawn = GenSpawn.Spawn(spawn, parent.PositionHeld, parent.MapHeld) as Pawn;
                        CompCloningEgg cloningEgg = spawn.GetComp<CompCloningEgg>();
                        if (XMTSettings.LogBiohorror)
                        {
                            Log.Message(parent + " spawned a cloning egg as " + spawn + " the comp is " + cloningEgg);
                        }
                        if (cloningEgg != null)
                        {
                            cloningEgg.SetupKindtoHatch(sampleKindDef, sampleGenes, eggMaturationTicks);
                        }
                        FilthMaker.TryMakeFilth(parent.PositionHeld, parent.MapHeld, InternalDefOf.Starbeast_Filth_Resin, count: 8);
                        return;
                    }
                }
            }
        }
    }
}
