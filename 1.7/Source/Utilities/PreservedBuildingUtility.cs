using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public static class PreservedBuildingUtility
    {
        internal static bool IsBeingDisposed(Pawn pawn)
        {
            for (IThingHolder holder = pawn?.ParentHolder; holder != null; holder = holder.ParentHolder)
                if (holder is CompPawnPreservation preservation && preservation.Disposing) return true;
            return false;
        }
        private sealed class SplitOutput
        {
            public PreservedPawn entry;
            public Thing output;
            public CompPawnPreservation destination;
            public bool reconstructing;
        }

        // Production entry point. Debug actions and awakening use this same transaction.
        public static bool TrySplitTransformedBuilding(Thing building, out List<Thing> outputs, bool releaseAllPawns = false)
        {
            outputs = new List<Thing>();
            Thing shell = building;
            if (building is MinifiedThing minified) building = minified.InnerThing;
            CompPawnPreservation source = building?.TryGetComp<CompPawnPreservation>();
            if (building?.MapHeld == null || source == null || !source.EnsureRecovery() || source.Entries.Count == 0) return false;
            List<SplitOutput> staged = new List<SplitOutput>();
            Map map = building.MapHeld;
            IntVec3 position = building.PositionHeld;
            bool shellWasSpawned = shell.Spawned;
            try
            {
                foreach (PreservedPawn entry in source.Entries.ToList())
                {
                    if (entry.Contents == null || entry.buildingDef == null) throw new InvalidOperationException("Invalid preserved contributor.");
                    SplitOutput item = new SplitOutput { entry = entry };
                    staged.Add(item);
                    if (releaseAllPawns || entry.buildingDef == building.def)
                        item.output = entry.Contents;
                    else
                    {
                        item.reconstructing = true;
                        if (entry.buildingDef.MadeFromStuff) throw new InvalidOperationException("Reconstruction requires a non-stuff building.");
                        item.output = ThingMaker.MakeThing(entry.buildingDef);
                        item.destination = item.output.TryGetComp<CompPawnPreservation>();
                        if (item.output is not XMTBase_Building reconstructed || item.destination == null)
                            throw new InvalidOperationException("Reconstruction requires an XMT building with preservation.");
                        reconstructed.SuppressTransformationEffects = true;
                        reconstructed.SetFaction(building.Faction);
                        BioUtility.TryApplyHorrorGenePayload(reconstructed, BioUtility.CaptureHorrorGenePayload(entry.pawn));
                        // Initialized from this contributor, not the combined building's origin list.
                        reconstructed.InitializeFrom(entry.pawn);
                    }
                }

                // Make the original footprint available; rollback restores it if any output fails.
                if (shellWasSpawned) shell.DeSpawn();
                foreach (SplitOutput item in staged)
                {
                    if (item.destination == null)
                        source.GetDirectlyHeldThings().Remove(item.output);
                    if (!GenPlace.TryPlaceThing(item.output, position, map, ThingPlaceMode.Near))
                        throw new InvalidOperationException("Could not place split output.");
                    if (item.destination != null && !source.TransferEntry(item.entry, item.destination))
                        throw new InvalidOperationException("Could not transfer reconstructed contributor.");
                }

                foreach (SplitOutput item in staged)
                {
                    if (item.destination == null)
                    {
                        source.Forget(item.entry);
                        if (item.output is Pawn pawn)
                        {
                            (building as XMTBase_Building)?.TransformingInto(pawn);
                            pawn.GetComp<CompBuildingOrganism>()?.ResetPlantingCooldown();
                        }
                    }
                    if (item.output is XMTBase_Building reconstructed) reconstructed.SuppressTransformationEffects = false;
                    outputs.Add(item.output);
                }
                if (building is XMTBase_Building original) original.SuppressTransformationEffects = true;
                building.Destroy(DestroyMode.Vanish);
                if (shell != building && !shell.Destroyed) shell.Destroy(DestroyMode.Vanish);
                return true;
            }
            catch (Exception exception)
            {
                if (building.Destroyed)
                {
                    outputs = staged.Select(item => item.output).ToList();
                    Log.Error("[XMT] Split committed, but source destruction callback failed: " + exception);
                    return true;
                }
                foreach (SplitOutput item in staged.AsEnumerable().Reverse())
                {
                    if (item.reconstructing)
                    {
                        if (item.destination?.Contains(item.entry.pawn) == true) item.destination.TransferEntry(item.entry, source);
                        if (item.output is XMTBase_Building reconstructed) reconstructed.SuppressTransformationEffects = true;
                        item.output?.Destroy();
                    }
                    else if (item.output != null)
                    {
                        if (item.output.Spawned) item.output.DeSpawn();
                        source.GetDirectlyHeldThings().TryAddOrTransfer(item.output, false);
                        source.RestoreEntry(item.entry);
                    }
                }
                if (shellWasSpawned && !shell.Spawned && !shell.Destroyed) GenSpawn.Spawn(shell, position, map, shell.Rotation, WipeMode.VanishOrMoveAside);
                outputs.Clear();
                Log.Warning("[XMT] Building split rolled back: " + exception);
                return false;
            }
        }

        public static float InjuryDebt(Pawn pawn)
        {
            if (pawn?.health == null) return 0f;
            return pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(injury => injury.Severity)
                + RecoverableMissingParts(pawn).Sum(part => part.Part.def.GetMaxHealth(pawn));
        }

        private static List<Hediff_MissingPart> RecoverableMissingParts(Pawn pawn)
        {
            List<Hediff_MissingPart> missing = pawn.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().ToList();
            return missing.Where(part => !pawn.health.hediffSet.AncestorHasDirectlyAddedParts(part.Part)
                && !missing.Any(ancestor => ancestor != part && IsAncestor(ancestor.Part, part.Part))).ToList();
        }

        private static bool IsAncestor(BodyPartRecord ancestor, BodyPartRecord part)
        {
            for (BodyPartRecord current = part?.parent; current != null; current = current.parent)
                if (current == ancestor) return true;
            return false;
        }

        public static void HealToHealthFraction(Pawn pawn, float fraction)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed) return;
            fraction = Mathf.Clamp01(fraction);
            float budget = Mathf.Max(0f, InjuryDebt(pawn) - (1f - fraction) * pawn.health.LethalDamageThreshold);
            foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
            {
                float amount = Mathf.Min(budget, injury.Severity);
                if (amount > 0f) { injury.Heal(amount); budget -= amount; }
                // Suspended pawns never reach the health tick's normal removal pass.
                if (injury.Severity <= 0f) pawn.health.RemoveHediff(injury);
            }
            foreach (Hediff_MissingPart missing in RecoverableMissingParts(pawn))
            {
                float cost = missing.Part.def.GetMaxHealth(pawn);
                if (fraction < 1f && budget < cost) continue;
                foreach (Hediff_MissingPart child in pawn.health.hediffSet.hediffs.OfType<Hediff_MissingPart>()
                    .Where(part => part == missing || IsAncestor(missing.Part, part.Part)).ToList())
                    pawn.health.RemoveHediff(child);
                budget -= cost;
            }
        }

        // Explicit extraction, never called merely because a preserved pawn changes holders.
        public static int ExtractImplants(Pawn pawn, IntVec3 cell, Map map, bool removeHediffs = true)
        {
            if (pawn?.health == null || map == null || !cell.InBounds(map)) return 0;
            int count = 0;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.ToList())
            {
                ThingDef productDef = hediff.def.spawnThingOnRemoved;
                if (productDef == null) continue;
                Thing product = ThingMaker.MakeThing(productDef);
                if (!GenPlace.TryPlaceThing(product, cell, map, ThingPlaceMode.Near))
                {
                    product.Destroy();
                    continue;
                }
                if (removeHediffs) pawn.health.RemoveHediff(hediff);
                count++;
            }
            return count;
        }

        internal static void MigrateLegacyPawn(ThingWithComps building, Pawn legacy)
        {
            if (legacy == null) return;
            (building as XMTBase_Building)?.RecordOrigin(legacy);
            if (!legacy.Destroyed && !legacy.Discarded)
                building.GetComp<CompPawnPreservation>()?.TryPreserve(legacy, building.def, false);
        }
    }
}
