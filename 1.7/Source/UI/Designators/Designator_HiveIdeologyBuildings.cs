using RimWorld;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class Designator_HiveIdeologyBuildings : Designator_Dropdown
    {
        private static readonly FieldInfo floatMenuInitialPositionShiftField = AccessTools.Field(typeof(FloatMenu), "InitialPositionShift");

        public Designator_HiveIdeologyBuildings()
        {
            defaultLabel = "XMT_CommandHiveIdeologyBuildings".Translate();
            defaultDesc = "XMT_CommandHiveIdeologyBuildingsDescription".Translate();
            icon = ContentFinder<Texture2D>.Get("Things/Building/Linked/HiveMass_MenuIcon");
            soundSucceeded = SoundDefOf.Designate_DragStandard_Changed;
            Add(new Designator_HiveIdeologyPlaceholder());
        }

        public override bool Visible
        {
            get
            {
                RefreshElements();
                return XMTIdeologyConstructionUtility.IdeologyConstructionEnabled() &&
                       Elements.Any(e => e is Designator_BuildHiveIdeologyBuilding);
            }
        }

        public override bool Disabled
        {
            get
            {
                return !Visible;
            }
        }

        public override void ProcessInput(Event ev)
        {
            RefreshElements();
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (Designator designator in Elements)
            {
                if (designator is Designator_BuildHiveIdeologyBuilding buildDesignator)
                {
                    options.Add(new FloatMenuOption(buildDesignator.MenuLabel, delegate
                    {
                        Find.DesignatorManager.Select(buildDesignator);
                    }));
                }
            }

            if (options.Any())
            {
                FloatMenu menu = new FloatMenu(options);
                floatMenuInitialPositionShiftField?.SetValue(menu, new Vector2(0f, -menu.InitialSize.y));
                Find.WindowStack.Add(menu);
            }
        }

        private void RefreshElements()
        {
            Elements.Clear();
            foreach (XMTIdeologyBuildOption option in XMTIdeologyConstructionUtility.BuildOptions().OrderBy(o => o.Label))
            {
                Add(new Designator_BuildHiveIdeologyBuilding(option));
            }

            if (!Elements.Any())
            {
                Add(new Designator_HiveIdeologyPlaceholder());
            }

            SetActiveDesignator(Elements[0], false);
        }
    }

    public class Designator_HiveIdeologyPlaceholder : Designator
    {
        public Designator_HiveIdeologyPlaceholder()
        {
            defaultLabel = "XMT_CommandHiveIdeologyBuildings".Translate();
            defaultDesc = "XMT_CommandHiveIdeologyBuildingsDescription".Translate();
            icon = ContentFinder<Texture2D>.Get("Things/Building/Linked/HiveMass_MenuIcon");
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            return false;
        }
    }

    public class Designator_BuildHiveIdeologyBuilding : Designator_Build
    {
        private readonly XMTIdeologyBuildOption option;

        public string MenuLabel => option.Label;

        public Designator_BuildHiveIdeologyBuilding(XMTIdeologyBuildOption option) : base(option.thingDef)
        {
            this.option = option;
            defaultLabel = option.Label;
            defaultDesc = option.thingDef.description;
            icon = option.thingDef.uiIcon;
            iconDrawScale = option.thingDef.uiIconScale;
            sourcePrecept = option.sourcePrecept as Precept_Building;
            styleDef = option.styleDef;
            styleOverridden = option.styleDef != null;

            if (option.stuffDef != null)
            {
                SetStuffDef(option.stuffDef);
            }
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (option.directPlace)
            {
                return GenConstruct.CanPlaceBlueprintAt(option.thingDef, c, placingRot, Map, false, null, null, option.stuffDef);
            }

            return GenConstruct.CanPlaceBlueprintAt(option.thingDef, c, placingRot, Map, false, null, null, option.stuffDef);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (option.directPlace)
            {
                IdeologyResinArtFrame frame = ThingMaker.MakeThing(XMTIdeologyConstructionUtility.ArtFrameDefFor(option.thingDef)) as IdeologyResinArtFrame;
                frame.TargetThingDef = option.thingDef;
                frame.StuffDef = option.stuffDef;
                frame.TargetStyleDef = option.styleDef;
                frame.StyleDef = option.styleDef;
                frame.StyleSourcePrecept = option.sourcePrecept;
                frame.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(frame, c, Map, placingRot, WipeMode.VanishOrMoveAside);
                Map.designationManager.AddDesignation(new Designation(frame, XenoWorkDefOf.XMT_CorpseArt));
                return;
            }

            Blueprint_Build blueprint = GenConstruct.PlaceBlueprintForBuild(
                option.thingDef,
                c,
                Map,
                placingRot,
                Faction.OfPlayer,
                option.stuffDef,
                option.sourcePrecept,
                option.styleDef,
                false);

            XMTIdeologyConstructionUtility.RegisterResinBuild(blueprint);
        }
    }
}
