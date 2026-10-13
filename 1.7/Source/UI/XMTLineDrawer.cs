using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal static class XMTLineDrawer
    {
        public static void DrawPolyline(IList<Vector2> points, Color color, float width)
        {
            if (Event.current.type != EventType.Repaint || points == null || points.Count < 2 || width <= 0f)
            {
                return;
            }

            // Keep dependency lines opaque; RimWorld owns antialiasing and GUI clipping.
            color.a = 1f;
            for (int i = 1; i < points.Count; i++)
            {
                Widgets.DrawLine(points[i - 1], points[i], color, width);
            }
        }
    }
}
