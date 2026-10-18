using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public static class HiveInputClipboard
    {
        // Vanilla exposes no silent Copy/Paste overload. Share its actual clipboard, replacing only
        // the stockpile-specific notification; pasted filters still pass through the validity clamp.
        private static readonly FieldInfo clipboardField = typeof(StorageSettingsClipboard).GetField("clipboard", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo copiedField = typeof(StorageSettingsClipboard).GetField("copied", BindingFlags.Static | BindingFlags.NonPublic);
        private static StorageSettings Clipboard => (StorageSettings)clipboardField.GetValue(null);

        public static void Copy(HiveInputSettings inputs, bool resin)
        {
            Clipboard.CopyFrom(inputs.GetStoreSettings());
            copiedField.SetValue(null, true);
            Messages.Message((resin ? "XMT_ResinFilterCopied" : "XMT_JellyFilterCopied").Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        public static void Paste(HiveInputSettings inputs, bool resin)
        {
            if (!StorageSettingsClipboard.HasCopiedSettings)
                return;
            inputs.GetStoreSettings().CopyFrom(Clipboard);
            inputs.Clamp();
            Messages.Message((resin ? "XMT_ResinFilterPasted" : "XMT_JellyFilterPasted").Translate(), MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
