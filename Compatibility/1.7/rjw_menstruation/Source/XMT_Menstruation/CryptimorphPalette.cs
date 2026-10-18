using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace XMT_Menstruation
{
    public class CryptimorphStateColor
    {
        public string stage;
        public float saturationFactor = 1f;
        public float brightnessFactor = 1f;
        public float minimumBrightness = 0.2f;

        public Color Evaluate(Color blood)
        {
            Color.RGBToHSV(blood, out float hue, out float saturation, out float value);
            Color result = Color.HSVToRGB(hue, Mathf.Clamp01(saturation * saturationFactor),
                Mathf.Clamp01(Mathf.Max(minimumBrightness, value * brightnessFactor)));
            result.a = 1f;
            return result;
        }
    }

    public class CryptimorphPaletteDef : Def
    {
        public bool enabled = true;
        public bool neutralizeWombTint = true;
        public List<CryptimorphStateColor> states = new List<CryptimorphStateColor>();
        public CryptimorphStateColor State(string stage) => states.Find(s => s.stage == stage) ?? states.Find(s => s.stage == "Default");
    }

    public static class CryptimorphPalette
    {
        private static readonly Dictionary<Color32, Texture2D> Textures = new Dictionary<Color32, Texture2D>();
        public static CryptimorphPaletteDef Settings => DefDatabase<CryptimorphPaletteDef>.GetNamedSilentFail("XMT_CryptimorphPalette");
        public static bool Applies(Pawn pawn, CryptimorphPaletteDef settings) => settings?.enabled == true && CryptimorphTextures.IsEligible(pawn);

        public static Color WombTint(Color original, bool eligible, CryptimorphPaletteDef settings) =>
            eligible && settings?.enabled == true && settings.neutralizeWombTint ? Color.white : original;

        public static Texture2D StateTexture(Texture2D original, HediffComp womb, string stage)
        {
            CryptimorphPaletteDef settings = Settings;
            if (!Applies(womb?.Pawn, settings)) return original;
            CryptimorphStateColor state = settings.State(stage);
            if (state == null) return original;
            // Use the native blood accessor, including its VEF gene/mutant/race resolution.
            Color32 key = state.Evaluate(MenstruationApiShim.BloodColor(womb));
            if (!Textures.TryGetValue(key, out Texture2D texture))
                Textures[key] = texture = SolidColorMaterials.NewSolidColorTexture(key);
            return texture;
        }

        public static Color WindowTint(Color original, HediffComp womb) =>
            WombTint(original, Applies(womb?.Pawn, Settings), Settings);

        [HarmonyPatch]
        private static class CycleProgressColor
        {
            private static MethodBase TargetMethod() => AccessTools.PropertyGetter(MenstruationApiShim.WombType, "GetStageTexture");
            private static void Postfix(HediffComp __instance, ref Texture2D __result)
            {
                __result = StateTexture(__result, __instance, MenstruationApiShim.VisibleStage(__instance));
            }
        }

        [HarmonyPatch]
        private static class WindowColors
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                Type type = AccessTools.TypeByName("RJW_Menstruation.Dialog_WombStatus");
                yield return AccessTools.Method(type, "DrawWomb");
                yield return AccessTools.Method(type, "DrawInfos");
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
            {
                MethodInfo womb = AccessTools.Method(typeof(MenstruationApiShim), nameof(MenstruationApiShim.WindowWomb));
                MethodInfo guiColor = AccessTools.PropertySetter(typeof(GUI), nameof(GUI.color));
                bool panel = __originalMethod.Name == "DrawWomb";
                bool panelTintReplaced = false;
                foreach (CodeInstruction instruction in instructions)
                {
                    if (panel && !panelTintReplaced && instruction.Calls(guiColor))
                    {
                        // The first GUI tint is the hardcoded pink shared by the box
                        // and womb draw. Keep the setter and all later cleanup intact.
                        CodeInstruction first = new CodeInstruction(OpCodes.Ldarg_0);
                        first.MoveLabelsFrom(instruction);
                        first.MoveBlocksFrom(instruction);
                        yield return first;
                        yield return new CodeInstruction(OpCodes.Call, womb);
                        yield return CodeInstruction.Call(typeof(CryptimorphPalette), nameof(WindowTint));
                        panelTintReplaced = true;
                    }
                    yield return instruction;
                    if (!panel && instruction.operand is MethodInfo method && method.DeclaringType.FullName == "RJW_Menstruation.TextureCache" &&
                        method.Name.StartsWith("get_", StringComparison.Ordinal) && method.Name.EndsWith("Texture", StringComparison.Ordinal))
                    {
                        // Recolor each stat-bar texture at its call site, without
                        // changing the upstream static cache used by other pawns.
                        string stage = method.Name.Substring(4, method.Name.Length - 4 - "Texture".Length);
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, womb);
                        yield return new CodeInstruction(OpCodes.Ldstr, stage);
                        yield return CodeInstruction.Call(typeof(CryptimorphPalette), nameof(StateTexture));
                    }
                }
            }
        }
    }
}
