using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class HiveWorkPresentationDef : Def
    {
        public WorkTypeDef workType;
        public string labelKey;
        public string descriptionKey;
    }

    public static class HiveWorkPresentation
    {
        public static HiveWorkPresentationDef For(WorkTypeDef type) => DefDatabase<HiveWorkPresentationDef>.AllDefsListForReading.FirstOrDefault(d => d.workType == type);
        public static string ShortLabel(WorkTypeDef type)
        {
            HiveWorkPresentationDef presentation = For(type);
            return XMTUtility.PlayerFactionIsCryptimorph() && presentation != null && !presentation.labelKey.NullOrEmpty() ? presentation.labelKey.Translate().ToString() : type.labelShort;
        }

        public static string Describe(WorkTypeDef type)
        {
            HiveWorkPresentationDef presentation = For(type);
            if (presentation == null)
                return null;
            string text = presentation.descriptionKey.Translate();
            var commands = HiveStructureUtility.Available.Where(d => d.automatic && d.workType == type).ToList();
            if (commands.Count > 0)
                text += "\n" + string.Join("\n", commands.Select(d => "• " + d.LabelCap + ": " + d.description));
            return text;
        }

        public static void HeaderTip(WorkTypeDef type, PawnTable table, ref string tip)
        {
            if (type == null)
                return;
            string description = Describe(type);
            if (description.NullOrEmpty())
                return;
            if (XMTUtility.PlayerFactionIsCryptimorph())
            {
                // Retain vanilla work lists, sorting instructions and priority help.
                string originalDescription = type.description;
                if (!originalDescription.NullOrEmpty())
                    tip = tip.Replace(originalDescription, description);
                if (!type.gerundLabel.NullOrEmpty())
                    tip = tip.Replace(type.gerundLabel.CapitalizeFirst(), ShortLabel(type).CapitalizeFirst());
                if (table?.PawnsListForReading.Any(p => p != null && !XMTUtility.IsXenomorph(p)) == true)
                    tip += "\n\n" + "XMT_HumanWorkRole".Translate(type.labelShort, originalDescription);
            }
            else if (table?.PawnsListForReading.Any(p => p != null && XMTUtility.IsXenomorph(p)) == true)
                tip += "\n\n" + "XMT_WorkTypeCryptimorphHeader".Translate() + "\n" + description;
        }
    }

    [HarmonyPatch(typeof(PawnColumnWorker_WorkPriority), nameof(PawnColumnWorker_WorkPriority.DoHeader))]
    public static class HiveWorkHeaderPatch
    {
        private sealed class HeaderState
        {
            public string label;
        }

        private static readonly ConditionalWeakTable<PawnColumnWorker_WorkPriority, HeaderState> labels = new ConditionalWeakTable<PawnColumnWorker_WorkPriority, HeaderState>();
        public static void Prefix(PawnColumnWorker_WorkPriority __instance, ref UnityEngine.Vector2 ___cachedWorkLabelSize)
        {
            HeaderState state = labels.GetValue(__instance, _ => new HeaderState());
            string label = HiveWorkPresentation.ShortLabel(__instance.def.workType);
            if (state.label == label)
                return;
            state.label = label;
            ___cachedWorkLabelSize = UnityEngine.Vector2.zero;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo label = AccessTools.Field(typeof(WorkTypeDef), nameof(WorkTypeDef.labelShort));
            MethodInfo replacement = AccessTools.Method(typeof(HiveWorkPresentation), nameof(HiveWorkPresentation.ShortLabel));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.LoadsField(label))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }

                yield return instruction;
            }
        }
    }
}
