using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompMeatBall : CompOrganicGrowth
    {
        private float heatPushCache;
        private CompMeatBallProperties Props => (CompMeatBallProperties)props;
        internal float harvestWork => Props.harvestWork;
        public Color progenitorSkinColor => Progenitor != null ? XMTUtility.GetSkinColorFrom(Progenitor) : Origin?.skin ?? parent.def.graphic.color;
        public ThingDef GetMeat() => Progenitor?.RaceProps.meatDef ?? Origin?.meat ?? ThingDefOf.Meat_Human;
        public ThingDef GetBlood() => Progenitor?.RaceProps.BloodDef ?? Origin?.blood ?? ThingDefOf.Filth_Blood;
        protected override ThingDef FeedingFilth => ModsConfig.AnomalyActive ? ThingDefOf.Filth_TwistedFlesh : ThingDefOf.Filth_CorpseBile;
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref heatPushCache, "heatPushCache", 0f);
        }
        protected override void TickSpecial()
        {
            if (tickCountUp % 60 == 0 && heatPushCache > 0f) GenTemperature.PushHeat(parent.Position, parent.Map, heatPushCache);
            if (tickCountUp % 240 != 0) return;
            heatPushCache = 0f;
            if (parent.Position.GetTemperature(parent.Map) < Props.IdealTemperature && !parent.Position.UsesOutdoorTemperature(parent.Map))
            {
                if (NotFedThisHour && parent.GetComp<CompSteam>()?.ProtectsLarderHeating != true) parent.HitPoints = Mathf.Max(1, parent.HitPoints - 1);
                heatPushCache = bodySize * 42f;
            }
        }
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            if (parent.Spawned) DropMeat(Mathf.RoundToInt(totalDamageDealt / 2f));
        }
        public void DropMeat(int amount) => DropMeat(amount, parent.Map);
        public void DropMeat(int amount, Map map) => XMTUtility.DropAmountThing(GetMeat(), amount, parent.Position, map, GetBlood());
        public bool CanBePruned() => parent.GetComp<CompHivePrunable>()?.CanPrune == true;
        public void PruneMeatBall(Pawn pruner) => parent.GetComp<CompHivePrunable>()?.Prune(pruner);
    }
    public class CompMeatBallProperties : CompProperties_OrganicGrowth
    {
        public float harvestWork = 250f;
        public float IdealTemperature = 20f;
        // Retained XML aliases for external patches; animation now uses CompBreathing.
        public float maxSizeChange = 0.01f;
        public int breathticks = 120;
        public CompMeatBallProperties() : base(typeof(CompMeatBall)) { }
        public CompMeatBallProperties(Type type) : base(type) { }
    }
}
