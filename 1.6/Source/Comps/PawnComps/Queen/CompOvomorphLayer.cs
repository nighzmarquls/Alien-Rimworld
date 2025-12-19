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
    internal class CompOvomorphLayer : ThingComp
    {

        static private Texture2D OvomorphTexture => ContentFinder<Texture2D>.Get("UI/Abilities/Ovomorph");
        static private Texture2D GeneOvomorphTexture => ContentFinder<Texture2D>.Get("UI/Abilities/GeneOvomorph");
        Pawn Parent => parent as Pawn;
        CompOvomorphLayerProperties Props => props as CompOvomorphLayerProperties;

        ThingDef nextOvomorph = null;

        public void SetNextOvomorphAsGene()
        {
            if (Props.geneOvomorphDef != null)
            {
                nextOvomorph = Props.geneOvomorphDef;
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

                if(Parent.health.hediffSet.HasHediff(InternalDefOf.XMT_Enthroned))
                {
                    adjustedCost *= 0.25f;
                }

                adjustedCost = Parent.needs.food == null? adjustedCost : Mathf.Min(Parent.needs.food.MaxLevel, adjustedCost);
                return adjustedCost;
            }
        }

        public string OvomorphDescription()
        {
            string description = "XMT_LayOvomorphDescription".Translate(Props.OvomorphDef.label);
           
            if (Parent.needs.food == null || Parent.needs.food.CurLevel > FoodCost)
            {
                return description;
            }

            description += "\n" + "XMT_InsufficientNutrition".Translate(Props.OvomorphDef.label);

            return description;
        }

        public string GeneOvomorphDescription()
        {
            string description = "XMT_LayOvomorphDescription".Translate(Props.geneOvomorphDef.label); ;

            if (Parent.needs.food == null || Parent.needs.food.CurLevel > FoodCost/2)
            {
                return description;
            }

            description += "\n" + "XMT_InsufficientNutrition".Translate(Props.geneOvomorphDef.label);

            return description;
        }

        public bool CannotLayOvomorph(float cost)
        {
            if (Parent.needs.food == null || Parent.needs.food.CurLevel > cost)
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

            if (Parent.Downed)
            {
                yield break;
            }

            if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_OvoThrone))
            {
                yield break;
            }

            TargetingParameters LayOvomorphParameters = TargetingParameters.ForCell();

            LayOvomorphParameters.validator = delegate (TargetInfo target)
            {
                if (target.Cell.GetEdifice(target.Map) != null)
                {
                    return false;
                }

                return target.Map.reachability.CanReach(parent.Position, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly);
            };

            Command_Action LayOvomorph_Action = new Command_Action();
            LayOvomorph_Action.defaultLabel = "XMT_LayOvomorphLabel".Translate(Props.OvomorphDef.label);
            LayOvomorph_Action.defaultDesc = OvomorphDescription();
            LayOvomorph_Action.icon = OvomorphTexture;
            LayOvomorph_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(LayOvomorphParameters, delegate (LocalTargetInfo target)
                {
                    nextOvomorph = Props.OvomorphDef;
                    Parent.Map.reservationManager.ReleaseAllForTarget(target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_LayOvomorph, target);
                    job.count = 1;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);
                });

            };

            LayOvomorph_Action.Disabled = CannotLayOvomorph(FoodCost);
            yield return LayOvomorph_Action;

            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneStorage))
            {
                yield break;
            }

            Command_Action LayGeneOvomorph_Action = new Command_Action();
            LayGeneOvomorph_Action.defaultLabel = "XMT_LayOvomorphLabel".Translate(Props.geneOvomorphDef.label);
            LayGeneOvomorph_Action.defaultDesc = GeneOvomorphDescription();
            LayGeneOvomorph_Action.icon = GeneOvomorphTexture;
            LayGeneOvomorph_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(LayOvomorphParameters, delegate (LocalTargetInfo target)
                {
                    nextOvomorph = Props.geneOvomorphDef;
                    Parent.Map.reservationManager.ReleaseAllForTarget(target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_LayOvomorph, target);
                    job.count = 1;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);

                });

            };

            LayGeneOvomorph_Action.Disabled = CannotLayOvomorph(FoodCost/2);
            yield return LayGeneOvomorph_Action;
        }

        
        public Thing LayOvomorph( IntVec3 loc)
        {
            Map map = parent.MapHeld;
            if (loc.InBounds(map) && loc.GetEdifice(map) == null)
            {
                ThingDef OvomorphDef = nextOvomorph;

                if (OvomorphDef == null)
                {
                    OvomorphDef = Props.OvomorphDef;
                }

                Thing laidThing = GenSpawn.Spawn(OvomorphDef, loc, map, WipeMode.Vanish);

                Ovomorph Ovomorph = laidThing as Ovomorph;

                if (Ovomorph != null)
                {
                    Ovomorph.LayEgg(Parent, Parent);
                    Ovomorph.ForceProgress(0);
                }

                if (Parent.needs.food != null)
                {
                    if (OvomorphDef == Props.geneOvomorphDef)
                    {

                        Parent.needs.food.CurLevel -= FoodCost / 2;
                    }
                    else
                    {
                        Parent.needs.food.CurLevel -= FoodCost;
                    }
                }

                XMTUtility.WitnessOvomorph(loc, map, 0.1f);
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Ovomorph_Laid, Parent.Named(HistoryEventArgsNames.Doer)));
                SoundDefOf.CocoonDestroyed.PlayOneShot(new TargetInfo(loc, map));
                FilthMaker.TryMakeFilth(loc, map, InternalDefOf.Starbeast_Filth_Resin, count: 8);
                nextOvomorph = null;
                return laidThing;

            }
            return null;
        }
    }



    public class CompOvomorphLayerProperties : CompProperties
    {
        public float foodCost;
        public ThingDef OvomorphDef;
        public ThingDef geneOvomorphDef;
        public CompOvomorphLayerProperties()
        {
            this.compClass = typeof(CompOvomorphLayer);
        }

    }
}
