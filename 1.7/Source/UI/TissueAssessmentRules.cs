using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class TissueSummaryRule
    {
        public Type workerClass;
        public string groupKey, labelKey, activityAdviceKey, restingAdviceKey;
        public int priority;
        public List<DamageDef> damageTypes, excludedDamageTypes;
        public AcidTissueHazard acidHazard;
        public bool hazard, activeMutator;
        private TissueSummaryWorker worker;
        public TissueSummaryWorker Worker => worker ??= (TissueSummaryWorker)Activator.CreateInstance(workerClass);
    }
    public abstract class TissueSummaryWorker
    {
        public abstract bool Matches(Hediff tissue, TissueSummaryRule rule);
        public virtual string Group(Hediff tissue) => "";
        public virtual string Advice(Hediff tissue, TissueSummaryRule rule) => null;
        public virtual string Detail(Hediff tissue) => "";
    }
    public class TissueSummary_All : TissueSummaryWorker
    { public override bool Matches(Hediff tissue, TissueSummaryRule rule) => true; }
    public class TissueSummary_Mutator : TissueSummaryWorker
    {
        public override bool Matches(Hediff tissue, TissueSummaryRule rule) =>
            tissue.TryGetComp<HediffComp_Mutator>()?.props is HediffCompProperties_Mutator p &&
            (p.useSeverityAsProbability ? tissue.Severity : p.probability) > 0f && CryptimorphicInfluence.CanMutate(tissue.pawn);
    }
    public class TissueSummary_Mechanical : TissueSummaryWorker
    {
        public override bool Matches(Hediff tissue, TissueSummaryRule rule)
        {
            var p = tissue.TryGetComp<HediffComp_GiveInjury>()?.props as HediffCompProperties_GiveInjury;
            return p != null && (rule.damageTypes == null || rule.damageTypes.Contains(p.damageDef)) &&
                (rule.excludedDamageTypes == null || !rule.excludedDamageTypes.Contains(p.damageDef)) && MutationDamageUtility.InjuryDamage(tissue) > 0f;
        }
        public override string Group(Hediff tissue) => ((HediffCompProperties_GiveInjury)tissue.TryGetComp<HediffComp_GiveInjury>().props).damageDef.defName +
            ((HediffCompProperties_GiveInjury)tissue.TryGetComp<HediffComp_GiveInjury>().props).injureWhileResting;
        public override string Advice(Hediff tissue, TissueSummaryRule rule) =>
            ((HediffCompProperties_GiveInjury)tissue.TryGetComp<HediffComp_GiveInjury>().props).injureWhileResting ? rule.restingAdviceKey : rule.activityAdviceKey;
        public override string Detail(Hediff tissue) => ((HediffCompProperties_GiveInjury)tissue.TryGetComp<HediffComp_GiveInjury>().props).damageDef.LabelCap.ToString();
    }
    public class TissueSummary_Acid : TissueSummaryWorker
    { public override bool Matches(Hediff tissue, TissueSummaryRule rule) => MutationDamageUtility.AcidHazard(tissue) == rule.acidHazard; }
    public class TissueSummary_Corrosive : TissueSummaryWorker
    {
        public override bool Matches(Hediff tissue, TissueSummaryRule rule) =>
            tissue.TryGetComp<HediffComp_Corrosive>()?.props is HediffCompProperties_Corrosive p && p.damageType != null &&
            p.damageMultiplier * tissue.Severity > 0f && HediffComp_Corrosive.DamageTargets(tissue.pawn, tissue.Part).Any();
    }
    public class TissueSummary_ToxicStress : TissueSummaryWorker
    {
        public override bool Matches(Hediff tissue, TissueSummaryRule rule) => tissue.TryGetComp<HediffComp_Mutator>()?.props is HediffCompProperties_Mutator p &&
            p.toxicShockSeverity > 0f && p.toxicShockChance > 0f && (p.useSeverityAsProbability ? tissue.Severity : p.probability) > 0f &&
            CryptimorphicInfluence.CanMutate(tissue.pawn) && tissue.pawn.GetStatValue(StatDefOf.ToxicResistance) < 1f;
    }
    public class TissueSummary_LatticeProtection : TissueSummaryWorker
    {
        public override bool Matches(Hediff tissue, TissueSummaryRule rule) =>
            tissue.TryGetComp<HediffComp_GiveInjury>()?.props is HediffCompProperties_GiveInjury p && p.latticeProtection && tissue.pawn.MesoSkeletonization() > 0f;
    }
    public class TissueSummary_Exposure : TissueSummaryWorker
    { public override bool Matches(Hediff tissue, TissueSummaryRule rule) => tissue is Hediff_CryptimorphicExposure; }


}
