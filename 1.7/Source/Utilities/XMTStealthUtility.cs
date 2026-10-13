using RimWorld;
using System.Text;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public static class XMTStealthUtility
    {
        private const float MinimumScore = 0.0001f;

        public static StealthDetectionReport GetDetectionReport(Pawn hidden, Thing spotter, float spotRange, float brightness)
        {
            StealthDetectionReport report = new StealthDetectionReport
            {
                Hidden = hidden,
                Spotter = spotter,
                SpotRange = spotRange,
                Brightness = brightness
            };

            if (hidden == null || spotter == null || hidden.MapHeld == null || spotter.MapHeld != hidden.MapHeld)
            {
                report.BlockedReason = "invalid map or target";
                return report;
            }

            report.Distance = hidden.PositionHeld.DistanceTo(spotter.PositionHeld);
            if (report.Distance > spotRange)
            {
                report.BlockedReason = "outside detection range";
                return report;
            }

            report.Stealth = Mathf.Max(hidden.GetStatValue(XenoStatDefOf.XMT_Stealth), MinimumScore);
            report.Detection = Mathf.Max(spotter.GetStatValue(XenoStatDefOf.XMT_StealthDetection), 0f);
            report.DistanceFactor = Mathf.Clamp01(1f - (report.Distance / Mathf.Max(spotRange, 1f)));

            if (hidden.AdjacentTo8WayOrInside(spotter))
            {
                report.Adjacent = true;
                report.VisualChance = report.Detection / report.Stealth;
                report.HearingChance = report.VisualChance;
                report.FinalChance = 1f;
                report.CanDetect = true;
                return report;
            }

            Pawn pawnSpotter = spotter as Pawn;
            if (pawnSpotter != null)
            {
                report.SightCapacity = pawnSpotter.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
                report.HearingCapacity = pawnSpotter.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
                report.HasDarkvision = HasDarkvision(pawnSpotter);
            }
            else
            {
                report.SightCapacity = 1f;
                report.HearingCapacity = 0f;
            }

            report.LineOfSight = GenSight.LineOfSightToThing(spotter.PositionHeld, hidden, hidden.MapHeld);
            report.VisualBrightnessFactor = report.HasDarkvision ? 1f : Mathf.Clamp01(brightness);
            if (report.LineOfSight)
            {
                report.VisualChance = (report.Detection / report.Stealth) * report.SightCapacity * report.VisualBrightnessFactor * Mathf.Lerp(0.25f, 1f, report.DistanceFactor);
            }

            report.NoiseFactor = NoiseFactor(hidden);
            report.HearingChance = (report.Detection / report.Stealth) * report.HearingCapacity * report.NoiseFactor * report.DistanceFactor * report.DistanceFactor;
            report.FinalChance = Mathf.Clamp01(Mathf.Max(report.VisualChance, report.HearingChance));
            report.CanDetect = report.FinalChance > 0f;
            return report;
        }

        private static bool HasDarkvision(Pawn pawn)
        {
            if (pawn.genes != null && pawn.genes.HasActiveGene(ExternalDefOf.DarkVision))
            {
                return true;
            }

            return XMTUtility.IsXenomorph(pawn);
        }

        private static float NoiseFactor(Pawn hidden)
        {
            if (hidden.Downed || hidden.Dead)
            {
                return 0.25f;
            }

            if (!hidden.Awake())
            {
                return 0.15f;
            }

            if (Find.TickManager.TicksGame < hidden.LastAttackTargetTick + 300)
            {
                return 1.5f;
            }

            if (hidden.pather != null && hidden.pather.MovingNow)
            {
                return 1f;
            }

            return 0.6f;
        }
    }

    public class StealthDetectionReport
    {
        public Pawn Hidden;
        public Thing Spotter;
        public string BlockedReason;
        public float Stealth;
        public float Detection;
        public float SpotRange;
        public float Distance;
        public float DistanceFactor;
        public float Brightness;
        public float VisualBrightnessFactor;
        public float SightCapacity;
        public float HearingCapacity;
        public float NoiseFactor;
        public float VisualChance;
        public float HearingChance;
        public float FinalChance;
        public bool LineOfSight;
        public bool HasDarkvision;
        public bool Adjacent;
        public bool CanDetect;

        public string ToLogString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Stealth detection check");
            builder.AppendLine("Hidden: " + Hidden);
            builder.AppendLine("Spotter: " + Spotter);
            if (!BlockedReason.NullOrEmpty())
            {
                builder.AppendLine("Blocked: " + BlockedReason);
                return builder.ToString();
            }
            builder.AppendLine("Stealth: " + Stealth.ToString("0.###"));
            builder.AppendLine("Detection: " + Detection.ToString("0.###"));
            builder.AppendLine("Distance: " + Distance.ToString("0.##") + " / " + SpotRange.ToString("0.##") + " (factor " + DistanceFactor.ToStringPercent() + ")");
            builder.AppendLine("Brightness: " + Brightness.ToStringPercent() + " (visual factor " + VisualBrightnessFactor.ToStringPercent() + ")");
            builder.AppendLine("Sight: " + SightCapacity.ToStringPercent() + ", hearing: " + HearingCapacity.ToStringPercent() + ", noise: " + NoiseFactor.ToStringPercent());
            builder.AppendLine("Line of sight: " + LineOfSight + ", darkvision: " + HasDarkvision + ", adjacent: " + Adjacent);
            builder.AppendLine("Visual chance: " + VisualChance.ToStringPercent());
            builder.AppendLine("Hearing chance: " + HearingChance.ToStringPercent());
            builder.AppendLine("Final chance: " + FinalChance.ToStringPercent());
            return builder.ToString();
        }
    }
}
