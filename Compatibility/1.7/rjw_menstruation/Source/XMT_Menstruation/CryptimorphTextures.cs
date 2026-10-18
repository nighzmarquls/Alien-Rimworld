using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Xenomorphtype;
using XMT_RJW;

namespace XMT_Menstruation
{
    public class CryptimorphTextureSetDef : Def
    {
        public string prefix;
        public string defaultPrefix;
        public string shineMaskPrefix = "XMT/Menstruation/Masks/Cryptimorph/";
        public float shineOpacity = 0.65f;
        public List<string> sourcePaths = new List<string>();
        public List<GeneDef> derivedGenes = new List<GeneDef>();
        private HashSet<string> paths;
        public bool Contains(string path) => (paths ??= new HashSet<string>(sourcePaths, StringComparer.Ordinal)).Contains(path);
    }

    public static class CryptimorphTextures
    {
        // Texture requests are scoped to one upstream pawn/comp operation. Never replace
        // shared human assets or infer lineage from a pregnancy, fluid exposure or essence.
        [ThreadStatic] private static bool grayscale;
        public static CryptimorphTextureSetDef Settings =>
            DefDatabase<CryptimorphTextureSetDef>.GetNamedSilentFail("XMT_CryptimorphTextures");

        public static bool MatchesLineage(ThingDef race, PawnKindDef kind, IEnumerable<GeneDef> activeGenes,
            bool perfectOrganism, CryptimorphTextureSetDef settings)
        {
            if (race?.HasModExtension<XMT_HorrorPawnExtension>() == true ||
                kind?.HasModExtension<XMT_HorrorPawnExtension>() == true || perfectOrganism) return true;
            return activeGenes?.Any(g => g != null &&
                (g.geneClass != null && typeof(XenomorphGene).IsAssignableFrom(g.geneClass) ||
                    settings?.derivedGenes.Contains(g) == true)) == true;
        }

        public static bool IsEligible(Pawn pawn) => pawn != null && MatchesLineage(pawn.def, pawn.kindDef,
            pawn.genes?.GenesListForReading.Where(g => g.Active).Select(g => g.def),
            pawn.AllComps?.Any(c => c is CompPerfectOrganism) == true, Settings);

        public static bool Enter(bool enabled) { bool previous = grayscale; grayscale = enabled; return previous; }
        public static void Restore(bool previous) { grayscale = previous; }
        public static string Remap(string path, bool enabled, CryptimorphTextureSetDef settings)
        {
            if (!enabled || path == null || settings == null) return path;
            string relative = !string.IsNullOrEmpty(settings.defaultPrefix) && path.StartsWith(settings.defaultPrefix, StringComparison.Ordinal)
                ? path.Substring(settings.defaultPrefix.Length) : path;
            return settings.Contains(relative) ? settings.prefix + relative : path;
        }

        [HarmonyPatch]
        private static class ScopedTextureLookup
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (string name in new[] { "RJW_Menstruation.MenstruationUtility", "RJW_Menstruation.Utility", "RJW_Menstruation.Pawn_GetGizmos" })
                {
                    Type type = AccessTools.TypeByName(name);
                    if (type == null) continue;
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        // Additive fluid overlays always use the shared upstream textures/tints.
                        if (method.Name == "GetCumIcon") continue;
                        // The gizmo factory also loads overlays directly, outside the icon helpers.
                        if (!(method.Name.Contains("Icon") || method.Name == "DrawEggOverlay" ||
                            method.Name == "CreateGizmo_WombStatus")) continue;
                        if (method.GetParameters().Any(p => p.ParameterType == typeof(Pawn) ||
                            typeof(HediffComp).IsAssignableFrom(p.ParameterType))) yield return method;
                    }
                }
            }
            private static void Prefix(object[] __args, out bool __state)
            {
                Pawn pawn = __args.OfType<Pawn>().FirstOrDefault() ?? __args.OfType<HediffComp>().FirstOrDefault()?.Pawn;
                __state = Enter(IsEligible(pawn));
            }
            private static Exception Finalizer(Exception __exception, bool __state)
            {
                Restore(__state);
                return __exception;
            }
        }

        [HarmonyPatch(typeof(ContentFinder<Texture2D>), nameof(ContentFinder<Texture2D>.Get),
            new Type[] { typeof(string), typeof(bool) })]
        private static class FindGrayscaleTexture
        {
            private static void Prefix(ref string __0)
            {
                if (grayscale) __0 = Remap(__0, true, Settings);
            }
            private static void Postfix(string __0, Texture2D __result)
            {
                CryptimorphShine.Register(__0, __result, Settings);
            }
        }

    }
}
