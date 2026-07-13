using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public enum KnowledgeAcquisition
    {
        Education,
        ControlledExperience,
        TraumaticExperience,
        PsychicExposure
    }

    public enum KnowledgeLevel { None, Vague, Basic, Practical, Thorough, Complete }
    public enum TraumaSeverity { None, Concerned, Anxious, Paranoid, Traumatized }
    public enum ObsessionSeverity { None, Intrigued, Fascinated, Obsessed, Enthralled }

    public class KnowledgeCategoryDef : Def
    {
        public float learnedEffectiveness = 0.5f;
        public float vagueThreshold = 0.01f;
        public float basicThreshold = 0.25f;
        public float practicalThreshold = 0.5f;
        public float thoroughThreshold = 0.75f;
        public float completeThreshold = 1f;
        public string learnedDescriptionKey;
        public string experiencedDescriptionKey;
        public string obsessedDescriptionKey;
    }

    public class KnowledgeProfileEntry
    {
        public KnowledgeCategoryDef category;
        public float weight = 1f;
    }

    public class KnowledgeProfileDef : Def
    {
        public List<KnowledgeProfileEntry> knowledge = new List<KnowledgeProfileEntry>();
        public KnowledgeAcquisition acquisition = KnowledgeAcquisition.TraumaticExperience;
        public float traumaFactor = 1f;
        public float obsessionFactor;
        public bool canRetriggerTrauma = true;
        public float triggerIntensity = 0.25f;
        public float retriggerTraumaGainFactor;
    }

    public class KnowledgeModifierDef : Def
    {
        public TraitDef trait;
        public int? traitDegree;
        public BackstoryDef backstory;
        public PreceptDef precept;
        public GeneDef gene;
        public HediffDef hediff;
        public List<KnowledgeProfileEntry> learnedKnowledge = new List<KnowledgeProfileEntry>();
        public List<KnowledgeProfileEntry> experiencedKnowledge = new List<KnowledgeProfileEntry>();
        public float obsessionOffset;
        public float obsessionGainFactor = 1f;
        public float obsessionResistance;
        public float traumaGainFactor = 1f;
        public float traumaTriggerFactor = 1f;
        public float traumaRecoveryFactor = 1f;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            int sources = (trait != null ? 1 : 0) + (backstory != null ? 1 : 0) +
                (precept != null ? 1 : 0) + (gene != null ? 1 : 0) + (hediff != null ? 1 : 0);
            if (sources != 1) yield return defName + " must define exactly one modifier source.";
        }
    }

    public class PawnKnowledgeRecord : IExposable
    {
        public KnowledgeCategoryDef category;
        public float learned;
        public float experienced;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref category, "category");
            Scribe_Values.Look(ref learned, "learned", 0f);
            Scribe_Values.Look(ref experienced, "experienced", 0f);
        }
    }

    public sealed class CategoryKnowledgeAssessment
    {
        public KnowledgeCategoryDef category;
        public float learned;
        public float experienced;
        public float effective;
        public KnowledgeLevel level;
    }

    public sealed class KnowledgeAssessment
    {
        public readonly Dictionary<KnowledgeCategoryDef, CategoryKnowledgeAssessment> categories = new Dictionary<KnowledgeCategoryDef, CategoryKnowledgeAssessment>();
        public float trauma;
        public TraumaSeverity traumaSeverity;
        public float traumaGainFactor = 1f;
        public float traumaTriggerFactor = 1f;
        public float traumaRecoveryFactor = 1f;
        public float obsessionPressure;
        public float obsessionResistance;
        public float obsessionBalance;
        public float obsessionGainFactor = 1f;
        public ObsessionSeverity obsessionSeverity;
        public bool obsessed;
    }

    [DefOf]
    public static class KnowledgeDefOf
    {
        public static KnowledgeCategoryDef XMT_Knowledge_Ovomorph;
        public static KnowledgeCategoryDef XMT_Knowledge_Larva;
        public static KnowledgeCategoryDef XMT_Knowledge_Adult;
        public static KnowledgeCategoryDef XMT_Knowledge_Acid;
        public static KnowledgeCategoryDef XMT_Knowledge_Psychic;
        public static KnowledgeProfileDef XMT_Profile_Ovomorph;
        public static KnowledgeProfileDef XMT_Profile_Larva;
        public static KnowledgeProfileDef XMT_Profile_Adult;
        public static KnowledgeProfileDef XMT_Profile_Acid;
        public static KnowledgeProfileDef XMT_Profile_Psychic;

        static KnowledgeDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(KnowledgeDefOf));
    }

    public static class KnowledgeUtility
    {
        private sealed class CachedAssessment { public int tick; public KnowledgeAssessment value; }
        private static readonly Dictionary<Pawn, CachedAssessment> cache = new Dictionary<Pawn, CachedAssessment>();
        private static readonly Dictionary<Pawn, int> lastTeachingTick = new Dictionary<Pawn, int>();
        private static Dictionary<TraitDef, List<KnowledgeModifierDef>> byTrait;
        private static Dictionary<BackstoryDef, List<KnowledgeModifierDef>> byBackstory;
        private static Dictionary<PreceptDef, List<KnowledgeModifierDef>> byPrecept;
        private static Dictionary<GeneDef, List<KnowledgeModifierDef>> byGene;
        private static Dictionary<HediffDef, List<KnowledgeModifierDef>> byHediff;

        public static void Invalidate(Pawn pawn)
        {
            if (pawn != null) cache.Remove(pawn);
        }

        private static void EnsureIndexes()
        {
            if (byTrait != null) return;
            byTrait = new Dictionary<TraitDef, List<KnowledgeModifierDef>>();
            byBackstory = new Dictionary<BackstoryDef, List<KnowledgeModifierDef>>();
            byPrecept = new Dictionary<PreceptDef, List<KnowledgeModifierDef>>();
            byGene = new Dictionary<GeneDef, List<KnowledgeModifierDef>>();
            byHediff = new Dictionary<HediffDef, List<KnowledgeModifierDef>>();
            foreach (KnowledgeModifierDef modifier in DefDatabase<KnowledgeModifierDef>.AllDefsListForReading)
            {
                if (modifier.trait != null) AddIndex(byTrait, modifier.trait, modifier);
                else if (modifier.backstory != null) AddIndex(byBackstory, modifier.backstory, modifier);
                else if (modifier.precept != null) AddIndex(byPrecept, modifier.precept, modifier);
                else if (modifier.gene != null) AddIndex(byGene, modifier.gene, modifier);
                else if (modifier.hediff != null) AddIndex(byHediff, modifier.hediff, modifier);
            }
        }

        private static void AddIndex<T>(Dictionary<T, List<KnowledgeModifierDef>> index, T key, KnowledgeModifierDef value)
        {
            if (!index.TryGetValue(key, out List<KnowledgeModifierDef> values)) index[key] = values = new List<KnowledgeModifierDef>();
            values.Add(value);
        }

        public static KnowledgeAssessment GetAssessment(Pawn pawn)
        {
            if (pawn == null) return new KnowledgeAssessment();
            int tick = Find.TickManager?.TicksGame ?? -1;
            if (cache.TryGetValue(pawn, out CachedAssessment cached) && cached.tick == tick) return cached.value;
            KnowledgeAssessment result = BuildAssessment(pawn);
            cache[pawn] = new CachedAssessment { tick = tick, value = result };
            return result;
        }

        private static KnowledgeAssessment BuildAssessment(Pawn pawn)
        {
            EnsureIndexes();
            KnowledgeAssessment result = new KnowledgeAssessment();
            CompPawnInfo info = pawn.Info();
            foreach (KnowledgeCategoryDef category in DefDatabase<KnowledgeCategoryDef>.AllDefsListForReading)
            {
                PawnKnowledgeRecord raw = info?.RawKnowledge(category);
                result.categories[category] = new CategoryKnowledgeAssessment { category = category, learned = raw?.learned ?? 0f, experienced = raw?.experienced ?? 0f };
            }
            result.trauma = Mathf.Max(0f, info?.RawTrauma ?? 0f);
            result.obsessionPressure = info?.RawObsession ?? 0f;
            foreach (KnowledgeModifierDef modifier in ApplicableModifiers(pawn)) ApplyModifier(result, modifier);
            foreach (CategoryKnowledgeAssessment category in result.categories.Values)
            {
                category.learned = Mathf.Max(0f, category.learned);
                category.experienced = Mathf.Max(0f, category.experienced);
                category.effective = Mathf.Max(category.experienced, category.learned * Mathf.Max(0f, category.category.learnedEffectiveness));
                category.level = GetKnowledgeLevel(category.category, category.effective);
            }
            result.trauma = Mathf.Max(0f, result.trauma);
            result.traumaSeverity = GetTraumaSeverity(result.trauma);
            result.obsessionResistance += result.trauma;
            result.obsessionBalance = result.obsessionPressure - result.obsessionResistance;
            result.obsessionSeverity = GetObsessionSeverity(result.obsessionBalance);
            result.obsessed = result.obsessionBalance > 0f;
            return result;
        }

        private static IEnumerable<KnowledgeModifierDef> ApplicableModifiers(Pawn pawn)
        {
            HashSet<KnowledgeModifierDef> yielded = new HashSet<KnowledgeModifierDef>();
            if (pawn.story?.traits != null)
                foreach (Trait trait in pawn.story.traits.allTraits)
                    if (byTrait.TryGetValue(trait.def, out List<KnowledgeModifierDef> mods))
                        foreach (KnowledgeModifierDef mod in mods)
                            if ((!mod.traitDegree.HasValue || mod.traitDegree.Value == trait.Degree) && yielded.Add(mod)) yield return mod;
            if (pawn.story != null)
                foreach (BackstoryDef backstory in pawn.story.AllBackstories)
                    if (backstory != null && byBackstory.TryGetValue(backstory, out List<KnowledgeModifierDef> mods))
                        foreach (KnowledgeModifierDef mod in mods) if (yielded.Add(mod)) yield return mod;
            if (ModsConfig.IdeologyActive && pawn.Ideo != null)
                foreach (Precept precept in pawn.Ideo.PreceptsListForReading)
                    if (byPrecept.TryGetValue(precept.def, out List<KnowledgeModifierDef> mods))
                        foreach (KnowledgeModifierDef mod in mods) if (yielded.Add(mod)) yield return mod;
            if (ModsConfig.BiotechActive && pawn.genes != null)
                foreach (Gene gene in pawn.genes.GenesListForReading)
                    if (byGene.TryGetValue(gene.def, out List<KnowledgeModifierDef> mods))
                        foreach (KnowledgeModifierDef mod in mods) if (yielded.Add(mod)) yield return mod;
            if (pawn.health?.hediffSet?.hediffs != null)
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                    if (byHediff.TryGetValue(hediff.def, out List<KnowledgeModifierDef> mods))
                        foreach (KnowledgeModifierDef mod in mods) if (yielded.Add(mod)) yield return mod;
        }

        private static void ApplyModifier(KnowledgeAssessment result, KnowledgeModifierDef modifier)
        {
            ApplyEntries(result, modifier.learnedKnowledge, true);
            ApplyEntries(result, modifier.experiencedKnowledge, false);
            result.obsessionPressure += modifier.obsessionOffset;
            result.obsessionResistance += modifier.obsessionResistance;
            result.obsessionGainFactor *= Mathf.Max(0f, modifier.obsessionGainFactor);
            result.traumaGainFactor *= Mathf.Max(0f, modifier.traumaGainFactor);
            result.traumaTriggerFactor *= Mathf.Max(0f, modifier.traumaTriggerFactor);
            result.traumaRecoveryFactor *= Mathf.Max(0f, modifier.traumaRecoveryFactor);
        }

        private static void ApplyEntries(KnowledgeAssessment result, List<KnowledgeProfileEntry> entries, bool learned)
        {
            if (entries == null) return;
            foreach (KnowledgeProfileEntry entry in entries)
                if (entry.category != null && result.categories.TryGetValue(entry.category, out CategoryKnowledgeAssessment category))
                    if (learned) category.learned += entry.weight; else category.experienced += entry.weight;
        }

        public static float GetLearnedKnowledge(Pawn pawn, KnowledgeCategoryDef category) => Category(pawn, category)?.learned ?? 0f;
        public static float GetExperiencedKnowledge(Pawn pawn, KnowledgeCategoryDef category) => Category(pawn, category)?.experienced ?? 0f;
        public static float GetEffectiveKnowledge(Pawn pawn, KnowledgeCategoryDef category) => Category(pawn, category)?.effective ?? 0f;
        private static CategoryKnowledgeAssessment Category(Pawn pawn, KnowledgeCategoryDef category) => category != null && GetAssessment(pawn).categories.TryGetValue(category, out CategoryKnowledgeAssessment value) ? value : null;
        public static float GetTotalEffectiveKnowledge(Pawn pawn) => GetAssessment(pawn).categories.Values.Sum(value => value.effective);
        public static float GetTrauma(Pawn pawn) => GetAssessment(pawn).trauma;
        public static bool IsObsessed(Pawn pawn) => GetAssessment(pawn).obsessed;
        public static bool IsObsessed(Pawn pawn, out float trauma) { trauma = GetTrauma(pawn); return IsObsessed(pawn); }

        public static KnowledgeLevel GetKnowledgeLevel(KnowledgeCategoryDef category, float value)
        {
            if (category == null || value < category.vagueThreshold) return KnowledgeLevel.None;
            if (value >= category.completeThreshold) return KnowledgeLevel.Complete;
            if (value >= category.thoroughThreshold) return KnowledgeLevel.Thorough;
            if (value >= category.practicalThreshold) return KnowledgeLevel.Practical;
            if (value >= category.basicThreshold) return KnowledgeLevel.Basic;
            return KnowledgeLevel.Vague;
        }

        public static TraumaSeverity GetTraumaSeverity(float value) => value >= 1f ? TraumaSeverity.Traumatized : value >= 0.5f ? TraumaSeverity.Paranoid : value >= 0.25f ? TraumaSeverity.Anxious : value > 0f ? TraumaSeverity.Concerned : TraumaSeverity.None;
        public static ObsessionSeverity GetObsessionSeverity(float value) => value >= 4f ? ObsessionSeverity.Enthralled : value >= 1f ? ObsessionSeverity.Obsessed : value >= 0.5f ? ObsessionSeverity.Fascinated : value > 0f ? ObsessionSeverity.Intrigued : ObsessionSeverity.None;

        public static void ApplyExposure(Pawn pawn, KnowledgeProfileDef profile, float magnitude = 1f, KnowledgeAcquisition? acquisitionOverride = null, Thing source = null)
        {
            if (pawn?.Info() is not CompPawnInfo info || profile == null || magnitude <= 0f) return;
            KnowledgeAcquisition acquisition = acquisitionOverride ?? profile.acquisition;
            foreach (KnowledgeProfileEntry entry in profile.knowledge ?? Enumerable.Empty<KnowledgeProfileEntry>())
            {
                if (entry.category == null) continue;
                PawnKnowledgeRecord record = info.RawKnowledge(entry.category, true);
                float gain = magnitude * entry.weight;
                if (acquisition == KnowledgeAcquisition.Education) record.learned = Mathf.Clamp01(record.learned + gain);
                else record.experienced = Mathf.Clamp01(record.experienced + gain);
            }
            KnowledgeAssessment before = GetAssessment(pawn);
            if (acquisition == KnowledgeAcquisition.TraumaticExperience || acquisition == KnowledgeAcquisition.PsychicExposure)
                info.RawTrauma = Mathf.Max(0f, info.RawTrauma + magnitude * profile.traumaFactor * before.traumaGainFactor);
            else if (profile.canRetriggerTrauma && before.trauma > 0f && Rand.Chance(Mathf.Clamp01(before.trauma * profile.triggerIntensity * before.traumaTriggerFactor)))
            {
                if (profile.retriggerTraumaGainFactor > 0f) info.RawTrauma += magnitude * profile.retriggerTraumaGainFactor;
                if (pawn.needs?.mood != null) XMTUtility.GiveMemory(pawn, HorrorMoodDefOf.VictimNightmareMood, stage: Math.Min(3, (int)before.trauma));
            }
            if (profile.obsessionFactor != 0f) info.RawObsession += magnitude * profile.obsessionFactor * before.obsessionGainFactor;
            Invalidate(pawn);
            info.TryApplyDisplayHediff();
        }

        public static void GainKnowledge(Pawn pawn, KnowledgeCategoryDef category, float amount, KnowledgeAcquisition acquisition)
        {
            if (category == null) return;
            KnowledgeProfileDef transient = new KnowledgeProfileDef { acquisition = acquisition, knowledge = new List<KnowledgeProfileEntry> { new KnowledgeProfileEntry { category = category } }, traumaFactor = acquisition == KnowledgeAcquisition.TraumaticExperience ? 1f : 0f };
            ApplyExposure(pawn, transient, amount, acquisition);
        }

        public static void GainObsession(Pawn pawn, float amount)
        {
            if (pawn?.Info() is not CompPawnInfo info || XMTUtility.IsXenomorph(pawn)) return;
            info.RawObsession += amount * GetAssessment(pawn).obsessionGainFactor;
            Invalidate(pawn);
            info.TryApplyDisplayHediff();
        }

        public static void RelieveTrauma(Pawn pawn, float amount)
        {
            if (pawn?.Info() is not CompPawnInfo info || amount <= 0f) return;
            info.RawTrauma = Mathf.Max(0f, info.RawTrauma - amount * GetAssessment(pawn).traumaRecoveryFactor);
            Invalidate(pawn);
        }

        public static void TryShareKnowledge(Pawn teacher, Pawn student)
        {
            if (teacher == null || student == null || teacher == student || !teacher.RaceProps.Humanlike || !student.RaceProps.Humanlike || teacher.HostileTo(student)) return;
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (lastTeachingTick.TryGetValue(student, out int lastTick) && tick - lastTick < GenDate.TicksPerDay) return;
            List<CategoryKnowledgeAssessment> candidates = GetAssessment(teacher).categories.Values
                .Where(value => value.effective - GetEffectiveKnowledge(student, value.category) >= 0.15f).ToList();
            if (candidates.Count == 0) return;
            CategoryKnowledgeAssessment selected = candidates.RandomElement();
            float gap = selected.effective - GetEffectiveKnowledge(student, selected.category);
            float gain = Mathf.Min(0.05f, gap * 0.15f);
            GainKnowledge(student, selected.category, gain, KnowledgeAcquisition.Education);
            lastTeachingTick[student] = tick;
        }
    }
}
