

using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class HediffComp_GiveInjury : HediffComp
    {
        HediffCompProperties_GiveInjury Props => props as HediffCompProperties_GiveInjury;
        int injuryInterval => Mathf.CeilToInt(Props.hourIntervalToInflict * 2500);

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
           if(Pawn.IsHashIntervalTick(injuryInterval))
           {
                if (!MutationDamageUtility.AllowsActivityInjury(Pawn.Downed, Pawn.GetPosture() != PawnPosture.Standing,
                    Pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving),
                    Pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation), Props))
                {
                    return;
                }
                float damage = MutationDamageUtility.InjuryDamage(parent);
                if (damage > 0f && Rand.Chance(Props.chancebySeverity.Evaluate(parent.Severity)))
                {
                    Pawn.TakeDamage(new DamageInfo(Props.damageDef, damage, 1000, hitPart: parent.Part));
                }
           }
        }
    }
    public class HediffCompProperties_GiveInjury : HediffCompProperties
    {
        public SimpleCurve chancebySeverity;
        public float maxDamage = 3f;
        public float hourIntervalToInflict = 1;
        public bool injureIfDowned = false;
        public bool injureWhileResting = false;
        public bool latticeProtection;
        public bool injureWithoutMoving = false;
        public bool injureWithoutManipulation = false;
        public DamageDef damageDef;
        public HediffCompProperties_GiveInjury()
        {
            compClass = typeof(HediffComp_GiveInjury);
        }
    }
}
