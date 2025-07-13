using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;
using Verse.Sound;
using static HarmonyLib.Code;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    internal class CompOvamorphLayer : ThingComp
    {

        static private Texture2D OvamorphTexture => ContentFinder<Texture2D>.Get("UI/Abilities/Ovamorph");
        static private Texture2D GeneOvamorphTexture => ContentFinder<Texture2D>.Get("UI/Abilities/GeneOvamorph");
        Pawn Parent => parent as Pawn;
        CompOvamorphLayerProperties Props => props as CompOvamorphLayerProperties;

        ThingDef nextOvamorph = null;

        public void SetNextOvamorphAsGene()
        {
            if (Props.geneOvamorphDef != null)
            {
                nextOvamorph = Props.geneOvamorphDef;
            }
        }
        public Rot4 GetLayingFacing(IntVec3 eggspot)
        {
            IntVec3 dif = eggspot - parent.Position;
            if (dif.x < 0)
            {
                return Rot4.East;
            }
            else if (dif.x > 0)
            {
                return Rot4.West;
            }
            else if (dif.z < 0)
            {
                return Rot4.North;
            }
            else
            {
                return Rot4.South;
            }
        }
        public float FoodCost
        {
            get
            {
                float adjustedCost = Props.foodCost;
               
                if (Parent.genes != null)
                {
                    foreach (Gene gene in Parent.genes.GenesListForReading)
                    {
                        if (gene.Active)
                        {
                            if (gene.def == XenoGeneDefOf.Sterile)
                            {
                                adjustedCost *= 2;
                                continue;
                            }
                            if (gene.def == XenoGeneDefOf.Fertile)
                            {
                                adjustedCost *= 0.5f;
                                continue;
                            }
                        }
                    }
                }

                if (Parent.health.hediffSet.HasHediff(RoyalEvolutionDefOf.XMT_Fertility))
                {
                    adjustedCost *= 0.5f;
                }

                adjustedCost = Mathf.Min(Parent.needs.food.MaxLevel, adjustedCost);
                return adjustedCost;
            }
        }

        public string OvamorphDescription()
        {
            string description = "XMT_LayOvamorphDescription".Translate(Props.ovamorphDef.label);
           
            if (Parent.needs.food.CurLevel > FoodCost)
            {
                return description;
            }

            description += "\n" + "XMT_InsufficientNutrition".Translate(Props.ovamorphDef.label);

            return description;
        }

        public string GeneOvamorphDescription()
        {
            string description = "XMT_LayOvamorphDescription".Translate(Props.geneOvamorphDef.label); ;

            if (Parent.needs.food.CurLevel > FoodCost/2)
            {
                return description;
            }

            description += "\n" + "XMT_InsufficientNutrition".Translate(Props.geneOvamorphDef.label);

            return description;
        }

        public bool CannotLayOvamorph(float cost)
        {
            if (Parent.needs.food.CurLevel > cost)
            {
                return false;
            }

            return true;
        }
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

            TargetingParameters LayOvamorphParameters = TargetingParameters.ForCell();

            LayOvamorphParameters.validator = delegate (TargetInfo target)
            {
                if (target.Cell.GetEdifice(target.Map) != null)
                {
                    return false;
                }

                return target.Map.reachability.CanReach(parent.Position, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly);
            };

            Command_Action LayOvamorph_Action = new Command_Action();
            LayOvamorph_Action.defaultLabel = "XMT_LayOvamorphLabel".Translate(Props.ovamorphDef.label);
            LayOvamorph_Action.defaultDesc = OvamorphDescription();
            LayOvamorph_Action.icon = OvamorphTexture;
            LayOvamorph_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(LayOvamorphParameters, delegate (LocalTargetInfo target)
                {
                    nextOvamorph = Props.ovamorphDef;
                    Parent.Map.reservationManager.ReleaseAllForTarget(target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.StarbeastLayOvamorph, target);
                    job.count = 1;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);

                });

            };

            LayOvamorph_Action.Disabled = CannotLayOvamorph(FoodCost);
            yield return LayOvamorph_Action;

            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneStorage))
            {
                yield break;
            }

            Command_Action LayGeneOvamorph_Action = new Command_Action();
            LayGeneOvamorph_Action.defaultLabel = "XMT_LayOvamorphLabel".Translate(Props.geneOvamorphDef.label);
            LayGeneOvamorph_Action.defaultDesc = GeneOvamorphDescription();
            LayGeneOvamorph_Action.icon = GeneOvamorphTexture;
            LayGeneOvamorph_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(LayOvamorphParameters, delegate (LocalTargetInfo target)
                {
                    nextOvamorph = Props.geneOvamorphDef;
                    Parent.Map.reservationManager.ReleaseAllForTarget(target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.StarbeastLayOvamorph, target);
                    job.count = 1;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);

                });

            };

            LayGeneOvamorph_Action.Disabled = CannotLayOvamorph(FoodCost/2);
            yield return LayGeneOvamorph_Action;
        }
        
        public Thing LayOvamorph( IntVec3 loc)
        {
            Map map = parent.Map;
            if (loc.InBounds(map) && loc.GetEdifice(map) == null)
            {
                ThingDef ovamorphDef = nextOvamorph;

                if (ovamorphDef == null)
                {
                    ovamorphDef = Props.ovamorphDef;
                }

                Thing laidThing = GenSpawn.Spawn(ovamorphDef, loc, map, WipeMode.Vanish);

                Ovamorph ovamorph = laidThing as Ovamorph;

                if (ovamorph != null)
                {
                    ovamorph.LayEgg(Parent, Parent);
                }

                if (ovamorphDef == Props.geneOvamorphDef)
                {
                    
                    Parent.needs.food.CurLevel -= FoodCost / 2;
                }
                else
                {
                    Parent.needs.food.CurLevel -= FoodCost;
                }
                XMTUtility.WitnessOvamorph(loc, map, 0.1f);
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Ovamorph_Laid, Parent.Named(HistoryEventArgsNames.Doer)));
                SoundDefOf.CocoonDestroyed.PlayOneShot(new TargetInfo(loc, map));
                FilthMaker.TryMakeFilth(loc, map, InternalDefOf.Starbeast_Filth_Resin, count: 25);
                nextOvamorph = null;
                return laidThing;

            }
            return null;
        }
    }



    public class CompOvamorphLayerProperties : CompProperties
    {
        public float foodCost;
        public ThingDef ovamorphDef;
        public ThingDef geneOvamorphDef;
        public CompOvamorphLayerProperties()
        {
            this.compClass = typeof(CompOvamorphLayer);
        }

    }
}
