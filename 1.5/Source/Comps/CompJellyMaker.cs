using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;


namespace Xenomorphtype
{
    public class CompJellyMaker : ThingComp
    {
        public float WorkPerJelly => Props.processingWork;
        public float Efficiency => Props.conversionRate;
        CompJellyMakerProperties Props => props as CompJellyMakerProperties;

        protected Thing ingredient;

        public bool CanMakeIntoJelly(Thing thing)
        {
            if(thing == null)
            {
                return false;
            }

            return CanMakeIntoJelly(thing.def);
        }

        public bool CanMakeIntoJelly(ThingDef thing)
        {
            if (parent is Pawn pawn && XMTUtility.IsQueen(pawn))
            {
                if (pawn.genes != null)
                {
                    if (pawn.genes.HasActiveGene(XenoGeneDefOf.XMT_Chemfuel_Metabolism))
                    {
                        if(ThingDefOf.Chemfuel == thing)
                        {
                            return true;
                        }
                    }

                    if (pawn.genes.HasActiveGene(XenoGeneDefOf.XMT_Muffalo_Ruff))
                    {
                        if (ExternalDefOf.WoolMuffalo == thing)
                        {
                            return true;
                        }
                    }
                }
            }

            return Props.jellyIngredientFilter.Allows(thing);
        }
        public bool GetJellyMakingJob(out Job job)
        {
            Region localRegion = parent.GetRegion();
            if (localRegion != null)
            {
                if (parent is Pawn pawn)
                {
                    Thing ingredient = XMTUtility.SearchRegionsByFilterForFirst(localRegion, Props.jellyIngredientFilter, pawn);

                    if (ingredient != null)
                    {
                        if (pawn.needs.joy != null)
                        {
                            pawn.needs.joy.GainJoy(0.12f, InternalDefOf.NestTending);
                        }
                        pawn.Map.reservationManager.ReleaseAllForTarget(ingredient);
                        job = JobMaker.MakeJob(XenoWorkDefOf.StarbeastProduceJelly, ingredient);
                        pawn.Map.reservationManager.Reserve(pawn, job, ingredient);
                        return true;
                    }
                }
            }
            job = null;
            return false;
        }

        public float GetNutritionFromThing(Thing ingredient, float efficiency = 1f)
        {
            float output = ingredient.GetStatValue(StatDefOf.Nutrition) * XMTSettings.JellyNutritionEfficiency * ingredient.stackCount; ;
            float ingredientMass = ingredient.GetStatValue(StatDefOf.Mass) * XMTSettings.JellyMassEfficiency * ingredient.stackCount; ;

            Corpse corpse = ingredient as Corpse;
            if (corpse != null)
            {
                output = 0;
                if (corpse.GetRotStage() != RotStage.Dessicated)
                {
                    if (corpse.InnerPawn != null)
                    {
                        if (parent is Pawn pawn)
                        {
                            IEnumerable<Thing> products = corpse.ButcherProducts(pawn, efficiency);
                            foreach (Thing product in products)
                            {
                                float nutrition = product.GetStatValue(StatDefOf.Nutrition) * XMTSettings.JellyNutritionEfficiency * product.stackCount; ;
                                if (nutrition <= 0)
                                {
                                    output += (product.GetStatValue(StatDefOf.Mass) * XMTSettings.JellyMassEfficiency) * product.stackCount;
                                }
                                output += nutrition;
                            }
                        }
                        else
                        {
                            if (corpse.InnerPawn.def.race != null)
                            {
                                ThingDef meatDef = corpse.InnerPawn.def.race.meatDef;
                                if (meatDef != null)
                                {
                                    if (CanMakeIntoJelly(meatDef))
                                    {
                                        output += JellyFromDef(meatDef, corpse.InnerPawn.def.GetStatValueAbstract(StatDefOf.MeatAmount));
                                    }
                                }

                                ThingDef leatherDef = corpse.InnerPawn.def.race.leatherDef;
                                if (leatherDef != null)
                                {
                                    if (CanMakeIntoJelly(leatherDef))
                                    {
                                        output += JellyFromDef(leatherDef, corpse.InnerPawn.def.GetStatValueAbstract(StatDefOf.LeatherAmount));
                                    }
                                }
                                
                                if (corpse.InnerPawn.def.butcherProducts != null)
                                {
                                    foreach (ThingDefCountClass thingCount in corpse.InnerPawn.def.butcherProducts)
                                    {
                                        if (CanMakeIntoJelly(thingCount.stuff))
                                        {
                                            output += JellyFromDef(thingCount.stuff, thingCount.count);
                                        }
                                    }
                                }

                            }
                        }
                    }
                }
            }

            if (output <= float.Epsilon)
            {
                return ingredientMass;
            }

            return output;
        }
        public int JellyFromThing(Thing ingredient, float efficiency = 1f)
        {
            float nutrition = GetNutritionFromThing(ingredient, efficiency);

            float jellyNutrition = Props.jellyProduct.GetStatValueAbstract(StatDefOf.Nutrition);

            float jellyValue = nutrition / jellyNutrition;

            return Mathf.CeilToInt( jellyValue * efficiency * Props.conversionRate);
        }
        public int ConvertToJelly(Thing ingredient, float efficiency = 1f)
        {
            IntVec3 cell = ingredient.Position;
            Map map = ingredient.Map;

            int totalJelly = JellyFromThing(ingredient, efficiency);
            ingredient.Destroy();
            DropJelly(totalJelly, cell, map);
            return totalJelly;
        }

        public ThingDef GetJellyProduct()
        {
            Pawn pawn = parent as Pawn;

            if (pawn != null && !XMTUtility.IsQueen(pawn))
            {
                if(pawn.genes != null)
                {
                    if(pawn.genes.HasActiveGene(XenoGeneDefOf.XMT_Chemfuel_Metabolism))
                    {
                        return ThingDefOf.Chemfuel;
                    }

                    if(pawn.genes.HasActiveGene(XenoGeneDefOf.XMT_Muffalo_Ruff))
                    {
                        return ExternalDefOf.WoolMuffalo;
                    }
                }
            }

            return Props.jellyProduct;
        }

        protected void DropJelly(int stackTotal, IntVec3 cell, Map currentmap)
        {
            ThingDef Jelly = GetJellyProduct();

            XMTUtility.DropAmountThing(Jelly, stackTotal, cell, currentmap, InternalDefOf.Starbeast_Filth_Resin);
        }

        protected int JellyFromDef(ThingDef thingDef, float efficiency = 1f)
        {
            float output = thingDef.GetStatValueAbstract(StatDefOf.Nutrition) * XMTSettings.JellyNutritionEfficiency;
            float ingredientMass = thingDef.GetStatValueAbstract(StatDefOf.Mass) * XMTSettings.JellyMassEfficiency;

            if(output == 0)
            {
                output = ingredientMass;
            }

            return Mathf.CeilToInt(output* efficiency);
        }

        public int MakeJellyByDef(int count, ThingDef thingDef, IntVec3 position, Map map, float efficiency = 1f)
        {
            int total = count * JellyFromDef(thingDef, efficiency);
            DropJelly(total, position, map);
            return total;
        }
    }

    public class CompJellyMakerProperties : CompProperties
    {
        public ThingFilter         jellyIngredientFilter;
        public ThingDef            jellyProduct;
        public float               conversionRate = 0.5f;
        public float               processingWork = 20;
        public CompJellyMakerProperties()
        {
            this.compClass = typeof(CompJellyMaker);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
            jellyIngredientFilter.ResolveReferences();
        }
        public CompJellyMakerProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
