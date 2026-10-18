using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace Xenomorphtype
{
    internal class XMTUIPatches
    {
        [HarmonyPatch(typeof(Dialog_NamePawn), nameof(Dialog_NamePawn.DoWindowContents))]
        public static class Patch_Dialog_NamePawn_DoWindowContents
        {
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo titleGetter = AccessTools.PropertyGetter(typeof(Pawn_StoryTracker), nameof(Pawn_StoryTracker.Title));
                MethodInfo safeTitle = AccessTools.Method(typeof(Patch_Dialog_NamePawn_DoWindowContents), nameof(GetTitle));
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.Calls(titleGetter))
                    {
                        // Non-humanlike Anomaly entities take the generic rename-message branch
                        // despite having no story tracker. Preserve the rest of the vanilla dialog.
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = safeTitle;
                    }
                    yield return instruction;
                }
            }

            public static string GetTitle(Pawn_StoryTracker story)
            {
                return story?.Title ?? string.Empty;
            }
        }

        [HarmonyPatch(typeof(PawnNamingUtility), nameof(PawnNamingUtility.NamePawnDialog))]
        public static class Patch_PawnNamingUtility_NamePawnDialog
        {
            [HarmonyPrefix]
            public static void Prefix(Pawn pawn)
            {
                // Wild larvae can be recruited without the normal animal-taming naming step.
                // Vanilla's dialog assumes Name is non-null, including for animals.
                if (pawn != null && pawn.Name == null && pawn.GetComp<CompLarvalGenes>() != null)
                {
                    pawn.Name = PawnBioAndNameGenerator.GeneratePawnName(pawn)
                        ?? new NameSingle(pawn.KindLabel);
                }
            }
        }

        [HarmonyPatch(typeof(PawnColumnWorker_WorkPriority), "GetHeaderTip")]
        public static class Patch_PawnColumnWorker_WorkPriority_GetHeaderTip
        {
            private static readonly FieldInfo pawnColumnDefField = AccessTools.Field(typeof(PawnColumnWorker), "def");

            [HarmonyPostfix]
            public static void Postfix(PawnColumnWorker_WorkPriority __instance, PawnTable table, ref string __result)
            {
                PawnColumnDef columnDef = pawnColumnDefField?.GetValue(__instance) as PawnColumnDef;
                WorkTypeDef workType = columnDef?.workType;
                HiveWorkPresentation.HeaderTip(workType, table, ref __result);
            }
        }

        [HarmonyPatch(typeof(PawnUIOverlay), nameof(PawnUIOverlay.DrawPawnGUIOverlay))]
        public static class Patch_PawnUIOverlay_DrawPawnGUIOverlay
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn ___pawn)
            {
                if(___pawn != null)
                {
                    CompStealth stealth = ___pawn.GetComp<CompStealth>();
                    if (stealth != null)
                    {
                        if (___pawn.Faction == null || !___pawn.Faction.IsPlayer)
                        {
                            
                            return false;
                            
                        }
                    }
                }

                //DO REGULAR OVERLAY
                return true;
            }

        }
        
    }
}
