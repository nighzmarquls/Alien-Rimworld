using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompBuildingOrganism : ThingComp
    {
        private CompProperties_BuildingOrganism Props => (CompProperties_BuildingOrganism)props;
        private int nextPlantingTick;
        private float bodySize;
        public float BodySize => bodySize;
        private Pawn Pawn => parent as Pawn;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextPlantingTick, "nextPlantingTick");
            Scribe_Values.Look(ref bodySize, "bodySize", 0f);
        }
        public void ResetPlantingCooldown() =>
            nextPlantingTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.plantingIntervalHour * 2500);

        public void InitializeSleeper(float size) => InitializeFromBuilding(size);
        public void InitializeFromBuilding(float size)
        {
            bodySize = Mathf.Max(0.01f, size);
            ResetPlantingCooldown();
            ExternalDefOf.SM_BodySizeOffset?.Worker.ClearCacheForThing(parent);
            ExternalDefOf.SM_BodySizeMultiplier?.Worker.ClearCacheForThing(parent);
            Pawn?.Drawer?.renderer?.SetAllGraphicsDirty();
        }
        public override float GetStatOffset(StatDef stat)
        {
            if (stat == ExternalDefOf.SM_BodySizeOffset && bodySize > 0f && Pawn != null)
                return bodySize - Pawn.RaceProps.baseBodySize;
            return base.GetStatOffset(stat);
        }
        public override void PostPostMake()
        {
            base.PostPostMake();
            ResetPlantingCooldown();
        }
        public override void CompTick()
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead || Props.plantingResult == null) return;
            int now = Find.TickManager.TicksGame;
            if (now < nextPlantingTick) return;
            ResetPlantingCooldown();
            if (pawn.Awake() && !pawn.Downed && now <= pawn.LastAttackTargetTick + Props.combatDelayTicks) return;
            bool suitable = false;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, Props.plantingRadius, true))
                if (cell.InBounds(pawn.Map) && cell.GetFertility(pawn.Map) >= Props.minimumFertility) { suitable = true; break; }
            if (suitable || (Props.plantWhenIncapacitated && (!pawn.Awake() || pawn.Downed)))
                XMTUtility.TransformPawnIntoThing(pawn, Props.plantingResult, out _);
        }
    }
    public class CompProperties_BuildingOrganism : CompProperties
    {
        public float plantingIntervalHour = 1f;
        public float plantingRadius = 3f;
        public float minimumFertility = 0.1f;
        public int combatDelayTicks = 2500;
        public bool plantWhenIncapacitated = true;
        public ThingDef plantingResult;
        public CompProperties_BuildingOrganism() { compClass = typeof(CompBuildingOrganism); }
        public CompProperties_BuildingOrganism(Type type) { compClass = type; }
    }
    // Compatibility names for existing defs and patches.
    public class CompAwakenedSlumberer : CompBuildingOrganism { }
    public class CompAwakenedSlumbererProperties : CompProperties_BuildingOrganism
    {
        public CompAwakenedSlumbererProperties() : base(typeof(CompAwakenedSlumberer)) { }
        public CompAwakenedSlumbererProperties(Type type) : base(type) { }
    }
}
