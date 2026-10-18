using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Xenomorphtype
{
    public sealed class PreservedPawn : IExposable
    {
        public Pawn pawn;
        public ThingDef buildingDef;
        public Pawn Pawn => pawn;
        public Thing Contents => pawn?.Dead == true ? pawn.Corpse : pawn;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Defs.Look(ref buildingDef, "buildingDef");
        }
    }

    public sealed class PawnRecoveryPair
    {
        public PawnKindDef pawnKind;
        public ThingDef buildingDef;
    }

    public class CompProperties_PawnPreservation : CompProperties
    {
        public List<PawnRecoveryPair> recovery = new List<PawnRecoveryPair>();
        public bool healWithBuilding;
        public bool extractImplantsOnDestruction;
        public CompProperties_PawnPreservation()
        {
            compClass = typeof(CompPawnPreservation);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
                yield return error;
            foreach (PawnRecoveryPair pair in recovery)
                if (pair.pawnKind?.race == null || pair.buildingDef == null)
                    yield return "Pawn preservation recovery requires a pawnKind and buildingDef.";
        }
    }

    public class CompPawnPreservation : ThingComp, IThingHolder, ISuspendableThingHolder
    {
        private ThingOwner<Thing> contents;
        private List<PreservedPawn> entries = new List<PreservedPawn>();
        private bool disposing;
        private int recoveryTick = -1;
        public CompProperties_PawnPreservation Props => (CompProperties_PawnPreservation)props;
        public IReadOnlyList<PreservedPawn> Entries => entries ??= new List<PreservedPawn>();
        public bool IsContentsSuspended => true;
        public new IThingHolder ParentHolder => parent.Spawned ? parent.Map : parent.ParentHolder;
        internal bool Disposing => disposing;
        private ThingOwner<Thing> Contents => contents ??= new ThingOwner<Thing>(this)
        {
            dontTickContents = true
        };

        public ThingOwner GetDirectlyHeldThings() => Contents;
        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, Contents);
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref contents, "preservedContents", this);
            Scribe_Collections.Look(ref entries, "preservedPawns", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                entries ??= new List<PreservedPawn>();
                Contents.dontTickContents = true;
                LongEventHandler.ExecuteWhenFinished(() => EnsureRecovery());
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            recoveryTick = Find.TickManager.TicksGame + 2;
        }

        public override void CompTick()
        {
            if (recoveryTick >= 0 && Find.TickManager.TicksGame >= recoveryTick)
            {
                recoveryTick = -1;
                EnsureRecovery();
            }

            if (Props.healWithBuilding && parent.IsHashIntervalTick(60))
                HealToBuildingHealth();
        }

        public bool Contains(Pawn pawn) => Entries.Any(entry => entry.pawn == pawn && entry.Contents?.holdingOwner == Contents);
        public bool TryPreserve(Pawn pawn, ThingDef associatedDef, bool dropApparel = true)
        {
            entries ??= new List<PreservedPawn>();
            if (pawn == null || pawn.Destroyed || pawn.Discarded || associatedDef == null)
                return false;
            if (Contains(pawn))
                return true;
            Thing thing = pawn.Dead ? pawn.Corpse : pawn;
            if (thing == null || thing.Destroyed)
                return false;
            Map map = thing.MapHeld ?? parent.MapHeld;
            IntVec3 cell = thing.MapHeld != null ? thing.PositionHeld : parent.PositionHeld;
            if (dropApparel && pawn.apparel != null && map != null)
                foreach (Apparel apparel in pawn.apparel.WornApparel.ToList())
                    if (!pawn.apparel.TryDrop(apparel, out _, cell, forbid: false))
                        return false;
            if (!pawn.Dead)
            {
                pawn.jobs?.StopAll();
                pawn.ClearAllReservations();
            }

            if (thing.Spawned)
                thing.DeSpawn();
            if (!Contents.TryAddOrTransfer(thing, false))
            {
                if (thing.holdingOwner == null && !thing.Spawned && map != null)
                    GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
                return false;
            }

            if (Find.WorldPawns?.Contains(pawn) == true)
                Find.WorldPawns.RemovePawn(pawn);
            entries.Add(new PreservedPawn { pawn = pawn, buildingDef = associatedDef });
            HiveColonistBarUtility.NotifyContentsChanged();
            return true;
        }

        internal bool TransferEntry(PreservedPawn entry, CompPawnPreservation destination)
        {
            if (entry?.Contents == null || !Contents.TryTransferToContainer(entry.Contents, destination.Contents, false))
                return false;
            entries.Remove(entry);
            destination.entries.Add(entry);
            HiveColonistBarUtility.NotifyContentsChanged();
            return true;
        }

        internal void Forget(PreservedPawn entry) => entries.Remove(entry);
        internal void RestoreEntry(PreservedPawn entry)
        {
            if (!entries.Contains(entry))
                entries.Add(entry);
        }

        public bool EnsureRecovery()
        {
            if (disposing || parent.Destroyed || (parent is XMTBase_Building building && building.SuppressTransformationEffects))
                return false;
            entries ??= new List<PreservedPawn>();
            HashSet<Pawn> seen = new HashSet<Pawn>();
            entries.RemoveAll(entry => entry.pawn == null || entry.pawn.Destroyed || entry.pawn.Discarded || entry.Contents?.holdingOwner != Contents);
            entries.RemoveAll(entry => !seen.Add(entry.pawn));
            // Recover valid owned objects even if a stale contribution reference was lost.
            foreach (Thing thing in Contents.ToList())
            {
                Pawn pawn = thing as Pawn ?? (thing as Corpse)?.InnerPawn;
                if (pawn != null && !pawn.Destroyed && !pawn.Discarded && seen.Add(pawn))
                    entries.Add(new PreservedPawn { pawn = pawn });
            }

            foreach (PreservedPawn entry in entries.Where(item => item.buildingDef == null))
            {
                List<PawnRecoveryPair> missing = MissingRecoveryPairs(entries.Where(item => item.buildingDef != null), Props.recovery);
                PawnRecoveryPair pair = missing.FirstOrDefault(item => item?.buildingDef != null && item.pawnKind == entry.pawn.kindDef) ?? missing.FirstOrDefault(item => item?.buildingDef != null);
                entry.buildingDef = pair?.buildingDef ?? parent.def;
            }

            bool complete = true;
            foreach (PawnRecoveryPair pair in MissingRecoveryPairs(entries, Props.recovery))
            {
                if (pair?.pawnKind?.race == null || pair.buildingDef == null)
                {
                    Log.ErrorOnce("[XMT] Invalid preservation recovery on " + parent.def.defName, parent.thingIDNumber ^ 71052);
                    complete = false;
                    continue;
                }

                Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(pair.pawnKind, parent.Faction));
                if (!TryPreserve(pawn, pair.buildingDef, false))
                {
                    pawn.Destroy();
                    complete = false;
                }
            }

            return complete;
        }

        // Match exact kinds first so an earlier fallback cannot steal a later pair's match.
        // A real source may have a different kind from its XML replacement, so remaining
        // contributors still satisfy one slot with the same associated building def.
        internal static List<PawnRecoveryPair> MissingRecoveryPairs(IEnumerable<PreservedPawn> existing, IEnumerable<PawnRecoveryPair> required)
        {
            List<PreservedPawn> unmatched = existing.ToList();
            List<PawnRecoveryPair> remaining = required.ToList();
            foreach (PawnRecoveryPair pair in required)
            {
                PreservedPawn match = unmatched.FirstOrDefault(entry => pair != null && entry.buildingDef == pair.buildingDef && entry.pawn?.kindDef == pair.pawnKind);
                if (match == null)
                    continue;
                unmatched.Remove(match);
                remaining.Remove(pair);
            }

            foreach (PawnRecoveryPair pair in remaining.ToList())
            {
                PreservedPawn match = unmatched.FirstOrDefault(entry => pair != null && entry.buildingDef == pair.buildingDef);
                if (match == null)
                    continue;
                unmatched.Remove(match);
                remaining.Remove(pair);
            }

            return remaining;
        }

        public void HealToBuildingHealth()
        {
            float fraction = (float)parent.HitPoints / Math.Max(1, parent.MaxHitPoints);
            foreach (PreservedPawn entry in entries.ToList())
                PreservedBuildingUtility.HealToHealthFraction(entry.pawn, fraction);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (parent is not XMTBase_Building building || !building.SuppressTransformationEffects)
                DisposeContents(null, previousMap);
        }

        public void DisposeContents(DamageInfo? damage, Map previousMap = null)
        {
            if (disposing)
                return;
            disposing = true;
            recoveryTick = -1;
            Map map = parent.MapHeld ?? previousMap;
            IntVec3 cell = parent.PositionHeld;
            try
            {
                foreach (PreservedPawn entry in entries.ToList())
                {
                    Pawn pawn = entry.pawn;
                    if (pawn == null)
                        continue;
                    if (!pawn.Dead && !pawn.Destroyed)
                    {
                        DamageInfo? pawnDamage = damage;
                        if (pawnDamage.HasValue)
                        {
                            DamageInfo value = pawnDamage.Value;
                            value.SetHitPart(null);
                            pawnDamage = value;
                        }

                        pawn.Kill(pawnDamage);
                    }

                    if (Props.extractImplantsOnDestruction)
                        PreservedBuildingUtility.ExtractImplants(pawn, cell, map);
                    Corpse corpse = pawn.Corpse;
                    if (corpse != null && !corpse.Destroyed)
                        corpse.Destroy();
                    else if (!pawn.Destroyed)
                        pawn.Destroy();
                    if (!pawn.Discarded && Find.WorldPawns != null && !Find.WorldPawns.Contains(pawn))
                        Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Decide);
                }

                entries.Clear();
                Contents.Clear();
            }
            finally
            {
                disposing = false;
            }
        }
    }
}
