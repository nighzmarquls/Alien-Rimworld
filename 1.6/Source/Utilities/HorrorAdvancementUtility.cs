using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public enum HorrorAdvancementDirection
    {
        Promote,
        Demote
    }

    public class HorrorAdvancementOption
    {
        public HorrorAdvancementDirection direction;
        public PawnKindDef pawnKind;
        public ThingDef thingDef;
        public float weight = 1f;

        public string Label => pawnKind?.LabelCap ?? thingDef?.LabelCap ?? "Unknown";
    }

    public static class HorrorAdvancementUtility
    {
        public const float BaseFoodPerBodySize = 1f;
        public const float HereditaryCapacityPerEfficiencyDoubling = 16f;
        private const float StandardMassPerBodySize = 60f;
        private const float SignificantPawnSizeIncrease = 1.25f;

        public static XMT_HorrorPawnExtension GetExtension(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                XMT_HorrorPawnExtension kindExtension = pawn.kindDef?.GetModExtension<XMT_HorrorPawnExtension>();
                if (kindExtension != null)
                {
                    return kindExtension;
                }
            }

            return thing?.def?.GetModExtension<XMT_HorrorPawnExtension>();
        }

        public static bool IsEffectiveTierZero(Pawn pawn)
        {
            return pawn != null &&
                !pawn.Dead &&
                GetExtension(pawn) == null &&
                BioUtility.HasMutations(pawn, false) &&
                pawn.def.GetModExtension<AnimalMutateForms>()?.resultCandidates.NullOrEmpty() == false;
        }

        public static List<HorrorAdvancementOption> GetOptions(Thing target, HorrorAdvancementDirection direction)
        {
            List<HorrorAdvancementOption> options = new List<HorrorAdvancementOption>();
            XMT_HorrorPawnExtension extension = GetExtension(target);
            List<HorrorAdvancementTarget> authoredTargets = direction == HorrorAdvancementDirection.Promote
                ? extension?.promotionTargets
                : extension?.demotionTargets;

            if (!authoredTargets.NullOrEmpty())
            {
                foreach (HorrorAdvancementTarget authoredTarget in authoredTargets)
                {
                    AddOption(options, direction, authoredTarget?.pawnKind, authoredTarget?.thingDef, authoredTarget?.weight ?? 1f, true);
                }
            }

            if (direction == HorrorAdvancementDirection.Promote && target is Pawn pawn && IsEffectiveTierZero(pawn))
            {
                AnimalMutateForms mutateForms = pawn.def.GetModExtension<AnimalMutateForms>();
                foreach (TransformationHorror candidate in mutateForms.resultCandidates)
                {
                    if (candidate == null)
                    {
                        continue;
                    }

                    // Existing maturation favors the pawn result when both fields name the same organism.
                    if (candidate.pawnTransformationTarget != null)
                    {
                        AddOption(options, direction, candidate.pawnTransformationTarget, null, 1f, true);
                    }
                    else
                    {
                        AddOption(options, direction, null, candidate.thingTransformationTarget, 1f, true);
                    }
                }
            }

            return options
                .OrderBy(option => option.Label)
                .ThenBy(option => option.pawnKind?.defName ?? option.thingDef?.defName)
                .ToList();
        }

        public static bool HasAnyOptions(Thing target)
        {
            return GetOptions(target, HorrorAdvancementDirection.Promote).Any() ||
                GetOptions(target, HorrorAdvancementDirection.Demote).Any();
        }

        public static AcceptanceReport CanSelectTarget(Pawn queen, Thing target)
        {
            if (queen?.GetComp<CompQueen>()?.HasActiveEvolution(RoyalEvolutionDefOf.Evo_RoyalCrown) != true)
            {
                return "XMT_HorrorAdvanceInvalid_NoCrown".Translate();
            }

            if (target == null || target.Destroyed || !target.Spawned)
            {
                return "XMT_HorrorAdvanceInvalid_Target".Translate();
            }

            if (target == queen)
            {
                return "XMT_HorrorAdvanceInvalid_Self".Translate();
            }

            if (target is Pawn pawn && pawn.Dead)
            {
                return "XMT_HorrorAdvanceInvalid_Dead".Translate(target.LabelShort);
            }

            if (queen.Map == null || target.Map != queen.Map)
            {
                return "XMT_HorrorAdvanceInvalid_Map".Translate(target.LabelShort);
            }

            if (target.HostileTo(queen))
            {
                return "XMT_HorrorAdvanceInvalid_Hostile".Translate(target.LabelShort);
            }

            if (!HasAnyOptions(target))
            {
                return "XMT_HorrorAdvanceInvalid_NoOptions".Translate(target.LabelShort);
            }

            return true;
        }

        public static AcceptanceReport CanExecute(Pawn queen, Thing target, HorrorAdvancementOption option, bool requireAdjacent = true)
        {
            AcceptanceReport targetReport = CanSelectTarget(queen, target);
            if (!targetReport.Accepted)
            {
                return targetReport;
            }

            if (option == null || !OptionStillAvailable(target, option))
            {
                return "XMT_HorrorAdvanceInvalid_Option".Translate();
            }

            if (requireAdjacent && !queen.Position.AdjacentTo8WayOrInside(target))
            {
                return "XMT_HorrorAdvanceInvalid_Adjacent".Translate(target.LabelShort);
            }

            if (option.thingDef != null && GenSpawn.WouldWipeAnythingWith(
                target.Position,
                target.Rotation,
                option.thingDef,
                target.Map,
                thing => thing != target && thing.def.category == ThingCategory.Building))
            {
                return "XMT_HorrorAdvanceInvalid_Placement".Translate(option.Label);
            }

            float cost = FoodCost(queen, target, option);
            if (cost > 0f && (queen.needs?.food == null || queen.needs.food.CurLevel + 0.0001f < cost))
            {
                return "XMT_HorrorAdvanceInvalid_Food".Translate(cost.ToString("0.##"), (queen.needs?.food?.CurLevel ?? 0f).ToString("0.##"));
            }

            return true;
        }

        public static float FoodCost(Pawn queen, Thing source, HorrorAdvancementOption option)
        {
            return Mathf.Max(0f, DestinationBodySize(source, option) - EffectiveBodySize(source)) * FoodPerBodySize(queen);
        }

        public static float FoodPerBodySize(Pawn queen)
        {
            if (queen?.def == null || XenoStatDefOf.XMT_HereditaryCapacity == null)
            {
                return BaseFoodPerBodySize;
            }

            float baselineCapacity = queen.def.GetStatValueAbstract(XenoStatDefOf.XMT_HereditaryCapacity);
            float currentCapacity = queen.GetStatValue(XenoStatDefOf.XMT_HereditaryCapacity);
            float advancement = currentCapacity - baselineCapacity;
            return BaseFoodPerBodySize * Mathf.Pow(0.5f, advancement / HereditaryCapacityPerEfficiencyDoubling);
        }

        public static float EffectiveBodySize(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                return XMTUtility.GetFinalBodySize(pawn);
            }

            if (thing is Petrolsump petrolsump)
            {
                return Mathf.Max(0f, petrolsump.bodySize);
            }

            if (thing is Slumberer slumberer)
            {
                return Mathf.Max(0f, slumberer.bodySize);
            }

            if (thing is MeatballLarder meatball)
            {
                return Mathf.Max(0f, meatball.bodySize);
            }

            return Mathf.Max(0f, (thing?.def?.BaseMass ?? 0f) / StandardMassPerBodySize);
        }

        public static float DestinationBodySize(Thing source, HorrorAdvancementOption option)
        {
            if (option?.pawnKind?.race?.race != null)
            {
                ResolvePawnDestination(source, option.pawnKind, out float bodySize, out _);
                return bodySize;
            }

            return Mathf.Max(0f, (option?.thingDef?.BaseMass ?? 0f) / StandardMassPerBodySize);
        }

        private static void ResolvePawnDestination(Thing source, PawnKindDef pawnKind, out float bodySize, out long? biologicalAgeTicks)
        {
            biologicalAgeTicks = null;
            bodySize = Mathf.Max(0f, pawnKind?.race?.race?.baseBodySize ?? 0f);
            float sourceSize = EffectiveBodySize(source);
            List<LifeStageAge> lifeStages = pawnKind?.race?.race?.lifeStageAges;
            if (sourceSize <= 0f || bodySize <= sourceSize * SignificantPawnSizeIncrease || lifeStages.NullOrEmpty())
            {
                return;
            }

            LifeStageAge closestStage = null;
            float closestSize = bodySize;
            float closestDifference = Mathf.Abs(bodySize - sourceSize);
            foreach (LifeStageAge lifeStage in lifeStages)
            {
                if (lifeStage?.def == null || lifeStage.def.bodySizeFactor <= 0f)
                {
                    continue;
                }

                float stageSize = bodySize * lifeStage.def.bodySizeFactor;
                float difference = Mathf.Abs(stageSize - sourceSize);
                if (difference + 0.0001f < closestDifference)
                {
                    closestStage = lifeStage;
                    closestSize = stageSize;
                    closestDifference = difference;
                }
            }

            if (closestStage != null)
            {
                bodySize = closestSize;
                biologicalAgeTicks = Math.Max(0L, (long)(closestStage.minAge * GenDate.TicksPerYear) + 1L);
            }
        }

        public static bool TryExecute(Pawn queen, Thing target, HorrorAdvancementOption option, out Thing result)
        {
            result = null;
            AcceptanceReport report = CanExecute(queen, target, option);
            if (!report.Accepted)
            {
                return false;
            }

            float cost = FoodCost(queen, target, option);
            ResolvePawnDestination(target, option.pawnKind, out _, out long? biologicalAgeTicks);
            bool transformed;
            if (target is Pawn pawn)
            {
                if (option.pawnKind != null)
                {
                    transformed = XMTUtility.TransformPawnIntoPawn(pawn, option.pawnKind, out Pawn pawnResult, queen, biologicalAgeTicks);
                    result = pawnResult;
                }
                else
                {
                    transformed = XMTUtility.TransformPawnIntoThing(pawn, option.thingDef, out Thing thingResult, queen);
                    result = thingResult;
                }
            }
            else if (option.pawnKind != null)
            {
                transformed = XMTUtility.TransformThingIntoPawn(target, option.pawnKind, out Pawn pawnResult, queen, biologicalAgeTicks);
                result = pawnResult;
            }
            else
            {
                transformed = XMTUtility.TransformThingIntoThing(target, option.thingDef, out Thing thingResult, queen);
                result = thingResult;
            }

            if (!transformed || result == null)
            {
                return false;
            }

            IntegrateWithQueenFaction(queen, result);

            if (cost > 0f && queen.needs?.food != null)
            {
                queen.needs.food.CurLevel = Mathf.Max(0f, queen.needs.food.CurLevel - cost);
            }

            return true;
        }

        private static void IntegrateWithQueenFaction(Pawn queen, Thing result)
        {
            if (queen?.Faction == null || result == null)
            {
                return;
            }

            if (result is Pawn pawnResult)
            {
                CompGeneManipulator manipulator = queen.GetComp<CompGeneManipulator>();
                if (manipulator != null)
                {
                    // Reuse Subjugator Crest progression: humanlikes become slaves/recruits,
                    // while animals are tamed by joining the queen's faction.
                    manipulator.ApplySubjugation(pawnResult);
                }
                else
                {
                    pawnResult.SetFaction(queen.Faction);
                }
                return;
            }

            result.SetFaction(queen.Faction);
        }

        public static bool TryChooseRandomAffordableOption(Pawn queen, Thing target, HorrorAdvancementDirection direction, out HorrorAdvancementOption option)
        {
            List<HorrorAdvancementOption> affordable = GetOptions(target, direction)
                .Where(candidate => CanExecute(queen, target, candidate, false).Accepted)
                .ToList();

            if (affordable.NullOrEmpty())
            {
                option = null;
                return false;
            }

            option = affordable.RandomElementByWeight(candidate => Mathf.Max(0.0001f, candidate.weight));
            return true;
        }

        public static HorrorAdvancementOption MakeOption(HorrorAdvancementDirection direction, PawnKindDef pawnKind, ThingDef thingDef)
        {
            return new HorrorAdvancementOption
            {
                direction = direction,
                pawnKind = pawnKind,
                thingDef = thingDef,
                weight = 1f
            };
        }

        private static bool OptionStillAvailable(Thing target, HorrorAdvancementOption option)
        {
            return GetOptions(target, option.direction).Any(candidate =>
                candidate.pawnKind == option.pawnKind && candidate.thingDef == option.thingDef);
        }

        private static void AddOption(List<HorrorAdvancementOption> options, HorrorAdvancementDirection direction, PawnKindDef pawnKind, ThingDef thingDef, float weight, bool requireHorrorDestination)
        {
            if ((pawnKind == null) == (thingDef == null))
            {
                return;
            }

            if (thingDef?.category == ThingCategory.Pawn)
            {
                return;
            }

            if (requireHorrorDestination)
            {
                XMT_HorrorPawnExtension destinationExtension = pawnKind?.GetModExtension<XMT_HorrorPawnExtension>() ??
                    pawnKind?.race?.GetModExtension<XMT_HorrorPawnExtension>() ??
                    thingDef?.GetModExtension<XMT_HorrorPawnExtension>();
                if (destinationExtension == null)
                {
                    return;
                }
            }

            if (options.Any(option => option.pawnKind == pawnKind && option.thingDef == thingDef))
            {
                return;
            }

            options.Add(new HorrorAdvancementOption
            {
                direction = direction,
                pawnKind = pawnKind,
                thingDef = thingDef,
                weight = weight > 0f ? weight : 1f
            });
        }
    }
}
