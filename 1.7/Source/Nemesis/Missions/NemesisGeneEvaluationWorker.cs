using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class NemesisGeneEvaluationWorker
    {
        public virtual void Snapshot(NemesisGeneticTrial trial, Map map) { }
        public virtual float Evaluate(NemesisGeneticTrial trial, Map map) => GenericFitness(trial);
        public static float GenericFitness(NemesisGeneticTrial trial)
        {
            if (trial.members.Count == 0) return 0f;
            return trial.members.Average(member => Mathf.Clamp01((member.escaped ? 0.65f : 0f)
                + (member.escaped && member.injury > 0f ? 0.15f : 0f)
                + 0.1f * Mathf.Clamp01(member.kills / 2f) + 0.1f * Mathf.Clamp01(member.extracted)));
        }
    }

    public sealed class NemesisGeneEvaluationWorker_Ablative : NemesisGeneEvaluationWorker
    {
        public override void Snapshot(NemesisGeneticTrial trial, Map map)
        {
            if (map == null) return;
            foreach (Building building in map.listerBuildings.allBuildingsColonist.Where(IsDefense))
                trial.defenses.Add(new NemesisDefenseSnapshot { target = building, hitPoints = building.HitPoints,
                    weight = building.def.building?.IsTurret == true ? 2f : building.def.IsWall ? 0.1f : 1f });
        }
        internal static bool IsDefense(Building building) => building != null && !building.Destroyed
            && (building.def.building?.IsTurret == true || building.def.IsWall
                || building.def.building?.isTrap == true && (NemesisThreatUtility.TrapThreat(building) > 1f
                    || NemesisThreatUtility.IsHarmfulExplosive(building.def)));
        public override float Evaluate(NemesisGeneticTrial trial, Map map)
        {
            float total = trial.defenses.Sum(value => value.weight);
            if (total <= 0f) return 0f;
            return Mathf.Clamp01(trial.defenses.Sum(value => value.weight * (value.target == null || value.target.Destroyed
                ? 1f : Mathf.Clamp01(1f - value.target.HitPoints / (float)Math.Max(1, value.hitPoints)))) / total);
        }
    }
}
