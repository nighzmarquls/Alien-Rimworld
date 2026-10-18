using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    // Uses the vanilla clipboard, but never registers a slot group or attracts storage hauling.
    public sealed class HiveInputSettings : IExposable, IStoreSettingsParent
    {
        private StorageSettings settings;
        private StorageSettings fixedSettings;
        private readonly Func<ThingDef, bool> valid;
        private readonly Func<ThingDef, bool> defaultAllowed;
        public event Action SettingsChanged;
        public HiveInputSettings(Func<ThingDef, bool> valid, Func<ThingDef, bool> defaultAllowed = null)
        {
            this.valid = valid;
            this.defaultAllowed = defaultAllowed;
        }

        public static bool PotentiallyConvertible(ThingDef def, List<ResourceConversionEntry> resources, Thing consumer)
        {
            if (ResourceHelperUtility.CanConvertDefinition(def, resources, consumer))
                return true;
            return def.MadeFromStuff && resources.Any(e => e?.resourceDef != null && ResourceHelperUtility.Available(e.resourceDef, consumer) && DefDatabase<ThingDef>.AllDefsListForReading.Any(stuff => stuff.IsStuff && def.stuffCategories?.Any(c => stuff.stuffProps.categories.Contains(c)) == true && ResourceHelperUtility.EligibleFraction(def, stuff, e.resourceDef) > 0f));
        }

        public bool StorageTabVisible => true;

        public StorageSettings GetParentStoreSettings()
        {
            if (fixedSettings == null)
            {
                fixedSettings = new StorageSettings();
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                    if (def.category == ThingCategory.Item && valid(def))
                        fixedSettings.filter.SetAllow(def, true);
            }

            return fixedSettings;
        }

        public StorageSettings GetStoreSettings()
        {
            if (settings == null)
            {
                settings = new StorageSettings(this);
                settings.filter.CopyAllowancesFrom(GetParentStoreSettings().filter);
                if (defaultAllowed != null)
                    foreach (ThingDef def in settings.filter.AllowedThingDefs.ToList())
                        if (!defaultAllowed(def))
                            settings.filter.SetAllow(def, false);
            }

            return settings;
        }

        public void Notify_SettingsChanged()
        {
            SettingsChanged?.Invoke();
        }

        public bool Allows(Thing thing) => thing != null && GetStoreSettings().filter.Allows(thing);
        public void Clamp()
        {
            foreach (ThingDef def in GetStoreSettings().filter.AllowedThingDefs.ToList())
                if (!GetParentStoreSettings().filter.Allows(def))
                    settings.filter.SetAllow(def, false);
        }

        public void ExposeData() => Scribe_Deep.Look(ref settings, "inputSettings", this);
    }
}
