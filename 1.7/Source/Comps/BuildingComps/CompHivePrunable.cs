using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompProperties_HivePrunable : CompProperties
    {
        public float work = 250f;
        public float minBodySize = 1f;
        public int minHitPoints = 75;
        public ThingDef product;
        public int fixedYield;
        public float yieldFactor = 0.25f;
        public float healthCostPerProduct = 1f;
        public float growthCost;
        public float nestJoy = 0.01f;
        public CompProperties_HivePrunable()
        {
            compClass = typeof(CompHivePrunable);
        }
    }

    public class CompHivePrunable : ThingComp
    {
        public CompProperties_HivePrunable Props => (CompProperties_HivePrunable)props;
        private CompOrganicGrowth Growth => parent.GetComp<CompOrganicGrowth>();
        public virtual bool CanPrune => parent.Spawned && parent.HitPoints > Props.minHitPoints && (Growth == null || Growth.TotalBodySize() > Props.minBodySize) && Product != null && Yield > 0;
        public virtual ThingDef Product => Props.product ?? parent.GetComp<CompMeatBall>()?.GetMeat();
        public virtual int Yield => Product == null ? 0 : Props.fixedYield > 0 ? Props.fixedYield : Mathf.FloorToInt(Product.stackLimit * Mathf.Min(Mathf.Sqrt(Growth?.TotalBodySize() ?? 1f) * Props.yieldFactor, Mathf.Max(0f, (Growth?.TotalBodySize() ?? 2f) - Props.minBodySize)));

        public virtual bool Prune(Pawn actor)
        {
            if (!CanPrune)
                return false;
            int amount = Yield;
            XMTUtility.DropAmountThing(Product, amount, parent.Position, parent.Map);
            parent.HitPoints = Math.Max(1, parent.HitPoints - Mathf.CeilToInt(amount * Props.healthCostPerProduct));
            if (Props.growthCost > 0f)
                Growth?.ConsumePruningGrowth(Props.growthCost);
            return true;
        }
    }
}
