using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    [Flags]
    public enum PheromoneProductionType
    {
        None = 0,
        Aggregation = 1,
        Reproductive = 2,
        Alarm = 4
    }

    public enum PheromoneDisplayMode
    {
        Cryptimorph,
        NaiveHuman,
        ClinicalHuman
    }

    public static class PheromoneUtility
    {
        public const float ArtificialMaxStrength = 10f;

        public static bool IsPheromoneProducer(Pawn pawn)
        {
            return pawn != null && (XMTUtility.IsXenomorph(pawn) || pawn.PheromoneProduction() != PheromoneProductionType.None);
        }

        public static bool CanExtractFrom(Pawn pawn)
        {
            return IsPheromoneProducer(pawn) && BioUtility.PawnHasEnoughForExtraction(pawn);
        }

        public static bool TryExtractGland(Pawn target, Pawn extractor, IntVec3 dropPosition, Map map)
        {
            if (!CanExtractFrom(target) || extractor == null || map == null)
            {
                return false;
            }

            BioUtility.ExtractMetabolicCostFromPawn(target);
            XMTUtility.GiveInteractionMemory(target, ThoughtDefOf.HarmedMe, extractor);

            Thing gland = ThingMaker.MakeThing(InternalDefOf.XMT_RawPheromone);
            if (!GenPlace.TryPlaceThing(gland, dropPosition, map, ThingPlaceMode.Near))
            {
                Log.Error("Could not drop a raw pheromone gland near " + dropPosition);
            }
            return true;
        }

        public static PheromoneProductionType ProductionTypeFor(Hediff hediff)
        {
            return hediff?.TryGetComp<HediffComp_PheromonePump>()?.ProductionTypes ?? PheromoneProductionType.None;
        }

        public static int CategoryCount(PheromoneProductionType types)
        {
            int count = 0;
            if ((types & PheromoneProductionType.Aggregation) != 0) count++;
            if ((types & PheromoneProductionType.Reproductive) != 0) count++;
            if ((types & PheromoneProductionType.Alarm) != 0) count++;
            return count;
        }

        public static PheromoneDisplayMode DisplayModeFor(Pawn pawn)
        {
            if (pawn != null && XMTUtility.PlayerXenosOnMap(pawn.MapHeld))
            {
                return PheromoneDisplayMode.Cryptimorph;
            }

            if (XenoGeneDefOf.XMT_CryptimorphicPheromones?.IsFinished == true)
            {
                return PheromoneDisplayMode.ClinicalHuman;
            }

            return PheromoneDisplayMode.NaiveHuman;
        }

        public static bool TryClearFirefoam(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            List<Hediff> firefoamHediffs = pawn.health.hediffSet.hediffs.FindAll(hediff => hediff.def == HediffDefOf.CoveredInFirefoam);
            for (int i = firefoamHediffs.Count - 1; i >= 0; i--)
            {
                pawn.health.RemoveHediff(firefoamHediffs[i]);
            }

            return !pawn.health.hediffSet.HasHediff(HediffDefOf.CoveredInFirefoam) &&
                pawn.Drawer?.renderer?.FirefoamOverlays?.coveredInFoam != true;
        }

        public static bool TryApplyArtificial(Pawn pawn, float aggregation, float reproductive, float alarm, float maxStrength = ArtificialMaxStrength, float alarmRadius = 5f)
        {
            if (pawn?.Info() is not CompPawnInfo info || !TryClearFirefoam(pawn))
            {
                return false;
            }

            if (aggregation > 0f)
            {
                info.ApplyFriendlyPheromone(pawn, aggregation, maxStrength);
            }
            if (reproductive > 0f)
            {
                info.ApplyLoverPheromone(pawn, reproductive, maxStrength);
            }
            if (alarm > 0f)
            {
                info.ApplyThreatPheromone(pawn, alarm, maxStrength, alarmRadius);
            }

            return true;
        }

        public static void RandomTopicalDose(out float aggregation, out float reproductive, out float alarm)
        {
            float firstCut = Rand.Value * 2f;
            float secondCut = Rand.Value * 2f;
            if (firstCut > secondCut)
            {
                (firstCut, secondCut) = (secondCut, firstCut);
            }

            aggregation = firstCut;
            reproductive = secondCut - firstCut;
            alarm = 2f - secondCut;
        }

        public static PheromoneProductionType ExposureTypes(float aggregation, float reproductive, float alarm)
        {
            PheromoneProductionType types = PheromoneProductionType.None;
            if (aggregation > 0f) types |= PheromoneProductionType.Aggregation;
            if (reproductive > 0f) types |= PheromoneProductionType.Reproductive;
            if (alarm > 0f) types |= PheromoneProductionType.Alarm;
            return types;
        }

        public static string ClinicalExposureLabel(PheromoneProductionType types)
        {
            if (CategoryCount(types) > 1)
            {
                return "XMT_Info_MixedPheromoneExposure".Translate();
            }
            if ((types & PheromoneProductionType.Aggregation) != 0)
            {
                return "XMT_Info_AggregationPheromoneExposure".Translate();
            }
            if ((types & PheromoneProductionType.Reproductive) != 0)
            {
                return "XMT_Info_ReproductivePheromoneExposure".Translate();
            }
            if ((types & PheromoneProductionType.Alarm) != 0)
            {
                return "XMT_Info_AlarmPheromoneExposure".Translate();
            }
            return "";
        }

        public static bool TryGetHumanGlandLabel(Hediff hediff, out string label)
        {
            label = null;
            PheromoneProductionType types = ProductionTypeFor(hediff);
            PheromoneDisplayMode displayMode = DisplayModeFor(hediff.pawn);
            if (types == PheromoneProductionType.None || displayMode == PheromoneDisplayMode.Cryptimorph)
            {
                return false;
            }

            if (displayMode == PheromoneDisplayMode.NaiveHuman)
            {
                label = "XMT_PheromoneGland_UnidentifiedLabel".Translate();
                return true;
            }

            if (CategoryCount(types) > 1)
            {
                label = "XMT_PheromoneGland_MixedLabel".Translate();
            }
            else if ((types & PheromoneProductionType.Aggregation) != 0)
            {
                label = "XMT_PheromoneGland_AggregationLabel".Translate();
            }
            else if ((types & PheromoneProductionType.Reproductive) != 0)
            {
                label = "XMT_PheromoneGland_ReproductiveLabel".Translate();
            }
            else
            {
                label = "XMT_PheromoneGland_AlarmLabel".Translate();
            }
            return true;
        }

        public static bool TryGetHumanGlandDescription(Hediff hediff, out string description)
        {
            description = null;
            PheromoneProductionType types = ProductionTypeFor(hediff);
            PheromoneDisplayMode displayMode = DisplayModeFor(hediff.pawn);
            if (types == PheromoneProductionType.None || displayMode == PheromoneDisplayMode.Cryptimorph)
            {
                return false;
            }

            if (displayMode == PheromoneDisplayMode.NaiveHuman)
            {
                description = "XMT_PheromoneGland_UnidentifiedDescription".Translate();
                return true;
            }

            if (CategoryCount(types) > 1)
            {
                description = "XMT_PheromoneGland_MixedDescription".Translate();
            }
            else if ((types & PheromoneProductionType.Aggregation) != 0)
            {
                description = "XMT_PheromoneGland_AggregationDescription".Translate();
            }
            else if ((types & PheromoneProductionType.Reproductive) != 0)
            {
                description = "XMT_PheromoneGland_ReproductiveDescription".Translate();
            }
            else
            {
                description = "XMT_PheromoneGland_AlarmDescription".Translate();
            }
            return true;
        }
    }
}
