using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal abstract class Designator_XMTZoneAdd : Designator_ZoneAdd
    {
        private readonly string zoneLabelKey;

        protected Designator_XMTZoneAdd(
            Type zoneType,
            string labelKey,
            string descriptionKey,
            string iconPath)
        {
            zoneTypeToPlace = zoneType;
            zoneLabelKey = labelKey;
            defaultLabel = labelKey.Translate();
            defaultDesc = descriptionKey.Translate();
            icon = ContentFinder<Texture2D>.Get(iconPath, false);
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragZone_Changed;
            soundSucceeded = SoundDefOf.Designate_ZoneAdd;
            useMouseIcon = true;
        }

        public override bool Disabled => !XMTUtility.QueenIsPlayer();

        public override bool Visible => true;

        protected override string NewZoneLabel => zoneLabelKey.Translate();

        public override AcceptanceReport CanDesignateCell(IntVec3 cell)
        {
            if (!XMTUtility.QueenIsPlayer())
            {
                return "XMT_ZoneNeedsPlayerQueen".Translate();
            }

            AcceptanceReport baseReport = base.CanDesignateCell(cell);
            if (!baseReport.Accepted)
            {
                return baseReport;
            }

            if (XMTZoneUtility.IsDoorwayCell(cell, Map))
            {
                return "XMT_ZoneCannotIncludeDoorway".Translate();
            }

            if (!XMTZoneUtility.IsEnclosedRoomAt(cell, Map))
            {
                return "XMT_ZoneMustBeEnclosed".Translate();
            }

            return true;
        }
    }

    internal class Designator_ZoneAddHostPlacement : Designator_XMTZoneAdd
    {
        public Designator_ZoneAddHostPlacement()
            : base(
                typeof(Zone_HostPlacement),
                "XMT_HostPlacementZone",
                "XMT_HostPlacementZoneDescription",
                "UI/Designators/Zone_HostPlacement")
        {
        }

        protected override Zone MakeNewZone()
        {
            return new Zone_HostPlacement(Map.zoneManager);
        }
    }

    internal class Designator_ZoneAddHostPlacement_Expand : Designator_ZoneAddHostPlacement
    {
        public Designator_ZoneAddHostPlacement_Expand()
        {
            defaultLabel = "DesignatorZoneExpand".Translate();
            hotKey = KeyBindingDefOf.Misc6;
        }
    }

    internal class Designator_ZoneAddOvomorphStorage : Designator_XMTZoneAdd
    {
        public Designator_ZoneAddOvomorphStorage()
            : base(
                typeof(Zone_OvomorphStorage),
                "XMT_OvomorphStorageZone",
                "XMT_OvomorphStorageZoneDescription",
                "UI/Designators/Zone_OvomorphStorage")
        {
        }

        protected override Zone MakeNewZone()
        {
            return new Zone_OvomorphStorage(Map.zoneManager);
        }
    }

    internal class Designator_ZoneAddOvomorphStorage_Expand : Designator_ZoneAddOvomorphStorage
    {
        public Designator_ZoneAddOvomorphStorage_Expand()
        {
            defaultLabel = "DesignatorZoneExpand".Translate();
            hotKey = KeyBindingDefOf.Misc6;
        }
    }
}
