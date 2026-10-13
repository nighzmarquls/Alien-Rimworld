using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompAbilityOverrun : CompAbilityEffect
    {
        private static readonly Color DirectPreviewColor = new Color(0.9f, 0.05f, 0.02f, 0.75f);
        private static readonly Color PeripheralPreviewColor = new Color(1f, 0.86f, 0.05f, 0.65f);
        private static readonly MethodInfo ApplyMeleeDamageToTargetMethod = AccessTools.Method(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget");

        private new CompProperties_AbilityOverrun Props => (CompProperties_AbilityOverrun)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn queen = parent.pawn;
            if (queen?.Map == null || !target.Cell.IsValid)
            {
                return;
            }

            Map map = queen.Map;
            IntVec3 startCell = queen.Position;
            OverrunPath path = ResolveAndDamageDirectEdifices(queen, target.Cell);

            foreach (Building_Door door in path.DoorsToForceOpen)
            {
                if (!door.Destroyed)
                {
                    XMTDoorUtility.ForceHoldOpenAndOpen(door, queen);
                }
            }

            ApplyPeripheralStructureDamage(queen, path);
            ApplyPawnImpacts(queen, path);

            if (path.Destination == startCell)
            {
                return;
            }

            MovePawnOutOfDestination(path.Destination, queen);
            PawnTrampler trampler = PawnTrampler.MakeTrampler(
                Props.tramplerDef ?? InternalDefOf.XMT_QueenTrampler,
                queen,
                path.Destination,
                Props.flightTicks);
            GenSpawn.Spawn(trampler, startCell, map);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn queen = parent.pawn;
            if (queen?.Map == null || !target.Cell.IsValid || target.Cell == queen.Position)
            {
                return false;
            }

            float range = JuggernautAbilityUtility.OverrunRange(queen);
            if (!target.Cell.InBounds(queen.Map) || !target.Cell.InHorDistOf(queen.Position, range))
            {
                if (throwMessages)
                {
                    Messages.Message("OutOfRange".Translate(), queen, MessageTypeDefOf.RejectInput, false);
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

            List<IntVec3> centralPath = GeometricCentralPath(queen, target.Cell);
            BuildPawnEffectCells(queen, centralPath, out HashSet<IntVec3> directCells, out HashSet<IntVec3> peripheralCells);
            if (peripheralCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(peripheralCells.ToList(), PeripheralPreviewColor);
            }

            if (directCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(directCells.ToList(), DirectPreviewColor);
            }
        }

        private OverrunPath ResolveAndDamageDirectEdifices(Pawn queen, IntVec3 targetCell)
        {
            OverrunPath path = new OverrunPath(queen.Position);
            float baseDamage = BaseEdificeDamage(queen);
            float momentum = 1f;
            HashSet<Building> handledEdifices = new HashSet<Building>();

            foreach (IntVec3 cell in GeometricCentralPath(queen, targetCell))
            {
                OverrunStep step = new OverrunStep(cell, momentum);
                path.Steps.Add(step);

                Building edifice = cell.GetEdifice(queen.Map);
                if (edifice != null && handledEdifices.Add(edifice))
                {
                    path.DirectEdifices.Add(edifice);
                    bool wasBlocking = BlocksOverrun(edifice);
                    ApplyEdificeDamage(edifice, queen, baseDamage * momentum);

                    if (edifice.Destroyed)
                    {
                        momentum *= Props.momentumRetention;
                    }
                    else
                    {
                        if (edifice is Building_Door door && !wasBlocking)
                        {
                            path.DoorsToForceOpen.Add(door);
                        }

                        if (wasBlocking)
                        {
                            break;
                        }
                    }
                }

                path.Destination = cell;
            }

            return path;
        }

        private void ApplyPeripheralStructureDamage(Pawn queen, OverrunPath path)
        {
            Dictionary<IntVec3, float> cellDamageFactors = new Dictionary<IntVec3, float>();
            HashSet<IntVec3> centralCells = new HashSet<IntVec3>(path.Steps.Select(step => step.Cell));
            float radius = JuggernautAbilityUtility.OverrunEffectRadius(queen);

            foreach (OverrunStep step in path.Steps)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(step.Cell, radius, true))
                {
                    if (!cell.InBounds(queen.Map) || centralCells.Contains(cell))
                    {
                        continue;
                    }

                    if (!cellDamageFactors.TryGetValue(cell, out float currentFactor) || step.DamageFactor > currentFactor)
                    {
                        cellDamageFactors[cell] = step.DamageFactor;
                    }
                }
            }

            Dictionary<Building, float> structureDamage = new Dictionary<Building, float>();
            foreach (KeyValuePair<IntVec3, float> pair in cellDamageFactors)
            {
                foreach (Building building in pair.Key.GetThingList(queen.Map).OfType<Building>())
                {
                    if (building.Destroyed || path.DirectEdifices.Contains(building))
                    {
                        continue;
                    }

                    float damage = pair.Value * Props.peripheralStructureDamageFactor;
                    if (!structureDamage.TryGetValue(building, out float currentDamage) || damage > currentDamage)
                    {
                        structureDamage[building] = damage;
                    }
                }
            }

            float baseDamage = BaseEdificeDamage(queen);
            foreach (KeyValuePair<Building, float> pair in structureDamage)
            {
                ApplyEdificeDamage(pair.Key, queen, baseDamage * pair.Value);
            }
        }

        private void ApplyPawnImpacts(Pawn queen, OverrunPath path)
        {
            List<IntVec3> centralPath = path.Steps.Select(step => step.Cell).ToList();
            BuildPawnEffectCells(queen, centralPath, out HashSet<IntVec3> directCells, out HashSet<IntVec3> peripheralCells);
            HashSet<Pawn> directPawns = PawnsInCells(queen, directCells);

            foreach (Pawn pawn in directPawns)
            {
                StunPawn(pawn, queen);
                ApplyUnavoidableMelee(queen, pawn);
                ApplyTrampledHediff(pawn);
                AddTrampleLog(queen, pawn, direct: true);
            }

            foreach (Pawn pawn in PawnsInCells(queen, peripheralCells))
            {
                if (directPawns.Contains(pawn))
                {
                    continue;
                }

                StunPawn(pawn, queen);
                if (Rand.Chance(XMTUtility.GetDodgeChance(pawn, false)))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "TextMote_Dodge".Translate(), 1.9f);
                    continue;
                }

                ApplyUnavoidableMelee(queen, pawn);
                AddTrampleLog(queen, pawn, direct: false);
            }
        }

        private static void BuildPawnEffectCells(Pawn queen, IEnumerable<IntVec3> centralPath,
            out HashSet<IntVec3> directCells, out HashSet<IntVec3> peripheralCells)
        {
            directCells = new HashSet<IntVec3>();
            HashSet<IntVec3> allCells = new HashSet<IntVec3>();
            float directRadius = JuggernautAbilityUtility.OverrunDirectPawnRadius(queen);
            float fullRadius = JuggernautAbilityUtility.OverrunEffectRadius(queen);

            foreach (IntVec3 center in centralPath)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, directRadius, true))
                {
                    if (cell.InBounds(queen.Map))
                    {
                        directCells.Add(cell);
                    }
                }

                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, fullRadius, true))
                {
                    if (cell.InBounds(queen.Map))
                    {
                        allCells.Add(cell);
                    }
                }
            }

            peripheralCells = new HashSet<IntVec3>(allCells);
            peripheralCells.ExceptWith(directCells);
        }

        private static HashSet<Pawn> PawnsInCells(Pawn queen, IEnumerable<IntVec3> cells)
        {
            HashSet<Pawn> pawns = new HashSet<Pawn>();
            foreach (IntVec3 cell in cells)
            {
                foreach (Pawn pawn in cell.GetThingList(queen.Map).OfType<Pawn>())
                {
                    if (pawn != queen && !pawn.Dead)
                    {
                        pawns.Add(pawn);
                    }
                }
            }

            return pawns;
        }

        private static List<IntVec3> GeometricCentralPath(Pawn queen, IntVec3 targetCell)
        {
            List<IntVec3> cells = new List<IntVec3>();
            float range = JuggernautAbilityUtility.OverrunRange(queen);
            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(queen.Position, targetCell))
            {
                if (cell == queen.Position)
                {
                    continue;
                }

                if (!cell.InBounds(queen.Map) || !cell.InHorDistOf(queen.Position, range))
                {
                    break;
                }

                cells.Add(cell);
            }

            return cells;
        }

        private float BaseEdificeDamage(Pawn queen)
        {
            float bodySizeFactor = Mathf.Max(0f, queen.BodySize) / JuggernautAbilityUtility.ReferenceBodySize;
            float bluntArmor = Mathf.Max(0f, queen.GetStatValue(StatDefOf.ArmorRating_Blunt));
            return Mathf.Max(0f, Mathf.Round(Props.referenceEdificeDamage * bodySizeFactor * bluntArmor));
        }

        private static void ApplyEdificeDamage(Building building, Pawn queen, float amount)
        {
            if (building == null || building.Destroyed || amount <= 0f)
            {
                return;
            }

            building.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Mathf.Round(amount), 0f, -1f, queen));
        }

        private static bool BlocksOverrun(Building edifice)
        {
            if (edifice is Building_Door door)
            {
                return door.GetComp<CompPowerTrader>() != null || door.DoorPowerOn;
            }

            return edifice.def.Fillage == FillCategory.Full || edifice.def.passability == Traversability.Impassable;
        }

        private void ApplyUnavoidableMelee(Pawn queen, Pawn target)
        {
            if (queen?.meleeVerbs == null || target == null || target.Destroyed)
            {
                return;
            }

            Verb_MeleeAttackDamage verb = BestMeleeDamageVerb(queen, target);
            if (verb != null)
            {
                ApplyMeleeDamageToTargetMethod.Invoke(verb, new object[] { new LocalTargetInfo(target) });
            }
        }

        private static Verb_MeleeAttackDamage BestMeleeDamageVerb(Pawn queen, Thing target)
        {
            Verb_MeleeAttackDamage bestVerb = null;
            float bestScore = float.MinValue;
            foreach (VerbEntry entry in queen.meleeVerbs.GetUpdatedAvailableVerbsList(false))
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
            pawn?.stances?.stunner?.StunFor(Props.stunTicks, queen, addBattleLog: false, showMote: true);
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

        private static void MovePawnOutOfDestination(IntVec3 destination, Pawn queen)
        {
            Pawn pawn = destination.GetFirstPawn(queen.Map);
            if (pawn == null || pawn == queen || pawn.Dead)
            {
                return;
            }

            if (!CellFinder.TryFindRandomCellNear(destination, queen.Map, 2,
                cell => cell.InBounds(queen.Map) && cell.Standable(queen.Map) && cell.GetFirstPawn(queen.Map) == null,
                out IntVec3 newCell))
            {
                return;
            }

            pawn.DeSpawn(DestroyMode.WillReplace);
            GenSpawn.Spawn(pawn, newCell, queen.Map, WipeMode.Vanish);
        }

        private static void AddTrampleLog(Pawn queen, Pawn target, bool direct)
        {
            Find.BattleLog?.Add(new BattleLogEntry_QueenTrample(queen, target, direct));
        }

        private sealed class OverrunPath
        {
            public readonly List<OverrunStep> Steps = new List<OverrunStep>();
            public readonly HashSet<Building> DirectEdifices = new HashSet<Building>();
            public readonly List<Building_Door> DoorsToForceOpen = new List<Building_Door>();
            public IntVec3 Destination;

            public OverrunPath(IntVec3 start)
            {
                Destination = start;
            }
        }

        private readonly struct OverrunStep
        {
            public readonly IntVec3 Cell;
            public readonly float DamageFactor;

            public OverrunStep(IntVec3 cell, float damageFactor)
            {
                Cell = cell;
                DamageFactor = damageFactor;
            }
        }
    }

    public class CompProperties_AbilityOverrun : CompProperties_AbilityEffect
    {
        public HediffDef trampledHediff;
        public ThingDef tramplerDef;
        public int stunTicks = 180;
        public int flightTicks = 15;
        public float referenceEdificeDamage = 420f;
        public float momentumRetention = 0.75f;
        public float peripheralStructureDamageFactor = 0.5f;

        public CompProperties_AbilityOverrun()
        {
            compClass = typeof(CompAbilityOverrun);
        }
    }
}
