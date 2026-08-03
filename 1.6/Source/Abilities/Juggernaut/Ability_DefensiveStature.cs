using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VEF.Hediffs;
using Verse;

namespace Xenomorphtype
{
    public class Ability_DefensiveStature : Ability
    {
        private bool active;

        public bool Active => active;

        public Ability_DefensiveStature()
        {
        }

        public Ability_DefensiveStature(Pawn pawn)
            : base(pawn)
        {
        }

        public Ability_DefensiveStature(Pawn pawn, AbilityDef def)
            : base(pawn, def)
        {
        }

        public override IEnumerable<Command> GetGizmos()
        {
            yield return new Command_Toggle
            {
                defaultLabel = "XMT_DefensiveStatureToggle".Translate(),
                defaultDesc = "XMT_DefensiveStatureToggleDesc".Translate(),
                icon = def.uiIcon,
                isActive = () => active,
                toggleAction = delegate
                {
                    active = !active;
                    if (!active)
                    {
                        DefensiveStatureUtility.RemoveStanceHediffs(pawn);
                    }
                }
            };
        }

        public override string Tooltip
        {
            get
            {
                string status = active
                    ? "XMT_DefensiveStatureStatusActive".Translate()
                    : "XMT_DefensiveStatureStatusInactive".Translate();
                return base.Tooltip + "\n\n" + "XMT_DefensiveStatureStats".Translate(
                    status,
                    JuggernautAbilityUtility.DefensiveStatureRadius(pawn).ToString("0.#"));
            }
        }

        public override void AbilityTick()
        {
            base.AbilityTick();
            if (!DefensiveStatureUtility.IsOperational(pawn))
            {
                DefensiveStatureUtility.RemoveStanceHediffs(pawn);
                return;
            }

            DefensiveStatureUtility.EnsureStanceHediff(pawn);
            DefensiveStatureUtility.RefreshProtectionAura(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref active, "defensiveStatureActive", false);
        }
    }

    public static class DefensiveStatureUtility
    {
        private const int AuraRefreshDuration = 5;

        public static Ability_DefensiveStature GetAbility(Pawn queen)
        {
            return queen?.abilities?.AllAbilitiesForReading?
                .OfType<Ability_DefensiveStature>()
                .FirstOrDefault();
        }

        public static bool IsOperational(Pawn queen)
        {
            return queen != null
                && queen.Spawned
                && !queen.Dead
                && !queen.Downed
                && queen.Faction != null
                && GetAbility(queen)?.Active == true
                && queen.GetComp<CompQueen>()?.HasFunctionalEvolutionFeature(RoyalEvolutionDefOf.Evo_JuggernautsCrest) == true;
        }

        public static void EnsureStanceHediff(Pawn queen)
        {
            if (queen?.health == null || queen.health.hediffSet.HasHediff(InternalDefOf.XMT_DefensiveStature))
            {
                return;
            }

            queen.health.AddHediff(InternalDefOf.XMT_DefensiveStature);
        }

        public static void RemoveStanceHediffs(Pawn queen)
        {
            if (queen?.health == null)
            {
                return;
            }

            List<Hediff> stanceHediffs = queen.health.hediffSet.hediffs
                .Where(hediff => hediff.def == InternalDefOf.XMT_DefensiveStature)
                .ToList();
            foreach (Hediff hediff in stanceHediffs)
            {
                queen.health.RemoveHediff(hediff);
            }
        }

        public static void RefreshProtectionAura(Pawn queen)
        {
            if (!IsOperational(queen))
            {
                return;
            }

            float radius = JuggernautAbilityUtility.DefensiveStatureRadius(queen);
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(queen.Position, radius, true))
            {
                if (!cell.InBounds(queen.Map))
                {
                    continue;
                }

                foreach (Pawn target in cell.GetThingList(queen.Map).OfType<Pawn>())
                {
                    if (target == queen || target.Dead || target.Faction != queen.Faction)
                    {
                        continue;
                    }

                    HediffComp_DefensiveStatureProtection comp = FindProtectionComp(target, queen);
                    if (comp == null)
                    {
                        Hediff_DefensiveStatureProtection protection =
                            (Hediff_DefensiveStatureProtection)HediffMaker.MakeHediff(
                                InternalDefOf.XMT_DefensiveStatureProtection, target);
                        comp = protection.TryGetComp<HediffComp_DefensiveStatureProtection>();
                        if (comp == null)
                        {
                            Log.Error("[XMT] Defensive Stature protection hediff is missing its redirect comp.");
                            continue;
                        }

                        comp.Protector = queen;
                        target.health.AddHediff(protection);
                    }

                    comp.parent.TryGetComp<HediffComp_Disappears>()?.SetDuration(AuraRefreshDuration);
                }
            }
        }

        private static HediffComp_DefensiveStatureProtection FindProtectionComp(Pawn target, Pawn queen)
        {
            foreach (Hediff hediff in target.health.hediffSet.hediffs)
            {
                if (hediff.def != InternalDefOf.XMT_DefensiveStatureProtection)
                {
                    continue;
                }

                HediffComp_DefensiveStatureProtection comp = hediff.TryGetComp<HediffComp_DefensiveStatureProtection>();
                if (comp?.Protector == queen)
                {
                    return comp;
                }
            }

            return null;
        }
    }

    public class Hediff_DefensiveStatureProtection : HediffWithComps
    {
        public override bool TryMergeWith(Hediff other)
        {
            return false;
        }
    }

    public class HediffComp_DefensiveStatureState : HediffComp
    {
        public override bool CompShouldRemove => !DefensiveStatureUtility.IsOperational(Pawn);
    }

    public class HediffCompProperties_DefensiveStatureState : HediffCompProperties
    {
        public HediffCompProperties_DefensiveStatureState()
        {
            compClass = typeof(HediffComp_DefensiveStatureState);
        }
    }

    public class HediffComp_DefensiveStatureProtection : HediffComp_Shield
    {
        [ThreadStatic]
        private static int redirectDepth;

        public Pawn Protector;

        public override bool ShieldActive => true;

        public override bool CompShouldRemove => Protector == null
            || Protector.Destroyed
            || Protector.Dead
            || !Protector.Spawned
            || Pawn == null
            || Pawn.Destroyed
            || Pawn.Dead
            || Pawn.Faction != Protector.Faction
            || !DefensiveStatureUtility.IsOperational(Protector);

        public override void PreApplyDamage(ref DamageInfo dinfo, ref bool absorbed)
        {
            Pawn target = Pawn;
            if (absorbed
                || redirectDepth > 0
                || !CanRedirect(target)
                || !dinfo.Def.ExternalViolenceFor(target))
            {
                return;
            }

            float targetFactor = target.health.FactorForDamage(dinfo);
            if (ModsConfig.BiotechActive && target.genes != null)
            {
                targetFactor *= target.genes.FactorForDamage(dinfo);
            }

            if (targetFactor <= 0f || dinfo.Amount <= 0f)
            {
                return;
            }

            DamageInfo redirected = new DamageInfo(dinfo);
            redirected.SetAmount(dinfo.Amount / targetFactor);
            redirected.SetHitPart(null);

            try
            {
                redirectDepth++;
                Protector.TakeDamage(redirected);
                absorbed = true;
            }
            finally
            {
                redirectDepth--;
            }
        }

        public override bool AllowVerbCast(Verb verb)
        {
            return true;
        }

        public override void DrawAt(Vector3 drawPos)
        {
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
        }

        public override void CompPostPostRemoved()
        {
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref Protector, "defensiveStatureProtector");
        }

        private bool CanRedirect(Pawn target)
        {
            return target != null
                && target != Protector
                && Protector != null
                && DefensiveStatureUtility.IsOperational(Protector)
                && target.Spawned
                && !target.Dead
                && target.MapHeld == Protector.MapHeld
                && target.Faction == Protector.Faction
                && target.Position.InHorDistOf(Protector.Position,
                    JuggernautAbilityUtility.DefensiveStatureRadius(Protector));
        }
    }

    public class HediffCompProperties_DefensiveStatureProtection : HediffCompProperties_Shield
    {
        public HediffCompProperties_DefensiveStatureProtection()
        {
            compClass = typeof(HediffComp_DefensiveStatureProtection);
        }
    }
}
