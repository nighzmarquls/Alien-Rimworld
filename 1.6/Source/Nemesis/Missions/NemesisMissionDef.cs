using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class NemesisMissionStanceWeight
    {
        public NemesisStanceDef stance;
        public float weight = 1f;
    }

    public class NemesisMissionPressureInput
    {
        public NemesisSignalDef signal;
        public float weight = 1f;
    }

    public class NemesisMissionSettings
    {
        public float localSearchRadius = 18f;
        public int maximumRoutePoints = 8;
        public float staleIntelDays = 5f;
        public float freshIntelWeight = 0.5f;
        public float staleScoutWeight = 4f;
        public NemesisEvidenceDef weightEvidence;
        public float weightEvidenceBonus = 1f;
        public PawnKindDef raidPointPawnKind;
        public float raidPointBudgetFactor = 1f;
        public float populationFraction = 0.5f;
    }

    public class NemesisMissionDef : Def
    {
        public Type workerClass;
        public Type lordJobClass;
        public bool allowDormant = true;
        public bool allowAwakened = true;
        public float baseWeight = 1f;
        public IntRange populationRange = new IntRange(1, 3);
        public float xenoformingForMaximumPressure = 100f;
        public int maximumDurationTicks = 18000;
        public NemesisEvidenceDef extractionEvidence;
        public List<NemesisMissionStanceWeight> stanceWeights = new List<NemesisMissionStanceWeight>();
        public List<NemesisMissionPressureInput> pressureInputs = new List<NemesisMissionPressureInput>();
        public NemesisMissionSettings workerSettings = new NemesisMissionSettings();

        private NemesisMissionWorker worker;
        public NemesisMissionWorker Worker => worker ??= (NemesisMissionWorker)Activator.CreateInstance(workerClass);

        public float StanceWeight(GameComponent_Nemesis component, bool active) => !active ? 1f
            : stanceWeights.FirstOrDefault(entry => entry.stance == component.CurrentStance)?.weight ?? 1f;

        public float Pressure(GameComponent_Nemesis component, bool active)
        {
            float result = XenoformingUtility.GetXenoforming() / Mathf.Max(1f, xenoformingForMaximumPressure);
            if (active)
                foreach (NemesisMissionPressureInput input in pressureInputs)
                    if (input.signal != null)
                        result += input.weight * NemesisSignalEvaluator.Evaluate(input.signal, component, Find.TickManager.TicksGame).value;
            return Mathf.Clamp01(result);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (workerClass == null || workerClass.IsAbstract || !typeof(NemesisMissionWorker).IsAssignableFrom(workerClass)
                || workerClass.GetConstructor(Type.EmptyTypes) == null) yield return defName + ": invalid mission worker.";
            if (lordJobClass == null || lordJobClass.IsAbstract || !typeof(LordJob_NemesisMission).IsAssignableFrom(lordJobClass)
                || lordJobClass.GetConstructor(Type.EmptyTypes) == null) yield return defName + ": invalid mission lord job.";
            if (populationRange.min < 1 || populationRange.max < populationRange.min || baseWeight < 0f
                || xenoformingForMaximumPressure <= 0f || maximumDurationTicks <= 0)
                yield return defName + ": invalid mission limits.";
            if (workerSettings == null || workerSettings.localSearchRadius <= 0f || workerSettings.maximumRoutePoints < 1
                || workerSettings.raidPointBudgetFactor < 0f || workerSettings.staleIntelDays <= 0f
                || workerSettings.populationFraction < 0f || workerSettings.freshIntelWeight < 0f
                || workerSettings.staleScoutWeight < workerSettings.freshIntelWeight || workerSettings.weightEvidenceBonus < 0f)
                yield return defName + ": invalid worker settings.";
            if (stanceWeights.Any(x => x.stance == null || x.weight < 0f)
                || stanceWeights.GroupBy(x => x.stance).Any(x => x.Count() > 1)) yield return defName + ": invalid stance weights.";
            if (pressureInputs.Any(x => x.signal == null)) yield return defName + ": invalid pressure input.";
            if (workerSettings != null && workerClass != null && !workerClass.IsAbstract
                && typeof(NemesisMissionWorker).IsAssignableFrom(workerClass) && workerClass.GetConstructor(Type.EmptyTypes) != null)
                foreach (string error in Worker.ConfigErrors(this)) yield return error;
        }
    }

    public sealed class NemesisMissionRequest : IExposable
    {
        public NemesisMissionDef mission;
        public int mapId = -1;
        public bool active;
        public int selectedTick;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref mission, "mission");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref active, "active");
            Scribe_Values.Look(ref selectedTick, "selectedTick");
        }
    }
}
