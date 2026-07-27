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
}
