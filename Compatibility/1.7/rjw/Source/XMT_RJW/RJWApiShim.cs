using HarmonyLib;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace XMT_RJW
{
    // 1.7 SHIM: keep all reflection against the 1.6 RJW API here. No old Unity/RJW
    // assembly references escape into core or force an incompatible DLL to load.
    public static class RJWApiShim
    {
        public static readonly Type PregnancyType = AccessTools.TypeByName("rjw.Hediff_BasePregnancy");
        private static readonly Type Helper = AccessTools.TypeByName("rjw.Genital_Helper");
        private static readonly Type PartAdder = AccessTools.TypeByName("rjw.SexPartAdder");
        public static bool Ready => PregnancyType != null && typeof(Hediff).IsAssignableFrom(PregnancyType);

        public static object Read(object instance, string name)
        {
            if (instance == null) return null;
            return AccessTools.Field(instance.GetType(), name)?.GetValue(instance) ??
                AccessTools.Property(instance.GetType(), name)?.GetValue(instance);
        }

        public static bool IsNative(Hediff pregnancy) => Ready && PregnancyType.IsInstanceOfType(pregnancy);

        public static IList Babies(Hediff pregnancy)
        {
            if (IsNative(pregnancy)) return Read(pregnancy, "babies") as IList;
            return (pregnancy as HediffWithComps)?.comps?.Select(c => Read(c, "babies")).OfType<IList>().FirstOrDefault();
        }

        public static bool Supports(HediffDef def) => Ready &&
            (PregnancyType.IsAssignableFrom(def.hediffClass) || def.comps?.Any(c =>
                c.compClass?.FullName == "RJW_Menstruation.HediffComp_PregeneratedBabies") == true);

        public static bool HasFemaleParts(Pawn pawn)
        {
            if (pawn?.health == null) return false;
            MethodInfo method = AccessTools.Method(Helper, "has_vagina");
            if (method == null) return false;
            ParameterInfo[] parameters = method.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = pawn;
            for (int i = 1; i < args.Length; i++) args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
            return (bool)method.Invoke(null, args);
        }

        public static BodyPartRecord Genitals(Pawn pawn) =>
            AccessTools.Method(Helper, "get_genitalsBPR")?.Invoke(null, new object[] { pawn }) as BodyPartRecord;

        public static Hediff MakeFemalePart(Pawn pawn, BodyPartRecord site) =>
            AccessTools.Method(PartAdder, "MakePart")?.Invoke(null,
                new object[] { RJWReproductionDef.Current.femalePart, pawn, site }) as Hediff;

        public static List<Hediff> FemaleParts(Pawn pawn)
        {
            // Part identity comes from RJW's family enum, not translated labels.
            return pawn.health.hediffSet.hediffs.Where(h =>
                Read(h.def, "genitalFamily")?.ToString() is "Vagina" or "FemaleOvipositor").ToList();
        }

        public static Hediff CreateNative(Pawn mother, Pawn donor)
        {
            if (!Ready) return null;
            Type type = AccessTools.TypeByName("RJW_Menstruation.Hediff_MultiplePregnancy") ??
                AccessTools.TypeByName(mother.RaceProps.Humanlike ? "rjw.Hediff_HumanlikePregnancy" : "rjw.Hediff_BestialPregnancy");
            MethodInfo factory = PregnancyType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Create" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);
            if (type == null || factory == null) return null;
            // 1.6 SHIM: Initialize skips baby generation with a null father, then indexes
            // that empty list to calculate gestation. A mother-only horror mutation needs
            // a generation seed, but must not invent a genetic father or self-inbreeding.
            Hediff created = factory.MakeGenericMethod(type).Invoke(null,
                new object[] { mother, donor ?? mother }) as Hediff;
            if (created != null && donor == null)
            {
                AccessTools.Field(created.GetType(), "father")?.SetValue(created, null);
                GeneSet maternalGenes = PregnancyUtility.GetInheritedGeneSet(null, mother);
                foreach (Pawn baby in Babies(created)?.Cast<Pawn>() ?? Enumerable.Empty<Pawn>())
                    Xenomorphtype.BioUtility.TryApplyHorrorGenePayload(baby,
                        new Xenomorphtype.HorrorGenePayload(maternalGenes.GenesListForReading), replaceNativeGenes: true);
            }
            return created;
        }

        public static void Advance(Hediff pregnancy, int delta, float factor)
        {
            // Native ticks still own labor/miscarriage. Shift only the extra accelerated time.
            if (!IsNative(pregnancy) || factor >= 1f) return;
            float extra = delta * (1f / Math.Max(0.01f, factor) - 1f);
            foreach (string name in new[] { "p_start_tick", "p_end_tick" })
            {
                FieldInfo field = AccessTools.Field(pregnancy.GetType(), name);
                if (field != null) field.SetValue(pregnancy, (float)field.GetValue(pregnancy) - extra);
            }
        }

        public static void ReplaceSiblingReferences(Hediff pregnancy, Pawn oldBaby, Pawn baby)
        {
            foreach (object holder in new object[] { pregnancy }.Concat(
                (pregnancy as HediffWithComps)?.comps?.Cast<object>() ?? Enumerable.Empty<object>()))
            {
                if (Read(holder, "enzygoticSiblings") is not IDictionary siblings) continue;
                List<DictionaryEntry> pairs = new List<DictionaryEntry>();
                foreach (DictionaryEntry pair in siblings) pairs.Add(pair);
                siblings.Clear();
                foreach (DictionaryEntry pair in pairs)
                    siblings[pair.Key == oldBaby ? baby : pair.Key] = pair.Value == oldBaby ? baby : pair.Value;
            }
        }
    }
}
