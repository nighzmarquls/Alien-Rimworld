using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class GeneOvomorph : XMTBase_Building
    {
        public int TimeLaid;

        private CompHiveGeneHolder geneHolder;

        static private Texture2D geneticTexture => ContentFinder<Texture2D>.Get("UI/Abilities/AlterGenes");
        List<string> tmpGeneLabelsDesc = new List<string>();
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            geneHolder = this.GetComp<CompHiveGeneHolder>();

            if (geneHolder != null)
            {
                XMTHiveUtility.AddGeneOvomorph(this, map);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref TimeLaid, "timeLaidTick");
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            XMTHiveUtility.RemoveGeneOvomorph(this, Map);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            BioUtility.SpawnJellyHorror(PositionHeld, MapHeld, 1);

            base.Destroy(mode);
        }
        public override string GetInspectString()
        {
            string description = base.GetInspectString(); ;
            string text = description;

            if (DebugSettings.godMode)
            {
                return text;
            }

            if ((!XMTUtility.QueenIsPlayer() || !XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneStorage)) &&
                !DebugSettings.godMode)
            {
                return text;
            }


            if (geneHolder == null || !geneHolder.GenesListForReading.Any())
            {
                return text;
            }


            if (!text.NullOrEmpty())
            {
                text += "\n\n";
            }

            if (!geneHolder.EffectiveTemplateName.NullOrEmpty())
            {
                text += geneHolder.EffectiveTemplateName + "\n";
            }

            tmpGeneLabelsDesc.Clear();

            for (int i = 0; i < geneHolder.GenesListForReading.Count; i++)
            {
                tmpGeneLabelsDesc.Add(geneHolder.GenesListForReading[i].label);
            }

            return text + ("Genes".Translate().CapitalizeFirst() + ":\n" + tmpGeneLabelsDesc.ToLineList("  - ", capitalizeItems: true));
        }

        public override void DrawGUIOverlay()
        {
            base.DrawGUIOverlay();

            if (geneHolder == null || geneHolder.EffectiveTemplateName.NullOrEmpty())
            {
                return;
            }

            if (!DebugSettings.godMode &&
                (!XMTUtility.QueenIsPlayer() || !XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneStorage)))
            {
                return;
            }

            Vector2 labelPosition = GenMapUI.LabelDrawPosFor(this, -0.4f);
            GenMapUI.DrawThingLabel(labelPosition, geneHolder.EffectiveTemplateName, GenMapUI.DefaultThingLabelColor);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {

            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneControl))
            {
                yield break;
            }

            TargetingParameters GeneTargetParameters = new TargetingParameters();

            GeneTargetParameters.validator = delegate (TargetInfo target)
            {
                if (target.Thing == this)
                {
                    return false;
                }

                if(target.Thing is Pawn targetPawn)
                {
                    if (targetPawn.Dead || targetPawn.Downed)
                    {
                        return false;
                    }

                    if (XMTUtility.IsQueen(targetPawn))
                    {
                        return false;
                    }

                    return BioUtility.HasAlterableGenes(target.Thing);
                }

                return false;
                
            };

            Command_Action GeneClone_Action = new Command_Action();
            GeneClone_Action.defaultLabel = "XMT_ApplyGenes".Translate();
            GeneClone_Action.defaultDesc = "XMT_ApplyGenesDesc".Translate();
            GeneClone_Action.icon = geneticTexture;
            GeneClone_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(GeneTargetParameters, delegate (LocalTargetInfo target)
                {
                    if (target.Thing is Pawn targetPawn)
                    {
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_CopyGenes, this);

                        targetPawn.jobs.StartJob(job, JobCondition.InterruptForced);
                    }

                });

            };

            yield return GeneClone_Action;
        }

        protected void RecieveGenesFrom(Pawn pawn)
        {
            HorrorGenePayload payload = BioUtility.CaptureHorrorGenePayload(pawn, inheritableOnly: true);
            if (payload != null)
            {
                geneHolder.AddGenes(payload.Genes, payload.TemplateName);
            }
        }

        public override void TransformedFrom(Pawn pawn, Pawn instigator)
        {
            RecieveGenesFrom(pawn);
            if (pawn.BodySize > 1)
            {
                int remainingBody = Mathf.FloorToInt(pawn.BodySize - 1);

                if (remainingBody > 0)
                {

                    IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(Position, remainingBody, false);

                    foreach (IntVec3 cell in cells)
                    {
                        if (remainingBody <= 0)
                        {
                            break;
                        }
                        GeneOvomorph egg = GenSpawn.Spawn(def, cell, Map, WipeMode.VanishOrMoveAside) as GeneOvomorph;


                        egg.RecieveGenesFrom(pawn);
                        remainingBody -= 1;
                    }
                }
            }
        }
    }
}
