using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompAbilityTrample : CompAbilityEffect
    {
        private static readonly Color DirectPreviewColor = new Color(0.9f, 0.05f, 0.02f, 0.75f);
        private static readonly Color AdjacentPreviewColor = new Color(1f, 0.86f, 0.05f, 0.65f);
        private static readonly MethodInfo ApplyMeleeDamageToTargetMethod = AccessTools.Method(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget");

        private new CompProperties_AbilityTrample Props => (CompProperties_AbilityTrample)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn queen = parent.pawn;
            if (queen == null || queen.Map == null || !target.Cell.IsValid)
            {
                return;
            }

            TramplePath path = ResolvePath(queen, target.Cell);
            if (!path.HasMovement)
            {
                return;
            }

            Map map = queen.Map;
            IntVec3 startCell = queen.Position;

            foreach (Building_Door door in path.DoorsToForceOpen)
            {
                XMTDoorUtility.ForceHoldOpenAndOpen(door, queen);
            }

            ApplyDirectImpacts(queen, path);
            ApplyAdjacentImpacts(queen, path);
            MovePawnOutOfDestination(path.Destination, queen);

            PawnTrampler trampler = PawnTrampler.MakeTrampler(Props.tramplerDef ?? InternalDefOf.XMT_QueenTrampler, queen, path.Destination, Props.flightTicks);
            GenSpawn.Spawn(trampler, startCell, map);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn queen = parent.pawn;
            if (queen == null || queen.Map == null || !target.Cell.IsValid)
            {
                return false;
            }

            if (queen.Position.DistanceTo(target.Cell) > parent.def.verbProperties.range)
            {
                if (throwMessages)
                {
                    Messages.Message("OutOfRange".Translate(), queen, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            TramplePath path = ResolvePath(queen, target.Cell);
            if (!path.HasMovement)
            {
                if (throwMessages)
                {
                    Messages.Message("XMT_TrampleInvalid_NoPath".Translate(), queen, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn queen = parent.pawn;
            if (queen?.Map == null || !target.Cell.IsValid)
            {
                return;
            }

            TramplePath path = ResolvePath(queen, target.Cell);
            if (path.AdjacentCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(path.AdjacentCells, AdjacentPreviewColor);
            }

            if (path.DirectPreviewCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(path.DirectPreviewCells, DirectPreviewColor);
            }
        }

        private void ApplyDirectImpacts(Pawn queen, TramplePath path)
        {
            HashSet<Thing> hitThings = new HashSet<Thing>();
            foreach (IntVec3 cell in path.DirectImpactCells)
            {
                foreach (Thing thing in cell.GetThingList(queen.Map).ToList())
                {
                    if (thing == queen || !CanTrampleDirectly(thing) || !hitThings.Add(thing))
                    {
                        continue;
                    }

                    int hitCount = thing == path.Blocker ? Props.blockerHitCount : 1;
                    ApplyUnavoidableMelee(queen, thing, hitCount);

                    if (thing is Pawn pawn)
                    {
                        StunPawn(pawn, queen);
                        ApplyTrampledHediff(pawn);
                        AddTrampleLog(queen, pawn, direct: true);
                    }
                }
            }
        }

        private void ApplyAdjacentImpacts(Pawn queen, TramplePath path)
        {
            HashSet<Thing> hitThings = new HashSet<Thing>();
            foreach (IntVec3 cell in path.AdjacentCells)
            {
                foreach (Thing thing in cell.GetThingList(queen.Map).ToList())
                {
                    if (thing == queen || path.DirectThings.Contains(thing) || !CanGraze(thing) || !hitThings.Add(thing))
                    {
                        continue;
                    }

                    if (thing is Pawn pawn)
                    {
                        StunPawn(pawn, queen);
                        if (Rand.Chance(XMTUtility.GetDodgeChance(pawn, false)))
                        {
                            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "TextMote_Dodge".Translate(), 1.9f);
                            continue;
                        }

                        ApplyUnavoidableMelee(queen, pawn, 1);
                        AddTrampleLog(queen, pawn, direct: false);
                    }
                    else
                    {
                        ApplyUnavoidableMelee(queen, thing, 1);
                    }
                }
            }
        }

        private bool CanTrampleDirectly(Thing thing)
        {
            return thing is Pawn || thing.def.category == ThingCategory.Building;
        }

        private bool CanGraze(Thing thing)
        {
            return thing is Pawn || thing.def.category == ThingCategory.Building;
        }

        private void ApplyUnavoidableMelee(Pawn queen, Thing target, int hitCount)
        {
            if (queen?.meleeVerbs == null || target == null || target.Destroyed)
            {
                return;
            }

            for (int i = 0; i < hitCount; i++)
            {
                Verb_MeleeAttackDamage verb = BestMeleeDamageVerb(queen, target);
                if (verb == null)
                {
                    return;
                }

                ApplyMeleeDamageToTargetMethod.Invoke(verb, new object[] { new LocalTargetInfo(target) });
            }
        }

        private Verb_MeleeAttackDamage BestMeleeDamageVerb(Pawn queen, Thing target)
        {
            Verb_MeleeAttackDamage bestVerb = null;
            float bestScore = float.MinValue;
            List<VerbEntry> entries = queen.meleeVerbs.GetUpdatedAvailableVerbsList(false);
            foreach (VerbEntry entry in entries)
            {
                if (entry.verb is not Verb_MeleeAttackDamage verb || !verb.IsStillUsableBy(queen) || !verb.IsUsableOn(target))
                {
                    continue;
                }

                float score = entry.GetSelectionWeight(target);
                if (score <= 0f)
                {
                    score = VerbUtility.InitialVerbWeight(verb, queen);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestVerb = verb;
                }
            }

            return bestVerb;
        }

        private void StunPawn(Pawn pawn, Pawn queen)
        {
            if (pawn?.stances?.stunner != null && !pawn.Dead)
            {
                pawn.stances.stunner.StunFor(Props.stunTicks, queen, addBattleLog: false, showMote: true);
            }
        }

        private void ApplyTrampledHediff(Pawn pawn)
        {
            if (pawn?.health == null || pawn.Dead)
            {
                return;
            }

            HediffDef hediffDef = Props.trampledHediff ?? InternalDefOf.XMT_Trampled;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }

            pawn.health.AddHediff(hediffDef);
            pawn.health.Notify_HediffChanged(null);
        }

        private void MovePawnOutOfDestination(IntVec3 destination, Pawn queen)
        {
            Pawn pawn = destination.GetFirstPawn(queen.Map);
            if (pawn == null || pawn == queen || pawn.Dead)
            {
                return;
            }

            if (!CellFinder.TryFindRandomCellNear(destination, queen.Map, 2, cell => CellCanReceiveDisplacedPawn(cell, queen.Map), out IntVec3 newCell))
            {
                return;
            }

            pawn.DeSpawn(DestroyMode.WillReplace);
            GenSpawn.Spawn(pawn, newCell, queen.Map, WipeMode.Vanish);
        }

        private bool CellCanReceiveDisplacedPawn(IntVec3 cell, Map map)
        {
            return cell.InBounds(map) && cell.Standable(map) && cell.GetFirstPawn(map) == null;
        }

        private void AddTrampleLog(Pawn queen, Pawn target, bool direct)
        {
            if (Find.BattleLog != null)
            {
                Find.BattleLog.Add(new BattleLogEntry_QueenTrample(queen, target, direct));
            }
        }

        private TramplePath ResolvePath(Pawn queen, IntVec3 targetCell)
        {
            TramplePath path = new TramplePath(queen.Map);
            IntVec3 previous = queen.Position;

            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(queen.Position, targetCell))
            {
                if (cell == queen.Position)
                {
                    continue;
                }

                if (!cell.InBounds(queen.Map))
                {
                    break;
                }

                Thing blocker = BlockingThingAt(cell, queen.Map);
                if (blocker != null)
                {
                    path.Blocker = blocker;
                    path.Destination = previous;
                    path.AddDirectPreviewCell(cell);
                    path.AddDirectImpactCell(cell);
                    break;
                }

                path.AddMovementCell(cell);
                previous = cell;
            }

            if (!path.Destination.IsValid)
            {
                path.Destination = previous;
            }

            path.BuildAdjacentCells();
            return path;
        }

        private Thing BlockingThingAt(IntVec3 cell, Map map)
        {
            Building edifice = cell.GetEdifice(map);
            if (edifice == null)
            {
                return null;
            }

            if (edifice is Building_Door door)
            {
                if (IsPoweredDoor(door))
                {
                    return door;
                }

                return null;
            }

            if (edifice.def.Fillage == FillCategory.Full || edifice.def.passability == Traversability.Impassable)
            {
                return edifice;
            }

            return null;
        }

        private bool IsPoweredDoor(Building_Door door)
        {
            return door.GetComp<CompPowerTrader>() != null || door.DoorPowerOn;
        }

        private class TramplePath
        {
            private readonly Map map;
            private readonly HashSet<IntVec3> directImpactSet = new HashSet<IntVec3>();
            private readonly HashSet<IntVec3> directPreviewSet = new HashSet<IntVec3>();

            public readonly List<IntVec3> MovementCells = new List<IntVec3>();
            public readonly List<IntVec3> DirectImpactCells = new List<IntVec3>();
            public readonly List<IntVec3> DirectPreviewCells = new List<IntVec3>();
            public readonly List<IntVec3> AdjacentCells = new List<IntVec3>();
            public readonly List<Building_Door> DoorsToForceOpen = new List<Building_Door>();
            public readonly HashSet<Thing> DirectThings = new HashSet<Thing>();
            public IntVec3 Destination = IntVec3.Invalid;
            public Thing Blocker;

            public bool HasMovement => MovementCells.Count > 0;

            public TramplePath(Map map)
            {
                this.map = map;
            }

            public void AddMovementCell(IntVec3 cell)
            {
                MovementCells.Add(cell);
                AddDirectPreviewCell(cell);
                AddDirectImpactCell(cell);

                if (cell.GetEdifice(map) is Building_Door door && !DoorsToForceOpen.Contains(door))
                {
                    DoorsToForceOpen.Add(door);
                }
            }

            public void AddDirectPreviewCell(IntVec3 cell)
            {
                if (directPreviewSet.Add(cell))
                {
                    DirectPreviewCells.Add(cell);
                }
            }

            public void AddDirectImpactCell(IntVec3 cell)
            {
                if (directImpactSet.Add(cell))
                {
                    DirectImpactCells.Add(cell);
                    foreach (Thing thing in cell.GetThingList(map))
                    {
                        DirectThings.Add(thing);
                    }
                }
            }

            public void BuildAdjacentCells()
            {
                HashSet<IntVec3> adjacentSet = new HashSet<IntVec3>();
                foreach (IntVec3 cell in MovementCells)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            if (x == 0 && z == 0)
                            {
                                continue;
                            }

                            IntVec3 adjacent = new IntVec3(cell.x + x, cell.y, cell.z + z);
                            if (adjacent.InBounds(map) && !directPreviewSet.Contains(adjacent) && adjacentSet.Add(adjacent))
                            {
                                AdjacentCells.Add(adjacent);
                            }
                        }
                    }
                }
            }
        }
    }

    public class CompProperties_AbilityTrample : CompProperties_AbilityEffect
    {
        public HediffDef trampledHediff;
        public ThingDef tramplerDef;
        public int stunTicks = 180;
        public int blockerHitCount = 2;
        public int flightTicks = 30;

        public CompProperties_AbilityTrample()
        {
            compClass = typeof(CompAbilityTrample);
        }
    }
}
