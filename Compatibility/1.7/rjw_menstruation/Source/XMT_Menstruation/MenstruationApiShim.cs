using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using XMT_RJW;

namespace XMT_Menstruation
{
    // 1.7 SHIM: the local upstream release is 1.6. Keep its womb API assumptions
    // isolated here so a native 1.7 release can replace this adapter without core changes.
    public static class MenstruationApiShim
    {
        public static readonly Type WombType = AccessTools.TypeByName("RJW_Menstruation.HediffComp_Menstruation");
        public static bool Ready => RJWApiShim.Ready && WombType != null && typeof(HediffComp).IsAssignableFrom(WombType);
        public static PropertyInfo PregnancyProperty => AccessTools.Property(WombType, "Pregnancy");

        public static IEnumerable<HediffComp> Wombs(Pawn pawn) =>
            pawn.health.hediffSet.hediffs.OfType<HediffWithComps>().SelectMany(h => h.comps ?? Enumerable.Empty<HediffComp>())
                .Where(c => WombType.IsInstanceOfType(c));

        public static Hediff Pregnancy(HediffComp womb) => PregnancyProperty.GetValue(womb) as Hediff;
        public static void Assign(HediffComp womb, Hediff pregnancy) => PregnancyProperty.SetValue(womb, pregnancy);

        // 1.7 SHIM: native 1.6 UI fields/accessors, isolated from the palette rules.
        public static HediffComp WindowWomb(object window) => RJWApiShim.Read(window, "comp") as HediffComp;
        public static string VisibleStage(HediffComp womb) => RJWApiShim.Read(womb, "CurrentVisibleStage")?.ToString();
        public static Color BloodColor(HediffComp womb) => RJWApiShim.Read(womb, "BloodColor") is Color color ? color : Color.white;

        // 1.7 SHIM: 1.6 represents lifetime egg supply as an integer, not an age flag.
        public static int ReserveThreshold(HediffComp womb) => (int)RJWApiShim.Read(womb, "OvaryPowerThreshold");
        public static void RenewReserve(HediffComp womb)
        {
            FieldInfo reserve = AccessTools.Field(WombType, "ovarypower");
            reserve.SetValue(womb, EverFertile.HealthyReserve((int)reserve.GetValue(womb), ReserveThreshold(womb)));
        }

        public static void TransferWomb(Hediff source, Hediff replacement)
        {
            if (source is not HediffWithComps oldPart || replacement is not HediffWithComps newPart) return;
            foreach (HediffComp womb in oldPart.comps.Where(c => WombType.IsInstanceOfType(c)).ToList())
            {
                // Move the actual comp, preserving ova, cycle state, fluids and pregnancy ownership.
                // CopyCycleProperties alone omits those fields in upstream 1.6.
                HediffComp generated = newPart.comps.FirstOrDefault(c => WombType.IsInstanceOfType(c));
                if (generated != null) { womb.props = generated.props; newPart.comps.Remove(generated); }
                oldPart.comps.Remove(womb);
                womb.parent = newPart;
                newPart.comps.Add(womb);
            }
        }

        public static void ClearPregnancy(Hediff pregnancy)
        {
            foreach (HediffComp womb in Wombs(pregnancy.pawn))
                if (Pregnancy(womb) == pregnancy) Assign(womb, null);
        }
    }
}
