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
                return OvomorphLayUtility.GetAdjustedFoodCost(Parent, Props.foodCost);
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
            return Parent.needs.food != null && Parent.needs.food.CurLevel <= cost;
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
                    FeralJobUtility.ClearFeralJobReservationsForTarget(Parent.Map, target.Thing);
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
                    FeralJobUtility.ClearFeralJobReservationsForTarget(Parent.Map, target.Thing);
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
            ThingDef OvomorphDef = nextOvomorph ?? Props.OvomorphDef;
            float foodCost = OvomorphDef == Props.geneOvomorphDef ? FoodCost / 2 : FoodCost;
            Thing laidThing = OvomorphLayUtility.TryLayOvomorphWithCost(Parent, loc, OvomorphDef, foodCost);

            if (laidThing != null)
            {
                nextOvomorph = null;
            }

            return laidThing;
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
