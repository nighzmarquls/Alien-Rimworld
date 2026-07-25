using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VefAbility = VEF.Abilities.Ability;
using VanillaAbilityDef = RimWorld.AbilityDef;

namespace Xenomorphtype
{
    internal sealed class PsychicDefenseCastContext
    {
        private Pawn defendingQueen;
        private bool resolved;
        private bool queenWon;
        private bool messageShown;

        internal bool Blocks(Pawn target, Pawn aggressor, Def abilityDef)
        {
            if (target == null || aggressor == null || target.MapHeld == null)
            {
                return false;
            }

            if (!resolved)
            {
                defendingQueen = PsychicDefenseUtility.FindDefendingQueen(
                    target,
                    aggressor,
                    PsychicDefenseUtility.Settings.targetedHeatCost);
                if (defendingQueen == null)
                {
                    return false;
                }

                resolved = true;
                if (!PsychicDefenseUtility.TryPayHeat(defendingQueen, PsychicDefenseUtility.Settings.targetedHeatCost))
                {
                    return false;
                }

                queenWon = PsychicDefenseUtility.QueenWinsContest(defendingQueen, aggressor);
            }

            if (!queenWon || !PsychicDefenseUtility.IsProtectedBy(target, defendingQueen))
            {
                return false;
            }

            if (!messageShown)
            {
                messageShown = true;
                PsychicDefenseUtility.ShowBlockedMessage(defendingQueen, aggressor, abilityDef);
            }

            return true;
        }
    }

    internal static class PsychicDefenseUtility
    {
        private sealed class VefCastState
        {
            internal VefAbility Ability;
            internal PsychicDefenseCastContext Context;
        }

        private const string SettingsDefName = "XMT_PsychicDefenseSettings";
        private static readonly PsychicDefenseSettingsDef fallbackSettings = new PsychicDefenseSettingsDef
        {
            defName = SettingsDefName
        };

        [ThreadStatic]
        private static Stack<PsychicDefenseCastContext> vanillaCastContexts;

        [ThreadStatic]
        private static Stack<VefCastState> vefCastStates;

        internal static PsychicDefenseSettingsDef Settings =>
            DefDatabase<PsychicDefenseSettingsDef>.GetNamedSilentFail(SettingsDefName) ?? fallbackSettings;

        internal static PsychicDefenseCastContext CurrentVanillaCastContext =>
            vanillaCastContexts != null && vanillaCastContexts.Count > 0 ? vanillaCastContexts.Peek() : null;

        internal static void BeginVanillaCast()
        {
            vanillaCastContexts ??= new Stack<PsychicDefenseCastContext>();
            vanillaCastContexts.Push(new PsychicDefenseCastContext());
        }

        internal static void EndVanillaCast()
        {
            if (vanillaCastContexts == null || vanillaCastContexts.Count == 0)
            {
                return;
            }

            vanillaCastContexts.Pop();
        }

        internal static void BeginVefCast(VefAbility ability)
        {
            vefCastStates ??= new Stack<VefCastState>();
            vefCastStates.Push(new VefCastState
            {
                Ability = ability,
                Context = new PsychicDefenseCastContext()
            });
        }

        internal static void EndVefCast()
        {
            if (vefCastStates == null || vefCastStates.Count == 0)
            {
                return;
            }

            vefCastStates.Pop();
        }

        internal static bool TryBlockVanillaTarget(Psycast psycast, Pawn target)
        {
            if (psycast?.pawn == null || target == null || !IsVanillaAbilityHarmful(psycast, target))
            {
                return false;
            }

            PsychicDefenseCastContext context = CurrentVanillaCastContext;
            if (context == null)
            {
                return false;
            }

            return context.Blocks(target, psycast.pawn, psycast.def);
        }

        internal static bool FilterVefTargets(VefAbility ability, ref RimWorld.Planet.GlobalTargetInfo[] targets)
        {
            if (ability?.pawn == null || targets == null || targets.Length == 0 || !IsVefPsycast(ability))
            {
                return false;
            }

            string defName = ability.def?.defName;
            if (ContainsDefName(Settings.ignoredVefAbilities, defName))
            {
                return false;
            }

            PsychicDefenseCastContext context = CurrentVefCastState?.Context ?? new PsychicDefenseCastContext();
            List<RimWorld.Planet.GlobalTargetInfo> filteredTargets = new List<RimWorld.Planet.GlobalTargetInfo>(targets.Length);
            bool changed = false;

            foreach (RimWorld.Planet.GlobalTargetInfo target in targets)
            {
                Pawn targetPawn = target.Thing as Pawn;
                if (targetPawn != null
                    && IsVefAbilityHarmful(ability, targetPawn)
                    && context.Blocks(targetPawn, ability.pawn, ability.def))
                {
                    changed = true;
                    continue;
                }

                filteredTargets.Add(target);
            }

            if (changed)
            {
                if (ability.def.targetCount > 1 && filteredTargets.Count < targets.Length)
                {
                    targets = Array.Empty<RimWorld.Planet.GlobalTargetInfo>();
                }
                else
                {
                    targets = filteredTargets.ToArray();
                }
            }

            return changed;
        }

        internal static bool TryBlockCurrentVefTarget(Pawn target)
        {
            VefCastState state = CurrentVefCastState;
            if (state?.Ability?.pawn == null
                || target == null
                || !IsVefPsycast(state.Ability)
                || !IsVefAbilityHarmful(state.Ability, target))
            {
                return false;
            }

            return state.Context.Blocks(target, state.Ability.pawn, state.Ability.def);
        }

        internal static void CompleteBlockedVefCast(VefAbility ability)
        {
            if (ability == null)
            {
                return;
            }

            System.Reflection.FieldInfo cooldownField = HarmonyLib.AccessTools.Field(typeof(VefAbility), "cooldown");
            cooldownField?.SetValue(ability, Find.TickManager.TicksGame + ability.GetCooldownForPawn());

            AbilityExtension_AbilityMod psycastExtension = ability.AbilityModExtensions?
                .FirstOrDefault(IsVpePsycastExtension);
            psycastExtension?.Cast(Array.Empty<RimWorld.Planet.GlobalTargetInfo>(), ability);
        }

        internal static bool TryProtectAmbient(Pawn target)
        {
            if (target?.MapHeld == null)
            {
                return false;
            }

            foreach (Pawn queen in target.MapHeld.mapPawns.AllPawnsSpawned
                .Where(queen => QueenCanProtect(queen) && IsProtectedBy(target, queen))
                .OrderBy(queen => queen.thingIDNumber))
            {
                if (GetProtectionAbility(queen)?.TryMaintainAmbientProtection() == true)
                {
                    return true;
                }
            }

            return false;
        }

        internal static Pawn FindDefendingQueen(Pawn target, Pawn aggressor, float heatCost)
        {
            if (target?.MapHeld == null)
            {
                return null;
            }

            return target.MapHeld.mapPawns.AllPawnsSpawned
                .Where(queen => queen != aggressor
                    && QueenCanProtect(queen)
                    && CanPayHeat(queen, heatCost)
                    && IsProtectedBy(target, queen))
                .OrderBy(queen => queen.thingIDNumber)
                .FirstOrDefault();
        }

        internal static bool QueenCanProtect(Pawn queen, bool requireActiveToggle = true)
        {
            if (!ModsConfig.RoyaltyActive
                || queen == null
                || !queen.Spawned
                || queen.Dead
                || queen.Downed
                || queen.MapHeld == null
                || queen.health?.capacities == null
                || queen.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) <= 0f
                || queen.psychicEntropy == null
                || queen.GetStatValue(StatDefOf.PsychicSensitivity) <= 0f)
            {
                return false;
            }

            CompQueen queenComp = queen.GetComp<CompQueen>();
            if (queenComp?.HasActiveEvolution(RoyalEvolutionDefOf.Evo_PsychicDefense) != true)
            {
                return false;
            }

            Ability_PsychicDefense ability = GetProtectionAbility(queen);
            return ability != null && (!requireActiveToggle || ability.Active);
        }

        internal static bool IsProtectedBy(Pawn target, Pawn queen)
        {
            if (target == null
                || queen == null
                || target.MapHeld != queen.MapHeld
                || target.Faction != queen.Faction)
            {
                return false;
            }

            return target == queen || XMTUtility.IsXenomorph(target) || target.HasBrainMutation();
        }

        internal static bool TryPayHeat(Pawn queen, float rawHeatCost)
        {
            if (!CanPayHeat(queen, rawHeatCost))
            {
                return false;
            }

            float cost = Mathf.Max(0f, rawHeatCost);
            if (cost <= 0f)
            {
                return true;
            }

            Pawn_PsychicEntropyTracker entropy = queen.psychicEntropy;
            return entropy.TryAddEntropy(cost, queen, scale: true, overLimit: false);
        }

        private static bool CanPayHeat(Pawn queen, float rawHeatCost)
        {
            if (queen?.psychicEntropy == null)
            {
                return false;
            }

            float cost = Mathf.Max(0f, rawHeatCost);
            if (cost <= 0f)
            {
                return true;
            }

            Pawn_PsychicEntropyTracker entropy = queen.psychicEntropy;
            if (queen.Faction == Faction.OfPlayer)
            {
                return !entropy.WouldOverflowEntropy(cost);
            }

            float scaledCost = cost * queen.GetStatValue(StatDefOf.PsychicEntropyGain);
            return entropy.EntropyValue + scaledCost <= entropy.MaxEntropy;
        }

        internal static bool QueenWinsContest(Pawn queen, Pawn aggressor)
        {
            PsychicDefenseSettingsDef settings = Settings;
            float queenPower = PsychicPower(queen, settings);
            float aggressorPower = PsychicPower(aggressor, settings);
            float denominator = queenPower + aggressorPower;
            float chance = denominator > 0f ? queenPower / denominator : 0.5f;
            chance = Mathf.Clamp(chance, settings.minimumContestChance, settings.maximumContestChance);
            return Rand.Chance(chance);
        }

        internal static bool IsInternallyEnumeratedVanillaAbility(VanillaAbilityDef abilityDef)
        {
            return ContainsDefName(Settings.internallyEnumeratedVanillaAbilities, abilityDef?.defName);
        }

        private static float PsychicPower(Pawn pawn, PsychicDefenseSettingsDef settings)
        {
            if (pawn == null)
            {
                return 0f;
            }

            float level = Mathf.Max(0, pawn.GetPsylinkLevel()) + Mathf.Max(0f, settings.basePsylinkPower);
            float sensitivity = Mathf.Max(0f, pawn.GetStatValue(StatDefOf.PsychicSensitivity));
            return level * sensitivity;
        }

        private static Ability_PsychicDefense GetProtectionAbility(Pawn queen)
        {
            return queen?.abilities?.AllAbilitiesForReading?
                .OfType<Ability_PsychicDefense>()
                .FirstOrDefault();
        }

        private static VefCastState CurrentVefCastState =>
            vefCastStates != null && vefCastStates.Count > 0 ? vefCastStates.Peek() : null;

        private static bool IsVanillaAbilityHarmful(Psycast psycast, Pawn target)
        {
            if (psycast?.def == null || target == null)
            {
                return false;
            }

            return psycast.def.hostile
                || ContainsDefName(Settings.alwaysHarmfulVanillaAbilities, psycast.def.defName)
                || psycast.pawn.HostileTo(target);
        }

        private static bool IsVefPsycast(VefAbility ability)
        {
            if (ability?.def?.modExtensions == null)
            {
                return false;
            }

            foreach (DefModExtension extension in ability.def.modExtensions)
            {
                if (!IsVpePsycastExtension(extension))
                {
                    continue;
                }

                Type extensionType = extension.GetType();
                System.Reflection.FieldInfo psychicField = HarmonyLib.AccessTools.Field(extensionType, "psychic");
                return psychicField?.GetValue(extension) is bool psychic && psychic;
            }

            return false;
        }

        private static bool IsVpePsycastExtension(DefModExtension extension)
        {
            Type extensionType = extension?.GetType();
            return extensionType != null
                && extensionType.Namespace == "VanillaPsycastsExpanded"
                && extensionType.Name.StartsWith("AbilityExtension_Psycast", StringComparison.Ordinal);
        }

        private static bool IsVefAbilityHarmful(VefAbility ability, Pawn target)
        {
            if (ability?.def == null || target == null)
            {
                return false;
            }

            if (ContainsDefName(Settings.alwaysHarmfulVefAbilities, ability.def.defName))
            {
                return true;
            }

            if (ability.def.isPositive.HasValue)
            {
                return !ability.def.isPositive.Value;
            }

            return ability.pawn.HostileTo(target);
        }

        private static bool ContainsDefName(List<string> defNames, string defName)
        {
            return !defName.NullOrEmpty()
                && defNames != null
                && defNames.Contains(defName);
        }

        internal static void ShowBlockedMessage(Pawn queen, Pawn aggressor, Def abilityDef)
        {
            if (queen?.MapHeld == null)
            {
                return;
            }

            string casterLabel = aggressor?.LabelShortCap ?? "XMT_UnknownPsycaster".Translate();
            string abilityLabel = abilityDef?.LabelCap ?? "XMT_UnknownPsycast".Translate();
            MoteMaker.ThrowText(
                queen.DrawPos,
                queen.MapHeld,
                "XMT_PsychicDefenseRepelled".Translate(casterLabel, abilityLabel),
                Color.white,
                3.65f);
        }
    }
}
