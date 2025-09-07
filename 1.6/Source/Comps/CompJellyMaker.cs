using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;


namespace Xenomorphtype
{
    public class CompJellyMaker : ThingComp
    {
        public float WorkPerJelly => Props.processingWork;
        public float Efficiency => Props.conversionRate;
        CompJellyMakerProperties Props => props as CompJellyMakerProperties;

        protected Thing ingredient;

        PipeSystem.CompResource _network;

        PipeSystem.CompResource network
        {
            get
            {
                if (_network == null)
                {
                    _network = parent.GetComp<PipeSystem.CompResource>();
                }

                return _network;
            }
        }

        public bool CanMakeIntoJelly(Thing thing)
        {
            if(thing == null)
            {
                return false;
            }

            if(thing is Corpse corpse)
            {
                if(corpse.GetRotStage() == RotStage.Dessicated)
                {
                    return false;
                }
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
                    Thing ingredient = XMTUtility.SearchRegionsForJellyMakable(localRegion, pawn, this);

                    if (ingredient != null)
                    {
                        job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ProduceJelly, ingredient);
                        pawn.Reserve(ingredient, job);
                        return true;
                    }
                }
            }
            job = null;
            return false;
        }

        public float GetNutritionFromThing(Thing ingredient, float efficiency = 1f)
        {
            float output = ingredient.GetStatValue(StatDefOf.Nutrition) * XMTSettings.JellyNutritionEfficiency * ingredient.stackCount;
            float ingredientMass = ingredient.GetStatValue(StatDefOf.Mass) * XMTSettings.JellyMassEfficiency * ingredient.stackCount; 
            if (ingredient is Corpse corpse)
            {
                output = 0;
                if (corpse.GetRotStage() != RotStage.Dessicated)
                {
                    if (corpse.InnerPawn != null)
                    {
                        if (parent is Pawn pawn)
                        {

                            IEnumerable<Thing> products = corpse.InnerPawn.ButcherProducts(pawn, efficiency);
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

                    CompRottable rottable = corpse.TryGetComp<CompRottable>();
                    if (rottable != null)
                    {
                        
                        float remaining = 1-((float)rottable.RotProgress / (float)rottable.PropsRot.TicksToDessicated);

                        output *= remaining;
                    }
                }
            }

            if(output == 0)
            {
                output = ingredientMass;
            }
            return output;
        }

        public int JellyFromCell(IntVec3 cell, float efficiency = 1f)
        {
            float jellyValue = 0;
            Map currentMap = parent.Map;
            if (currentMap != null)
            {
                Thing item = cell.GetFirstItem(currentMap);
                if (item != null)
                {
                    jellyValue = JellyFromThing(item, efficiency);
                }
                
                float TerrainFertility = XenoformingUtility.ValueOfTerrainOnCell(currentMap,cell, out TerrainDef degraded);

                jellyValue += (TerrainFertility * 10);
            }
            return Mathf.CeilToInt(jellyValue);
        }
        public int JellyFromThing(Thing ingredient, float efficiency = 1f)
        {
            float nutrition = GetNutritionFromThing(ingredient, efficiency);

            float jellyNutrition = Props.jellyProduct.GetStatValueAbstract(StatDefOf.Nutrition);

            float jellyValue = nutrition / jellyNutrition;

            return Mathf.CeilToInt( jellyValue * efficiency * Props.conversionRate);
        }

        public int ConvertTerrainToJelly(IntVec3 cell,Map currentMap, float efficiency = 1f)
        {
            int terrainStack = 0;
            if (XenoformingUtility.CellIsFertile(cell, currentMap))
            {
                TerrainDef terrain = cell.GetTerrain(currentMap);
                if (!terrain.affordances.Contains(InternalDefOf.Resin))
                {
                    XenoformingUtility.DegradeTerrainOnCell(currentMap, cell);
                }
            }
            return terrainStack;
        }

        public int ConvertToJelly(IntVec3 cell, float efficiency = 1f)
        {
            int droppedJelly = 0;
            Map currentMap = parent.Map;
            if(currentMap != null)
            {
                Thing item = cell.GetFirstItem(currentMap);
                if (item != null)
                {
                    return ConvertToJelly(item,efficiency);
                }
                else
                {
                    droppedJelly = ConvertTerrainToJelly(cell, currentMap, efficiency);
                }
            }

            int totalJelly = droppedJelly;
            if (network != null)
            {
                if (network.PipeNet.AvailableCapacity > 0)
                {
                    float stored = 0;
                    network.PipeNet.DistributeAmongStorage(totalJelly, out stored);
                    droppedJelly -= Mathf.CeilToInt(stored);
                }
            }

            XMTResearch.ProgressEvolutionTech(totalJelly, parent);
            DropJelly(droppedJelly, cell, currentMap);
            return totalJelly;
        }

        public int ConvertToJelly(Thing ingredient, float efficiency = 1f)
        {
            IntVec3 cell = ingredient.Position;
            Map map = ingredient.Map;

            int droppedJelly = JellyFromThing(ingredient, efficiency);
            CleanUpConvertedAmount(ingredient, efficiency);

            int totalJelly = droppedJelly;
            if(network != null)
            {
                if(network.PipeNet.AvailableCapacity > 0)
                {
                    float stored = 0;
                    network.PipeNet.DistributeAmongStorage(totalJelly, out stored);
                    droppedJelly -= Mathf.CeilToInt(stored);
                }
            }

            XMTResearch.ProgressEvolutionTech(totalJelly, parent);
            droppedJelly += ConvertTerrainToJelly(cell, map, efficiency);
            DropJelly(droppedJelly, cell, map);
            return totalJelly;
        }

        private void CleanUpConvertedAmount(Thing ingredient, float efficiency)
        {
            if(ingredient is Corpse corpse)
            {
                CompRottable rottable = corpse.TryGetComp<CompRottable>();
                if(rottable != null)
                {
                    rottable.RotProgress += (rottable.PropsRot.TicksToDessicated) * efficiency;
                }
                return;
            }

            if (efficiency >= 1f)
            {
                ingredient.Destroy();
                return;
            }

            int adjustedStack = Mathf.FloorToInt(ingredient.stackCount*efficiency);

            ingredient.stackCount = ingredient.stackCount - adjustedStack;
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

            bool geneProduct = Props.jellyProduct != Jelly;

            if (geneProduct)
            {
                int halfstack = stackTotal / 2;
                XMTUtility.DropAmountThing(Jelly, halfstack, cell, currentmap, InternalDefOf.Starbeast_Filth_Resin);
                XMTUtility.DropAmountThing(Props.jellyProduct, halfstack, cell, currentmap, InternalDefOf.Starbeast_Filth_Resin);
            }
            else
            {
                XMTUtility.DropAmountThing(Jelly, stackTotal, cell, currentmap, InternalDefOf.Starbeast_Filth_Resin);
            }
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
