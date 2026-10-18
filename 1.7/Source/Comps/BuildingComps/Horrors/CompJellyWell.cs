using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    // Usable on ordinary buildings; no XMT building or animated class is required.
    public class CompLocalResourceProcessor : ThingComp
    {
        protected bool NoJellyPresent;
        protected int tickCountUp;
        public bool processedJelly => !NoJellyPresent;
        protected CompJellyMaker Maker => parent.GetComp<CompJellyMaker>();
        protected PipeSystem.CompResource Network => parent.GetComp<PipeSystem.CompResource>();
        protected CompProperties_LocalResourceProcessor Settings => (CompProperties_LocalResourceProcessor)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref tickCountUp, "tickCount", 0);
            Scribe_Values.Look(ref NoJellyPresent, "noProductPresent");
        }
        public override void CompTick()
        {
            if (!parent.Spawned || parent.Destroyed) return;
            if (++tickCountUp < Math.Max(1, Settings.processingInterval)) return;
            tickCountUp = 0;
            NoJellyPresent = true;
            if (Maker == null) return;
            PipeSystem.PipeNet pipeNet = Network?.PipeNet;
            if (pipeNet != null && pipeNet.Stored > Settings.networkRepairCost)
            {
                NoJellyPresent = false;
                if (parent.HitPoints < parent.MaxHitPoints)
                {
                    Repair(Settings.networkRepairAmount);
                    pipeNet.DrawAmongStorage(Settings.networkRepairCost, pipeNet.storages);
                }
            }
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, parent.def.specialDisplayRadius, true))
            {
                if (!cell.InBounds(parent.Map)) continue;
                foreach (Thing thing in cell.GetThingList(parent.Map).ToList())
                {
                    if (thing == parent) continue;
                    if (Maker.GetJellyProduct() == thing.def)
                    {
                        NoJellyPresent = false;
                        if (parent.HitPoints < parent.MaxHitPoints)
                        {
                            Repair(thing.stackCount * Settings.productRepairAmount);
                            thing.Destroy();
                            return;
                        }
                    }
                    else if (Maker.CanMakeIntoJelly(thing))
                    {
                        NoJellyPresent = false;
                        Repair(Maker.ConvertToJelly(thing, Maker.Efficiency));
                        return;
                    }
                }
            }
            // Environmental resources have no Thing target. Process them only after the item scan.
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, parent.def.specialDisplayRadius, true))
            {
                if (!cell.InBounds(parent.Map)) continue;
                int amount = Maker.ConvertTerrainToJelly(cell, parent.Map, Maker.Efficiency);
                if (!Maker.LastConversionSucceeded) continue;
                NoJellyPresent = false;
                Repair(amount);
                return;
            }
        }
        private void Repair(int amount)
        {
            parent.HitPoints = Math.Min(parent.MaxHitPoints, parent.HitPoints + amount);
            parent.GetComp<CompPawnPreservation>()?.HealToBuildingHealth();
        }
    }
    public class CompProperties_LocalResourceProcessor : CompProperties
    {
        public int processingInterval = 500;
        public int productRepairAmount = 5;
        public int networkRepairAmount = 50;
        public float networkRepairCost = 10f;
        public CompProperties_LocalResourceProcessor() { compClass = typeof(CompLocalResourceProcessor); }
        public CompProperties_LocalResourceProcessor(Type type) { compClass = type; }
    }

    // Existing XML and save class names remain valid.
    public class CompJellyWell : CompLocalResourceProcessor
    {
        private Pawn legacyPawn;
        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars || legacyPawn != null)
                Scribe_Deep.Look(ref legacyPawn, "progenitorPawn");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                PreservedBuildingUtility.MigrateLegacyPawn(parent, legacyPawn);
                legacyPawn = null;
            }
        }
        public void SetProgenitor(Pawn pawn) => parent.HitPoints = parent.MaxHitPoints;
        public void SetProgenitor(Thing thing)
        {
            parent.HitPoints = parent.MaxHitPoints;
            if (thing is MeatballLarder meatball)
                Maker?.MakeJellyByDef(meatball.HitPoints, meatball.GetComp<CompMeatBall>().GetMeat(),
                    parent.Position, parent.Map, Maker.Efficiency);
        }
    }
    public class CompJellywellProperties : CompProperties_LocalResourceProcessor
    {
        public int breathticks = 120;
        public float maxSizeChange = 0.005f;
        public CompJellywellProperties() : base(typeof(CompJellyWell)) { }
        public CompJellywellProperties(Type type) : base(type) { }
    }
}
