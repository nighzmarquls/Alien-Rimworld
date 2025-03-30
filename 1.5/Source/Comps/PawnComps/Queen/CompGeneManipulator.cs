using RimWorld;
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

        static private Texture2D consumeTexture => ContentFinder<Texture2D>.Get("UI/Abilities/ConsumeGenes");
        static private Texture2D selfTexture => ContentFinder<Texture2D>.Get("UI/Abilities/ExpressGenes");
        Pawn Parent => parent as Pawn;
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
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
            GeneControl_Action.defaultLabel = "Alter Genes";
            GeneControl_Action.defaultDesc = "Alter Gene Expression in Target";
            GeneControl_Action.icon = geneticTexture;
            GeneControl_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(GeneTargetParameters, delegate (LocalTargetInfo target)
                {
                    Parent.Map.reservationManager.ReleaseAllForTarget(target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.AlterGenes, target);
                    job.count = 1;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);

                });
                
            };

            yield return GeneControl_Action;

            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneDigestion))
            {
                yield break;
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
            GeneConsume_Action.defaultLabel = "Consume Genes";
            GeneConsume_Action.defaultDesc = "Extract Genes into a Gene Ovamorph.";
            GeneConsume_Action.icon = consumeTexture;
            GeneConsume_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(CorpseParameters, delegate (LocalTargetInfo target)
                {
                    Parent.Map.reservationManager.ReleaseAllForTarget(target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.StarbeastGeneDevour, target.Thing);
                    job.count = 1;
                    job.overeat = true;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);
                });

            };

            yield return GeneConsume_Action;

            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneSelfExpression))
            {
                yield break;
            }

            Command_Action GeneSelf_Action = new Command_Action();
            GeneSelf_Action.defaultLabel = "Alter Self";
            GeneSelf_Action.defaultDesc = "Alter Gene Expression in self.";
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
