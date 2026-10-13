using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class Zone_HostPlacement : Zone
    {
        private const float ZoneOpacity = 0.09f;

        public Zone_HostPlacement()
        {
        }

        public Zone_HostPlacement(ZoneManager zoneManager)
            : base("XMT_HostPlacementZone".Translate(), zoneManager)
        {
        }

        public override bool IsMultiselectable => true;

        protected override Color NextZoneColor => new Color(0.12f, 0.22f, 0.38f, ZoneOpacity);

        public override void AddCell(IntVec3 cell)
        {
            base.AddCell(cell);
            XMTZoneRoomAssignment.NotifyAddedCell(this, cell);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            yield return new Command_Hide_XMTZone<Zone_HostPlacement>(
                this,
                "XMT_HostPlacementZoneGroup",
                "UI/Designators/Zone_HostPlacement");

            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
        }

        public override IEnumerable<Gizmo> GetZoneAddGizmos()
        {
            yield return DesignatorUtility.FindAllowedDesignator<Designator_ZoneAddHostPlacement_Expand>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                color.a = ZoneOpacity;
            }
        }
    }

    public class Zone_OvomorphStorage : Zone
    {
        private const float ZoneOpacity = 0.09f;

        public Zone_OvomorphStorage()
        {
        }

        public Zone_OvomorphStorage(ZoneManager zoneManager)
            : base("XMT_OvomorphStorageZone".Translate(), zoneManager)
        {
        }

        public override bool IsMultiselectable => true;

        protected override Color NextZoneColor => new Color(0.28f, 0.43f, 0.42f, ZoneOpacity);

        public override void AddCell(IntVec3 cell)
        {
            base.AddCell(cell);
            XMTZoneRoomAssignment.NotifyAddedCell(this, cell);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            yield return new Command_Hide_XMTZone<Zone_OvomorphStorage>(
                this,
                "XMT_OvomorphStorageZoneGroup",
                "UI/Designators/Zone_OvomorphStorage");

            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
        }

        public override IEnumerable<Gizmo> GetZoneAddGizmos()
        {
            yield return DesignatorUtility.FindAllowedDesignator<Designator_ZoneAddOvomorphStorage_Expand>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                color.a = ZoneOpacity;
            }
        }
    }

    internal class Command_Hide_XMTZone<TZone> : Command_Hide
        where TZone : Zone
    {
        private readonly string groupLabelKey;
        private readonly Texture2D groupIcon;

        internal Command_Hide_XMTZone(IHideable hideable, string groupLabelKey, string groupIconPath)
            : base(hideable)
        {
            this.groupLabelKey = groupLabelKey;
            groupIcon = ContentFinder<Texture2D>.Get(groupIconPath, false);
        }

        protected override IEnumerable<FloatMenuOption> GetOptions()
        {
            yield return new FloatMenuOption("ShowAllZones".Translate(), () => ToggleAll(false));
            yield return new FloatMenuOption("HideAllZones".Translate(), () => ToggleAll(true));

            foreach (FloatMenuOption option in ZoneTypeOptions<TZone>(groupLabelKey.Translate(), groupIcon))
            {
                yield return option;
            }
        }
    }

    internal static class XMTZoneRoomAssignment
    {
        internal static void NotifyAddedCell(Zone zone, IntVec3 cell)
        {
            NotifyRoomAt(zone?.Map, cell);
        }

        internal static void NotifyRoomAt(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return;
            }

            Room room = cell.GetRoom(map);
            if (room != null && room.ProperRoom && !room.TouchesMapEdge)
            {
                XMTHiveUtility.NotifyZonedHiveRoom(room, cell);
            }
        }
    }
}
