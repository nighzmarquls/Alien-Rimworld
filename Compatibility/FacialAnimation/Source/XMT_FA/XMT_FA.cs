using FacialAnimation;
using System;
using System.Collections.Generic;
using Verse;

namespace XMT_FA
{
    [StaticConstructorOnStartup]
    public static class XMT_FA
    {
        private const string CompatibilityVersionMarkerPrefix = "XMT_FA_CompatibilityVersion_";
        private const int CurrentCompatibilityVersion = 1;

        private static readonly string[] InitiallyDisabledRaceKeys =
        {
            "XMT_Starbeast_AlienRace-null",
            "XMT_Royal_AlienRace-null",
            "XMT_StarCutie_AlienRace-null"
        };

        static XMT_FA()
        {
            FacialAnimationModSettings settings = FacialAnimationMod.Settings;
            if (settings == null)
            {
                Log.Warning("[Alien|Rimworld] Facial Animation settings were unavailable; race defaults were not applied.");
                return;
            }

            settings.IgnoreEnableAnimationForRaceXenoTypeList ??= new List<string>();
            List<string> ignoredRaceXenotypes = settings.IgnoreEnableAnimationForRaceXenoTypeList;
            int previousVersion = GetCompatibilityVersion(ignoredRaceXenotypes);
            if (previousVersion >= CurrentCompatibilityVersion)
            {
                return;
            }

            if (previousVersion < 1)
            {
                foreach (string raceKey in InitiallyDisabledRaceKeys)
                {
                    if (!ignoredRaceXenotypes.Contains(raceKey))
                    {
                        ignoredRaceXenotypes.Add(raceKey);
                    }
                }
            }

            ignoredRaceXenotypes.RemoveAll(IsCompatibilityVersionMarker);
            ignoredRaceXenotypes.Add(CompatibilityVersionMarkerPrefix + CurrentCompatibilityVersion);
            settings.Write();

            Log.Message("[Alien|Rimworld] applied Facial Animation compatibility defaults version "
                + CurrentCompatibilityVersion + ".");
        }

        private static int GetCompatibilityVersion(List<string> ignoredRaceXenotypes)
        {
            int version = 0;
            foreach (string entry in ignoredRaceXenotypes)
            {
                if (entry != null
                    && entry.StartsWith(CompatibilityVersionMarkerPrefix, StringComparison.Ordinal)
                    && int.TryParse(entry.Substring(CompatibilityVersionMarkerPrefix.Length), out int parsedVersion))
                {
                    version = Math.Max(version, parsedVersion);
                }
            }

            return version;
        }

        private static bool IsCompatibilityVersionMarker(string entry)
        {
            return entry != null && entry.StartsWith(CompatibilityVersionMarkerPrefix, StringComparison.Ordinal);
        }
    }
}
