using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using VEF.Abilities;
using VefAbility = VEF.Abilities.Ability;

namespace Xenomorphtype
{
    internal static class XMTPsychicDefensePatches
    {
        [HarmonyPatch(typeof(Psycast), nameof(Psycast.Activate), new Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
        private static class Patch_Psycast_Activate
        {
            private static void Prefix()
            {
                PsychicDefenseUtility.BeginVanillaCast();
            }

            private static Exception Finalizer(Exception __exception)
            {
                PsychicDefenseUtility.EndVanillaCast();
                return __exception;
            }
        }

        [HarmonyPatch(typeof(Psycast), "ApplyEffects", new Type[] { typeof(IEnumerable<CompAbilityEffect>), typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
        private static class Patch_Psycast_ApplyEffects
        {
            private static bool Prefix(Psycast __instance, LocalTargetInfo target)
            {
                if (PsychicDefenseUtility.IsInternallyEnumeratedVanillaAbility(__instance.def))
                {
                    return true;
                }

                return target.Pawn == null || !PsychicDefenseUtility.TryBlockVanillaTarget(__instance, target.Pawn);
            }
        }

        [HarmonyPatch(typeof(CompAbilityEffect_Neuroquake), "CanApplyEffects")]
        private static class Patch_CompAbilityEffect_Neuroquake_CanApplyEffects
        {
            private static void Postfix(CompAbilityEffect_Neuroquake __instance, Pawn p, ref bool __result)
            {
                if (!__result
                    || __instance.parent is not Psycast psycast
                    || PsychicDefenseUtility.CurrentVanillaCastContext == null)
                {
                    return;
                }

                if (PsychicDefenseUtility.TryBlockVanillaTarget(psycast, p))
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(typeof(ThoughtWorker_PsychicDrone), "CurrentStateInternal")]
        private static class Patch_ThoughtWorker_PsychicDrone_CurrentStateInternal
        {
            private static void Postfix(Pawn p, ref ThoughtState __result)
            {
                if (__result.Active && PsychicDefenseUtility.TryProtectAmbient(p))
                {
                    __result = ThoughtState.Inactive;
                }
            }
        }

        [HarmonyPatch(typeof(GameCondition_PsychicSuppression), "CheckPawn")]
        private static class Patch_GameCondition_PsychicSuppression_CheckPawn
        {
            private static bool Prefix(Pawn pawn)
            {
                if (!PsychicDefenseUtility.TryProtectAmbient(pawn))
                {
                    return true;
                }

                Hediff suppression = pawn.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.PsychicSuppression);
                if (suppression != null)
                {
                    pawn.health.RemoveHediff(suppression);
                }

                return false;
            }
        }

        [HarmonyPatch]
        private static class Patch_VefAbility_Cast
        {
            [ThreadStatic]
            private static int castDepth;

            private static IEnumerable<MethodBase> TargetMethods()
            {
                Type baseType = typeof(VefAbility);
                Type[] signature = { typeof(GlobalTargetInfo[]) };
                HashSet<MethodBase> methods = new HashSet<MethodBase>();

                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (Type type in GetLoadableTypes(assembly))
                    {
                        if (type == null || !baseType.IsAssignableFrom(type) || type.IsAbstract)
                        {
                            continue;
                        }

                        MethodInfo method = type.GetMethod(
                            nameof(VefAbility.Cast),
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                            binder: null,
                            types: signature,
                            modifiers: null);

                        if (method != null)
                        {
                            methods.Add(method);
                        }
                    }
                }

                MethodInfo baseMethod = AccessTools.Method(baseType, nameof(VefAbility.Cast), signature);
                if (baseMethod != null)
                {
                    methods.Add(baseMethod);
                }

                return methods;
            }

            private static bool Prefix(VefAbility __instance, MethodBase __originalMethod, ref GlobalTargetInfo[] targets)
            {
                bool outermost = castDepth == 0;
                castDepth++;
                if (!outermost)
                {
                    return true;
                }

                PsychicDefenseUtility.BeginVefCast(__instance);
                bool changed = PsychicDefenseUtility.FilterVefTargets(__instance, ref targets);
                if (!changed || targets.Length > 0 || __originalMethod.DeclaringType == typeof(VefAbility))
                {
                    return true;
                }

                PsychicDefenseUtility.CompleteBlockedVefCast(__instance);
                return false;
            }

            private static Exception Finalizer(Exception __exception)
            {
                if (castDepth == 1)
                {
                    PsychicDefenseUtility.EndVefCast();
                }

                castDepth = Math.Max(0, castDepth - 1);
                return __exception;
            }
        }

        [HarmonyPatch]
        private static class Patch_VpeNeuroquake_CanApplyEffects
        {
            private static bool Prepare()
            {
                return AccessTools.TypeByName("VanillaPsycastsExpanded.AbilityExtension_Neuroquake") != null;
            }

            private static MethodBase TargetMethod()
            {
                Type type = AccessTools.TypeByName("VanillaPsycastsExpanded.AbilityExtension_Neuroquake");
                return AccessTools.Method(type, "CanApplyEffects");
            }

            private static void Postfix(Pawn p, ref bool __result)
            {
                if (__result && PsychicDefenseUtility.TryBlockCurrentVefTarget(p))
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch]
        private static class Patch_VpePsychicDrone_TargetPredicate
        {
            private static bool Prepare()
            {
                return AccessTools.TypeByName("VanillaPsycastsExpanded.Hediff_PsychicDrone") != null;
            }

            private static MethodBase TargetMethod()
            {
                Type type = AccessTools.TypeByName("VanillaPsycastsExpanded.Hediff_PsychicDrone");
                return AccessTools.Method(type, "<Tick>b__6_0");
            }

            private static void Postfix(Pawn __0, ref bool __result)
            {
                if (__result && PsychicDefenseUtility.TryProtectAmbient(__0))
                {
                    __result = false;
                }
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            if (assembly == null)
            {
                return Enumerable.Empty<Type>();
            }

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }
    }
}
