using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class HiveStructureSite : Building
    {
        public HiveStructureUnlockDef unlock;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref unlock, "structureUnlock");
        }

        public static AcceptanceReport CanPlace(HiveStructureUnlockDef def, IntVec3 cell, Rot4 rotation, Map map, Thing ignore = null)
        {
            if (def?.building == null || map == null || !HiveStructureUtility.Unlocked(def))
                return false;
            CellRect footprint = GenAdj.OccupiedRect(cell, rotation, def.building.size);
            if (map.listerThings.ThingsOfDef(XenoBuildingDefOf.XMT_HiveStructureSite).OfType<HiveStructureSite>()
                .Any(s => s != ignore && s.unlock?.building != null
                    && footprint.Overlaps(GenAdj.OccupiedRect(s.Position, s.Rotation, s.unlock.building.size))))
                return "XMT_StructureSiteOccupied".Translate();
            return GenConstruct.CanPlaceBlueprintAt(def.building, cell, rotation, map, false, ignore, null, null);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (unlock?.building != null)
                GhostDrawer.DrawGhostThing(Position, Rotation, unlock.building, null, new Color(0.5f, 0.9f, 0.7f, 0.5f), AltitudeLayer.Blueprint);
        }

        public bool TryTransform(Pawn actor)
        {
            if (!Spawned || !CanPlace(unlock, Position, Rotation, Map, this).Accepted)
                return false;
            IntVec3 cell = Position;
            Map map = Map;
            Rot4 rotation = Rotation;
            DeSpawn();
            if (XMTUtility.TransformPawnIntoThing(actor, unlock.building, out _, actor, cell, rotation))
            {
                Destroy();
                return true;
            }

            GenSpawn.Spawn(this, cell, map, rotation);
            return false;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            yield return new Command_Action
            {
                defaultLabel = "CommandCancel".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                action = () => Destroy()
            };
        }
    }
}
