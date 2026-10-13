using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class Verb_CastNetworkStunPayload : Verb_CastAbility
    {
        public override void DrawHighlight(LocalTargetInfo target)
        {
            Pawn caster = CasterPawn;
            if (caster != null)
            {
                GenDraw.DrawRadiusRing(caster.Position, NetworkStunPayloadUtility.Range(caster));
            }

            Ability?.DrawEffectPreviews(target);
        }
    }

    public class Ability_NetworkStunPayload : Ability
    {
        public Ability_NetworkStunPayload()
        {
        }

        public Ability_NetworkStunPayload(Pawn pawn) : base(pawn)
        {
        }

        public Ability_NetworkStunPayload(Pawn pawn, AbilityDef def) : base(pawn, def)
        {
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            CompAbilityNetworkStunPayload comp = CompOfType<CompAbilityNetworkStunPayload>();
            if (comp == null || !comp.HasEligibleTargets(target.Cell))
            {
                if (pawn?.Faction == Faction.OfPlayer)
                {
                    Messages.Message("XMT_NetworkStunEmpty".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            bool activated = base.Activate(target, dest);
            if (activated)
            {
                ResetCooldown();
                StartCooldown(NetworkStunPayloadUtility.CooldownTicks(pawn));
            }

            return activated;
        }

        public override string Tooltip
        {
            get
            {
                string baseTooltip = base.Tooltip;
                if (pawn == null)
                {
                    return baseTooltip;
                }

                float range = NetworkStunPayloadUtility.Range(pawn);
                return baseTooltip + "\n\n" + "XMT_NetworkStunStats".Translate(
                    pawn.GetStatValue(StatDefOf.PsychicSensitivity).ToStringPercent(),
                    NetworkStunPayloadUtility.TotalBandwidth(pawn),
                    range.ToString("0.#"),
                    NetworkStunPayloadUtility.Radius(pawn).ToString("0.#"),
                    (range + NetworkStunPayloadUtility.Radius(pawn)).ToString("0.#"),
                    NetworkStunPayloadUtility.DurationTicks(pawn).ToStringTicksToPeriod(),
                    NetworkStunPayloadUtility.CooldownTicks(pawn).ToStringTicksToPeriod());
            }
        }
    }

    public class CompAbilityNetworkStunPayload : CompAbilityEffect
    {
        private static readonly Color PreviewColor = new Color(0.25f, 0.75f, 0.9f, 0.75f);
        private static readonly HashSet<string> UnsupportedTurretWarnings = new HashSet<string>();

        public override bool GizmoDisabled(out string reason)
        {
            if (!NetworkStunPayloadUtility.CanUse(parent.pawn, out reason))
            {
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            if (!NetworkStunPayloadUtility.CanUse(caster, out string reason))
            {
                if (throwMessages && !reason.NullOrEmpty())
                {
                    Messages.Message(reason, caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (!target.Cell.IsValid || !target.Cell.InBounds(caster.Map)
                || !target.Cell.InHorDistOf(caster.Position, NetworkStunPayloadUtility.Range(caster)))
            {
                if (throwMessages)
                {
                    Messages.Message("XMT_NetworkStunOutOfRange".Translate(NetworkStunPayloadUtility.Range(caster)),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (!HasEligibleTargets(target.Cell))
            {
                if (throwMessages)
                {
                    Messages.Message("XMT_NetworkStunEmpty".Translate(), caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !HasEligibleTargets(target.Cell))
            {
                return;
            }

            int durationTicks = NetworkStunPayloadUtility.DurationTicks(caster);
            HashSet<Faction> affectedFactions = new HashSet<Faction>();
            foreach (Thing thing in EligibleTargets(target.Cell).ToList())
            {
                HarmfulAbilityUtility.RegisterFactionHarm(caster, thing, affectedFactions, -5, false);
                if (thing is Pawn pawn)
                {
                    pawn.stances?.stunner?.StunFor(durationTicks, caster, addBattleLog: false, showMote: true);
                }
                else
                {
                    DisableTurret(thing, durationTicks, caster);
                }
            }

            FleckMaker.Static(target.Cell, caster.Map, FleckDefOf.PsycastAreaEffect,
                Mathf.Max(1f, NetworkStunPayloadUtility.Radius(caster)));
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map != null && target.Cell.IsValid)
            {
                GenDraw.DrawFieldEdges(NetworkStunPayloadUtility.AffectedCells(caster, target.Cell).ToList(), PreviewColor);
            }
        }

        public bool HasEligibleTargets(IntVec3 center)
        {
            return EligibleTargets(center).Any();
        }

        private IEnumerable<Thing> EligibleTargets(IntVec3 center)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !center.IsValid)
            {
                yield break;
            }

            HashSet<Thing> yielded = new HashSet<Thing>();
            foreach (IntVec3 cell in NetworkStunPayloadUtility.AffectedCells(caster, center))
            {
                foreach (Thing thing in cell.GetThingList(caster.Map))
                {
                    if (yielded.Add(thing) && NetworkStunPayloadUtility.IsEligibleTarget(caster, thing))
                    {
                        yield return thing;
                    }
                }
            }
        }

        private static void DisableTurret(Thing turret, int durationTicks, Pawn caster)
        {
            CompStunnable stunnable = turret.TryGetComp<CompStunnable>();
            if (stunnable?.StunHandler != null)
            {
                stunnable.StunHandler.StunFor(durationTicks, caster, addBattleLog: false, showMote: true);
                return;
            }

            CompBreakdownable breakdownable = turret.TryGetComp<CompBreakdownable>();
            if (breakdownable != null)
            {
                if (!breakdownable.BrokenDown)
                {
                    breakdownable.DoBreakdown();
                }
                return;
            }

            CompFlickable flickable = turret.TryGetComp<CompFlickable>();
            if (flickable != null)
            {
                flickable.SwitchIsOn = false;
                return;
            }

            CompPowerTrader power = turret.TryGetComp<CompPowerTrader>();
            if (power != null)
            {
                power.PowerOn = false;
                return;
            }

            string warningKey = turret.def?.defName ?? turret.GetType().FullName;
            if (UnsupportedTurretWarnings.Add(warningKey))
            {
                Log.Warning($"[XMT] Network Stun Payload could not stun, break down, or disable turret {turret.LabelCap} ({warningKey}).");
            }
        }
    }

    public class CompProperties_AbilityNetworkStunPayload : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityNetworkStunPayload()
        {
            compClass = typeof(CompAbilityNetworkStunPayload);
        }
    }

    public static class NetworkStunPayloadUtility
    {
        public static int TotalBandwidth(Pawn pawn)
        {
            return Mathf.Max(0, pawn?.mechanitor?.TotalBandwidth ?? 0);
        }

        public static int Range(Pawn pawn)
        {
            float psychicSensitivity = pawn?.GetStatValue(StatDefOf.PsychicSensitivity) ?? 0f;
            return (int)EvolutionScalingUtility.Scale(8f, 4f, 1f, 12f,
                EvolutionScalingCurve.LinearDelta, EvolutionScalingRounding.Round,
                new EvolutionScalingFactor(psychicSensitivity, 1.25f),
                new EvolutionScalingFactor(TotalBandwidth(pawn), 15f));
        }

        public static float Radius(Pawn pawn)
        {
            return Range(pawn) * 0.5f;
        }

        public static int DurationTicks(Pawn pawn)
        {
            return (int)EvolutionScalingUtility.Scale(900f, 0f, 0f, int.MaxValue,
                EvolutionScalingCurve.Proportional, EvolutionScalingRounding.Round,
                new EvolutionScalingFactor(TotalBandwidth(pawn), 15f));
        }

        public static int CooldownTicks(Pawn pawn)
        {
            return (int)EvolutionScalingUtility.Scale(2500f, 0f, 600f, 7500f,
                EvolutionScalingCurve.Inverse, EvolutionScalingRounding.Round,
                new EvolutionScalingFactor(TotalBandwidth(pawn), 15f));
        }

        public static bool CanUse(Pawn pawn, out string reason)
        {
            if (pawn?.GetComp<CompQueen>()?.HasFunctionalEvolutionFeature(RoyalEvolutionDefOf.Evo_MechanitorArray) != true)
            {
                reason = "XMT_NetworkStunNoArray".Translate();
                return false;
            }

            if (pawn.mechanitor == null || TotalBandwidth(pawn) <= 0)
            {
                reason = "XMT_NetworkStunNoBandwidth".Translate();
                return false;
            }

            if (pawn.GetStatValue(StatDefOf.PsychicSensitivity) <= 0f)
            {
                reason = "XMT_NetworkStunNoSensitivity".Translate();
                return false;
            }

            reason = null;
            return true;
        }

        public static IEnumerable<IntVec3> AffectedCells(Pawn caster, IntVec3 center)
        {
            if (caster?.Map == null || !center.IsValid)
            {
                yield break;
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, Radius(caster), true))
            {
                if (cell.InBounds(caster.Map))
                {
                    yield return cell;
                }
            }
        }

        public static bool IsEligibleTarget(Pawn caster, Thing target)
        {
            if (caster == null || target == null || target.Destroyed || !target.Spawned || target.MapHeld != caster.MapHeld)
            {
                return false;
            }

            if (target is Pawn pawn)
            {
                return !pawn.Dead && XMTUtility.IsInorganic(pawn)
                    && MechanitorUtility.GetOverseer(pawn) != caster
                    && !HasAttachedSubverter(pawn);
            }

            return target is Building_Turret && target.TryGetComp<CompMannable>() == null;
        }

        private static bool HasAttachedSubverter(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.hediffs
                .OfType<HediffWithComps>()
                .Any(hediff => hediff.TryGetComp<HediffComp_InorganicSubverterAttachment>()?.attachedPawn != null) == true;
        }
    }
}
