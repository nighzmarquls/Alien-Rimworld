﻿using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    public class CompGeneManipulator : ThingComp
    {
        static private Texture2D geneticTexture => ContentFinder<Texture2D>.Get("UI/Abilities/AlterGenes");
        static private Texture2D mutantTexture => ContentFinder<Texture2D>.Get("UI/GeneIcons/XMT_Gene_Unknown");

        static private Texture2D consumeTexture => ContentFinder<Texture2D>.Get("UI/Abilities/ConsumeGenes");
        static private Texture2D selfTexture => ContentFinder<Texture2D>.Get("UI/Abilities/ExpressGenes");
        Pawn Parent => parent as Pawn;
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (Parent.Drafted)
            {
                yield break;
            }

            if(!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneControl))
            {
                yield break;
            }

            TargetingParameters GeneTargetParameters = new TargetingParameters();

            GeneTargetParameters.validator = delegate (TargetInfo target)
            {
                if (target.Thing == Parent)
                {
                    return false;
                }
                return BioUtility.HasAlterableGenes(target.Thing);
            };

            Command_Action GeneControl_Action = new Command_Action();
            GeneControl_Action.defaultLabel = "XMT_AlterGenesLabel".Translate();
            GeneControl_Action.defaultDesc = "XMT_AlterGenesDescription".Translate();
            GeneControl_Action.icon = geneticTexture;
            GeneControl_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(GeneTargetParameters, delegate (LocalTargetInfo target)
                {
                    Parent.Map.reservationManager.ReleaseAllForTarget(target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AlterGenes, target);
                    job.count = 1;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);

                });
                
            };

            yield return GeneControl_Action;

            TargetingParameters MutantTargetParameters = new TargetingParameters();

            MutantTargetParameters.validator = delegate (TargetInfo target)
            {
                if (target.Thing == Parent)
                {
                    return false;
                }
                return BioUtility.HasMutations(target.Thing as Pawn);
            };

            Command_Action MutantControl_Action = new Command_Action();
            MutantControl_Action.defaultLabel = "XMT_AlterMutationsLabel".Translate();
            MutantControl_Action.defaultDesc = "XMT_AlterMutationsDescription".Translate();
            MutantControl_Action.icon = mutantTexture;
            MutantControl_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(MutantTargetParameters, delegate (LocalTargetInfo target)
                {
                    Parent.Map.reservationManager.ReleaseAllForTarget(target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_MutateTarget, target);
                    job.count = 1;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);
                });
            };

            if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_MutantExpression))
            {
                yield return MutantControl_Action;
            }

            TargetingParameters CorpseParameters = new TargetingParameters();

            CorpseParameters.canTargetCorpses = true;
            CorpseParameters.canTargetItems = true;
            CorpseParameters.validator = delegate (TargetInfo target)
            {
               
                if(!target.Map.reachability.CanReach(parent.Position, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Unspecified))
                {
                    return false;
                }

               
                if (BioUtility.HasConsumableGenes(target.Thing))
                {
                    return true;
                }
                

                return false;
            };

            Command_Action GeneConsume_Action = new Command_Action();
            GeneConsume_Action.defaultLabel = "XMT_ConsumeGenesLabel".Translate();
            GeneConsume_Action.defaultDesc = "XMT_ConsumeGenesDescription".Translate();
            GeneConsume_Action.icon = consumeTexture;
            GeneConsume_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(CorpseParameters, delegate (LocalTargetInfo target)
                {
                    Parent.Map.reservationManager.ReleaseAllForTarget(target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_GeneDevour, target.Thing);
                    job.count = 1;
                    job.overeat = true;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);
                });

            };

            if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneDigestion))
            {
                yield return GeneConsume_Action;
            }

            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneSelfExpression))
            {
                yield break;
            }

            Command_Action GeneSelf_Action = new Command_Action();
            GeneSelf_Action.defaultLabel = "XMT_AlterSelfLabel".Translate();
            GeneSelf_Action.defaultDesc = "XMT_AlterSelfDescription".Translate();
            GeneSelf_Action.icon = selfTexture;
            GeneSelf_Action.action = delegate
            {
                AlterGenes(parent);
            };

            yield return GeneSelf_Action;
        }

        public void AlterGenes(Thing Target)
        {
            Find.WindowStack.Add(new Dialogue_GeneExpression(Target));
        }
    }

    public class CompGeneManipulatorProperties : CompProperties
    {
        public CompGeneManipulatorProperties()
        {
            this.compClass = typeof(CompGeneManipulator);
        }

    }
}
