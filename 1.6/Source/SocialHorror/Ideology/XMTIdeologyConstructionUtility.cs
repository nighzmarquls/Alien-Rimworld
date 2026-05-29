using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class XMTIdeologyBuildOption
    {
        public ThingDef thingDef;
        public ThingDef stuffDef;
        public Ideo ideo;
        public Precept_ThingStyle sourcePrecept;
        public ThingStyleDef styleDef;
        public bool directPlace;

        public string Label
        {
            get
            {
                if (ideo != null)
                {
                    return "XMT_IdeologyBuildOptionWithIdeo".Translate(thingDef.LabelCap, ideo.name);
                }

                return thingDef.LabelCap;
            }
        }
    }

    public static class XMTIdeologyConstructionUtility
    {
        private static GameComponent_Xenomorph Tracker
        {
            get
            {
                return Current.Game?.GetComponent<GameComponent_Xenomorph>();
            }
        }

        public static bool IdeologyConstructionEnabled()
        {
            return ModsConfig.IdeologyActive &&
                   Find.IdeoManager != null &&
                   !Find.IdeoManager.classicMode &&
                   XenoSocialDefOf.XMT_Starbeast_Construction != null &&
                   XenoSocialDefOf.XMT_Starbeast_Construction.IsFinished;
        }

        public static bool CanUseResinAsStuff(ThingDef thingDef)
        {
            return StuffDefFor(thingDef) != null;
        }

        public static ThingDef StuffDefFor(ThingDef thingDef)
        {
            if (thingDef == null || !thingDef.MadeFromStuff)
            {
                return null;
            }

            if (InternalDefOf.Starbeast_Resin?.stuffProps != null &&
                InternalDefOf.Starbeast_Resin.stuffProps.CanMake(thingDef))
            {
                return InternalDefOf.Starbeast_Resin;
            }

            if (InternalDefOf.Starbeast_Fabric?.stuffProps != null &&
                InternalDefOf.Starbeast_Fabric.stuffProps.CanMake(thingDef))
            {
                return InternalDefOf.Starbeast_Fabric;
            }

            return null;
        }

        public static IEnumerable<XMTIdeologyBuildOption> BuildOptions()
        {
            if (!IdeologyConstructionEnabled())
            {
                yield break;
            }

            HashSet<string> yielded = new HashSet<string>();
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
            {
                if (!XMTUtility.IsXenomorph(pawn) || !pawn.ageTracker.Adult || pawn.Ideo == null)
                {
                    continue;
                }

                foreach (Precept precept in pawn.Ideo.PreceptsListForReading)
                {
                    ThingDef thingDef = IdeologyBuildingFromPrecept(precept);
                    if (!CanOfferBuilding(thingDef))
                    {
                        continue;
                    }

                    ThingStyleDef styleDef = pawn.Ideo.GetStyleFor(thingDef);
                    string key = pawn.Ideo.id + "|" + thingDef.defName + "|" + (styleDef?.defName ?? "none");
                    if (!yielded.Add(key))
                    {
                        continue;
                    }

                    yield return new XMTIdeologyBuildOption
                    {
                        thingDef = thingDef,
                        stuffDef = StuffDefFor(thingDef),
                        ideo = pawn.Ideo,
                        sourcePrecept = precept as Precept_ThingStyle,
                        styleDef = styleDef,
                        directPlace = DirectPlaceOnly(thingDef)
                    };
                }
            }
        }

        public static void RegisterResinBuild(Thing thing)
        {
            Tracker?.RegisterIdeologyResinBuild(thing);
        }

        public static void UnregisterResinBuild(Thing thing)
        {
            Tracker?.UnregisterIdeologyResinBuild(thing);
        }

        public static bool IsRegisteredResinBuild(Thing thing)
        {
            return Tracker != null && Tracker.IsIdeologyResinBuild(thing);
        }

        public static List<ThingDefCountClass> RequiredMaterialCost(ThingDef buildDef)
        {
            return XMT_ResinConstructionSubstitutionDef.RequiredMaterialCost(buildDef);
        }

        public static ThingDef ArtFrameDefFor(ThingDef artDef)
        {
            if (artDef == null)
            {
                return XenoWorkDefOf.XMT_IdeologyResinArtFrameSmall;
            }

            float drawSize = Mathf.Max(artDef.graphicData?.drawSize.x ?? 1f, artDef.graphicData?.drawSize.y ?? 1f);
            if (artDef.defName.Contains("Grand") || artDef.size.x >= 2 || artDef.size.z >= 2 || drawSize >= 4f)
            {
                return XenoWorkDefOf.XMT_IdeologyResinArtFrameGrand;
            }

            if (artDef.defName.Contains("Large") || drawSize >= 3f)
            {
                return XenoWorkDefOf.XMT_IdeologyResinArtFrameLarge;
            }

            return XenoWorkDefOf.XMT_IdeologyResinArtFrameSmall;
        }

        private static ThingDef IdeologyBuildingFromPrecept(Precept precept)
        {
            if (precept is Precept_Building buildingPrecept)
            {
                return buildingPrecept.ThingDef;
            }

            if (precept is Precept_RitualSeat ritualSeat)
            {
                return ritualSeat.ThingDef;
            }

            PropertyInfo thingDefProperty = precept.GetType().GetProperty("ThingDef", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (thingDefProperty != null && typeof(ThingDef).IsAssignableFrom(thingDefProperty.PropertyType))
            {
                return thingDefProperty.GetValue(precept) as ThingDef;
            }

            return null;
        }

        private static bool CanOfferBuilding(ThingDef thingDef)
        {
            if (thingDef == null || thingDef.category != ThingCategory.Building)
            {
                return false;
            }

            if (!DirectPlaceOnly(thingDef) && (thingDef.blueprintDef == null || thingDef.frameDef == null))
            {
                return false;
            }

            if (thingDef.MadeFromStuff && !CanUseResinAsStuff(thingDef))
            {
                return false;
            }

            return true;
        }

        private static bool DirectPlaceOnly(ThingDef thingDef)
        {
            return thingDef?.Minifiable == true && (thingDef.blueprintDef == null || thingDef.frameDef == null);
        }
    }
}
