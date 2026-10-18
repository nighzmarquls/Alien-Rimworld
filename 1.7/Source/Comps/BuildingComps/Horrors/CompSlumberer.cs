using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class CompSlumberer : CompOrganicGrowth
    {
        private bool awakening;
        private CompSlumbererProperties Props => (CompSlumbererProperties)props;
        protected override bool InheritInjuries => true;
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            // Preserve the existing above-half-health awakening trigger.
            if (parent.Spawned && !parent.Destroyed && parent.HitPoints > parent.MaxHitPoints / 2) AwakenSleeper();
        }
        protected override void OnStarved() => AwakenSleeper();
        public void AwakenSleeper()
        {
            if (awakening || !parent.Spawned || parent.Destroyed) return;
            awakening = true;
            try
            {
                CompPawnPreservation preservation = parent.GetComp<CompPawnPreservation>();
                if (preservation == null || !preservation.EnsureRecovery()) return;
                preservation.HealToBuildingHealth();
                if (PreservedBuildingUtility.TrySplitTransformedBuilding(parent, out List<Thing> outputs, true))
                    foreach (Thing output in outputs)
                        if (output is Pawn pawn && !pawn.Dead)
                            pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, forced: true);
            }
            finally { awakening = false; }
        }
    }
    public class CompSlumbererProperties : CompProperties_OrganicGrowth
    {
        public PawnKindDef slumbererKindDef;
        public float maxSizeChange = 0.01f;
        public int breathticks = 120;
        public CompSlumbererProperties() : base(typeof(CompSlumberer)) { }
        public CompSlumbererProperties(Type type) : base(type) { }
    }
}
