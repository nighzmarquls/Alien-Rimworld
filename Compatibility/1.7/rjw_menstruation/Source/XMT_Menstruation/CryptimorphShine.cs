using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace XMT_Menstruation
{
    // Static painted highlights for the UI, not a lighting-dependent world shader.
    // Only textures loaded from the cryptimorph namespace can acquire a mask.
    public static class CryptimorphShine
    {
        private static readonly Dictionary<Texture, Texture2D> Masks = new Dictionary<Texture, Texture2D>();

        public static string MaskPath(string path, CryptimorphTextureSetDef settings)
        {
            if (path == null || string.IsNullOrEmpty(settings?.prefix) || string.IsNullOrEmpty(settings.shineMaskPrefix) ||
                !path.StartsWith(settings.prefix, StringComparison.Ordinal)) return null;
            string relative = path.Substring(settings.prefix.Length);
            return settings.Contains(relative) ? settings.shineMaskPrefix + relative : null;
        }

        public static void Register(string path, Texture2D texture, CryptimorphTextureSetDef settings)
        {
            if (texture == null || Masks.ContainsKey(texture)) return;
            string maskPath = MaskPath(path, settings);
            if (maskPath == null) return;
            Texture2D mask = ContentFinder<Texture2D>.Get(maskPath, false);
            Masks[texture] = mask != null && mask.width == texture.width && mask.height == texture.height ? mask : null;
        }

        private static bool TryMask(Texture texture, out Texture2D mask)
        {
            mask = null;
            return texture != null && Masks.TryGetValue(texture, out mask) && mask != null &&
                CryptimorphTextures.Settings.shineOpacity > 0f;
        }

        // 1.7 SHIM: use overloads verified on the installed UnityStubs/Waylith build.
        // Keep all original geometry, UVs, alpha and GUI color restoration intact.
        public static void DrawTexture(Rect rect, Texture texture, ScaleMode mode)
        {
            GUI.DrawTexture(rect, texture, mode);
            if (!TryMask(texture, out Texture2D mask)) return;
            Color previous = GUI.color;
            try
            {
                GUI.color = new Color(1f, 1f, 1f, previous.a * Mathf.Clamp01(CryptimorphTextures.Settings.shineOpacity));
                GUI.DrawTexture(rect, mask, mode);
            }
            finally { GUI.color = previous; }
        }

        public static void DrawTexture(Rect rect, Texture texture, ScaleMode mode, bool alphaBlend, float aspect,
            Color color, float borderWidth, float borderRadius)
        {
            GUI.DrawTexture(rect, texture, mode, alphaBlend, aspect, color, borderWidth, borderRadius);
            if (!TryMask(texture, out Texture2D mask)) return;
            Color previous = GUI.color;
            try
            {
                GUI.color = new Color(1f, 1f, 1f, previous.a);
                Color highlight = new Color(1f, 1f, 1f, color.a * Mathf.Clamp01(CryptimorphTextures.Settings.shineOpacity));
                GUI.DrawTexture(rect, mask, mode, true, aspect, highlight, borderWidth, borderRadius);
            }
            finally { GUI.color = previous; }
        }

        public static void DrawTextureFitted(Rect rect, Texture texture, float scale, Vector2 proportions,
            Rect texCoords, float angle, Material material, float borderWidth)
        {
            Widgets.DrawTextureFitted(rect, texture, scale, proportions, texCoords, angle, material, borderWidth);
            if (!TryMask(texture, out Texture2D mask)) return;
            Color previous = GUI.color;
            try
            {
                GUI.color = new Color(1f, 1f, 1f, previous.a * Mathf.Clamp01(CryptimorphTextures.Settings.shineOpacity));
                Widgets.DrawTextureFitted(rect, mask, scale, proportions, texCoords, angle, material, borderWidth);
            }
            finally { GUI.color = previous; }
        }

        [HarmonyPatch]
        private static class DrawHighlights
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (var target in new[] {
                    ("RJW_Menstruation.Utility", "DrawBreastIcon"),
                    ("RJW_Menstruation.Dialog_WombStatus", "DrawVagina"),
                    ("RJW_Menstruation.Dialog_WombStatus", "DrawWomb"),
                    ("RJW_Menstruation.MenstruationUtility", "DrawEggOverlay"),
                    ("RJW_Menstruation.Gizmo_Womb", "DrawIcon") })
                    yield return AccessTools.Method(AccessTools.TypeByName(target.Item1), target.Item2);
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.operand is MethodInfo method &&
                        (method.DeclaringType == typeof(GUI) && method.Name == nameof(GUI.DrawTexture) ||
                         method.DeclaringType == typeof(Widgets) && method.Name == nameof(Widgets.DrawTextureFitted)))
                    {
                        MethodInfo replacement = AccessTools.Method(typeof(CryptimorphShine), method.Name,
                            method.GetParameters().Select(p => p.ParameterType).ToArray());
                        if (replacement != null) instruction.operand = replacement;
                    }
                    yield return instruction;
                }
            }
        }
    }
}
