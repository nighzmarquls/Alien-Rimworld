using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public interface IHiveInputProcessor
    {
        HiveInputSettings Inputs { get; }
        IEnumerable<QueenIngestibleResourceDef> InputResources { get; }
    }

    public class ITab_HiveInputs : ITab
    {
        private readonly ThingFilterUI.UIState state = new ThingFilterUI.UIState();
        private List<IHiveInputProcessor> group;
        private HiveInputSettings sharedInputs;
        private bool sharedInputsChanged;
        protected virtual bool ResinInputs => false;
        public override bool IsVisible => XMTUtility.QueenIsPlayer()
            && HiveInputSelection.TryGetProcessors(AllSelObjects, ResinInputs, out _);

        public ITab_HiveInputs()
        {
            size = new Vector2(420f, 480f);
            labelKey = "XMT_InputTab";
        }

        protected override void FillTab()
        {
            if (!HiveInputSelection.TryGetProcessors(AllSelObjects, ResinInputs, out List<IHiveInputProcessor> processors))
                return;
            HiveInputSettings inputs = SelectionInputs(processors);
            Rect area = new Rect(10f, 10f, size.x - 20f, size.y - 20f);
            if (processors.Count > 1)
            {
                Rect groupLabel = new Rect(area.x, area.y, area.width, 26f);
                Widgets.Label(groupLabel, "XMT_GroupInputs".Translate(processors.Count));
                TooltipHandler.TipRegion(groupLabel, "XMT_GroupInputsDesc".Translate());
                area.yMin += 28f;
            }
            if (Widgets.ButtonText(new Rect(area.x, area.y, 190f, 28f), (ResinInputs ? "XMT_CopyResinInputs" : "XMT_CopyInputs").Translate()))
                HiveInputClipboard.Copy(inputs, ResinInputs);
            if (StorageSettingsClipboard.HasCopiedSettings && Widgets.ButtonText(new Rect(area.x + 200f, area.y, 190f, 28f), (ResinInputs ? "XMT_PasteResinInputs" : "XMT_PasteInputs").Translate()))
                HiveInputClipboard.Paste(inputs, ResinInputs);
            ThingFilterUI.DoThingFilterConfigWindow(new Rect(area.x, area.y + 36f, area.width, area.height - 36f), state, inputs.GetStoreSettings().filter, inputs.GetParentStoreSettings().filter, 16, null, null, false, true, true, null, (AllSelObjects[0] as Thing)?.MapHeld);
            if (sharedInputsChanged)
                ApplySharedInputs();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            group = null;
            sharedInputs = null;
            sharedInputsChanged = false;
        }

        private HiveInputSettings SelectionInputs(List<IHiveInputProcessor> processors)
        {
            if (processors.Count == 1)
            {
                group = null;
                sharedInputs = null;
                sharedInputsChanged = false;
                return processors[0].Inputs;
            }
            if (group != null && group.SequenceEqual(processors))
                return sharedInputs;

            // Opening a mixed selection must not overwrite its members. Only editing this
            // temporary filter, or pasting into it, commits a common configuration.
            group = processors;
            sharedInputs = new HiveInputSettings(def => group.All(processor => processor.Inputs.GetParentStoreSettings().filter.Allows(def)));
            sharedInputs.GetStoreSettings().filter.CopyAllowancesFrom(group[0].Inputs.GetStoreSettings().filter);
            sharedInputs.Clamp();
            sharedInputsChanged = false;
            sharedInputs.SettingsChanged += () => sharedInputsChanged = true;
            return sharedInputs;
        }

        private void ApplySharedInputs()
        {
            // Category and allow-all operations issue many callbacks; propagate once per UI edit.
            sharedInputsChanged = false;
            foreach (IHiveInputProcessor processor in group)
            {
                processor.Inputs.GetStoreSettings().CopyFrom(sharedInputs.GetStoreSettings());
                processor.Inputs.Clamp();
            }
        }
    }

    public class ITab_AcidPitInputs : ITab_HiveInputs
    {
        protected override bool ResinInputs => true;

        public ITab_AcidPitInputs()
        {
            labelKey = "XMT_ResinTab";
        }
    }
}
