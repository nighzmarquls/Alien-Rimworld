using RimWorld;
using System.Text;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompXMT_Containment : ThingComp
    {
        private Thing transferTarget;

        public CompProperties_XMT_Containment Props =>
            parent?.def?.GetCompProperties<CompProperties_XMT_Containment>() ??
            (CompProperties_XMT_Containment)props;

        public CompPowerTrader PowerComp => parent?.GetComp<CompPowerTrader>();
        public Thing TransferTarget => transferTarget;
        public bool Occupied => parent is Building_ContainmentHarness harness && harness.ContainedThing is Pawn;
        public bool Powered => PowerComp?.PowerOn == true;
        public bool AcidImmune => AcidUtility.IsAcidImmune(parent);
        public float MaterialQuality => Mathf.Clamp(parent.GetStatValue(StatDefOf.MaxHitPoints) * Props.materialHitPointsFactor,
            0f, Props.maxMaterialQuality);
        public float TemperatureQuality => Mathf.Clamp((Props.idealTemperature - parent.AmbientTemperature) *
            Props.temperatureFactor, Props.minTemperatureQuality, Props.maxTemperatureQuality);
        public float LightQuality => parent.Spawned && parent.Map != null
            ? parent.Map.glowGrid.GroundGlowAt(parent.Position) * Props.maxLightQuality
            : 0f;
        public float PowerQuality => Powered ? Props.poweredQuality : Props.unpoweredQuality;
        public float AcidImmunityQuality => AcidImmune ? Props.acidImmuneQuality : 0f;
        public float ContainmentQuality => Mathf.Max(0f, Props.baseContainmentQuality + MaterialQuality +
            TemperatureQuality + LightQuality + PowerQuality + AcidImmunityQuality);

        public bool CanContain(Pawn pawn, bool requireDowned = true)
        {
            return pawn != null && !pawn.Dead && (!requireDowned || pawn.Downed) && pawn.RaceProps.Humanlike &&
                pawn.BodySize <= Props.maxBodySize;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            UpdatePowerDemand();
        }

        public override void CompTick()
        {
            base.CompTick();
            UpdatePowerDemand();
        }

        public void Notify_ContentsChanged()
        {
            if (!Occupied)
            {
                transferTarget = null;
            }
            UpdatePowerDemand();
        }

        public void SetTransferTarget(Thing target)
        {
            transferTarget = target;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref transferTarget, "transferTarget");
        }

        public string QualityExplanation()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("XMT_ContainmentQualityBase".Translate(Props.baseContainmentQuality.ToString("F1")));
            builder.AppendLine("XMT_ContainmentQualityMaterial".Translate(MaterialQuality.ToString("F1")));
            builder.AppendLine("XMT_ContainmentQualityTemperature".Translate(TemperatureQuality.ToString("+0.0;-0.0;0.0"), parent.AmbientTemperature.ToStringTemperature()));
            builder.AppendLine("XMT_ContainmentQualityLight".Translate(LightQuality.ToString("F1")));
            builder.AppendLine("XMT_ContainmentQualityPower".Translate(PowerQuality.ToString("+0.0;-0.0;0.0")));
            builder.Append("XMT_ContainmentQualityAcid".Translate(AcidImmunityQuality.ToString("F1")));
            return builder.ToString();
        }

        public override string CompInspectStringExtra()
        {
            string power = Occupied
                ? "XMT_ContainmentPowerOccupied".Translate(Props.occupiedPowerConsumption.ToString("F0"))
                : "XMT_ContainmentPowerIdle".Translate();
            return "XMT_ContainmentQualityInspect".Translate(ContainmentQuality.ToString("F1")) + "\n" + power;
        }

        private void UpdatePowerDemand()
        {
            if (PowerComp != null)
            {
                PowerComp.PowerOutput = Occupied ? -Props.occupiedPowerConsumption : 0f;
            }
        }
    }

    public class CompProperties_XMT_Containment : CompProperties
    {
        public float maxBodySize = 2.9f;
        public float occupiedPowerConsumption = 200f;
        public float baseContainmentQuality = 20f;
        public float materialHitPointsFactor = 0.1f;
        public float maxMaterialQuality = 50f;
        public float idealTemperature = 21f;
        public float temperatureFactor = 0.5f;
        public float minTemperatureQuality = -20f;
        public float maxTemperatureQuality = 20f;
        public float maxLightQuality = 15f;
        public float poweredQuality = 25f;
        public float unpoweredQuality = -25f;
        public float acidImmuneQuality = 15f;
        public ContainmentHarnessDrawOffsets north = new ContainmentHarnessDrawOffsets();
        public ContainmentHarnessDrawOffsets east = new ContainmentHarnessDrawOffsets();
        public ContainmentHarnessDrawOffsets south = new ContainmentHarnessDrawOffsets();
        public ContainmentHarnessDrawOffsets west = new ContainmentHarnessDrawOffsets();

        public CompProperties_XMT_Containment()
        {
            compClass = typeof(CompXMT_Containment);
        }

        public ContainmentHarnessDrawOffsets DrawOffsetsFor(Rot4 rotation)
        {
            if (rotation == Rot4.North)
            {
                return north;
            }
            if (rotation == Rot4.East)
            {
                return east;
            }
            if (rotation == Rot4.West)
            {
                return west;
            }
            return south;
        }
    }

    public class ContainmentHarnessDrawOffsets
    {
        public Vector2 spatialOffset = Vector2.zero;
        public float backDepthOffset = -0.02f;
        public float bodyDepthOffset;
        public float headDepthOffset;
        public float pawnRotationOffset;
    }
}
