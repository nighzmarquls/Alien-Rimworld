using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class AcidPitSpray
    {
        public static List<IntVec3> AffectedCells(IntVec3 origin, IntVec3 target, Map map, float range, float angle)
        {
            var cells = new List<IntVec3>();
            if (map == null || !origin.InBounds(map) || !target.IsValid || target == origin)
                return cells;
            // As with FireSpew/miasma, geometry and preview share a bounded radial cone and native sight checks.
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, range, false))
                if (cell.InBounds(map) && ContainsOffset(target - origin, cell - origin, range, angle) && cell.CanBeSeenOverFast(map) && GenSight.LineOfSight(origin, cell, map, true))
                    cells.Add(cell);
            return cells;
        }

        public static bool ContainsOffset(IntVec3 forward, IntVec3 offset, float range, float angle) => forward != IntVec3.Zero && offset != IntVec3.Zero && offset.LengthHorizontalSquared <= range * range && Vector3.Angle(forward.ToVector3(), offset.ToVector3()) <= angle * 0.5f + 0.01f;
    }
}
