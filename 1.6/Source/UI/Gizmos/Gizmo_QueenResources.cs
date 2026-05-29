using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class Gizmo_QueenResources : Gizmo
    {
        private const float Width = 180f;
        private const float RowHeight = 28f;
        private readonly CompQueenAssimilation comp;

        public Gizmo_QueenResources(CompQueenAssimilation comp)
        {
            this.comp = comp;
            Order = -90f;
        }

        public override float GetWidth(float maxWidth)
        {
            return Width;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            List<QueenIngestibleResourceDef> resources = VisibleResources();
            Rect rect = new Rect(topLeft.x, topLeft.y, Width, 36f + resources.Count * RowHeight);
            Widgets.DrawWindowBackground(rect);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 24f), "XMT_QueenResources".Translate());

            float curY = rect.y + 32f;
            foreach (QueenIngestibleResourceDef resource in resources)
            {
                DrawResourceRow(new Rect(rect.x + 8f, curY, rect.width - 16f, RowHeight - 4f), resource);
                curY += RowHeight;
            }

            return new GizmoResult(GizmoState.Clear);
        }

        private void DrawResourceRow(Rect rect, QueenIngestibleResourceDef resource)
        {
            float amount = comp.GetResourceAmount(resource);
            float capacity = comp.GetResourceCapacity(resource);
            Rect barRect = new Rect(rect.x, rect.y + 14f, rect.width, 8f);
            Widgets.DrawBoxSolid(barRect, Color.black);
            Rect filledRect = barRect.ContractedBy(1f);
            filledRect.width *= capacity <= 0f ? 0f : Mathf.Clamp01(amount / capacity);
            Widgets.DrawBoxSolid(filledRect, resource.barColor);
            Widgets.DrawHighlightIfMouseover(rect);
            Widgets.Label(new Rect(rect.x, rect.y - 2f, rect.width * 0.58f, 20f), resource.LabelCap);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(rect.x + rect.width * 0.42f, rect.y - 2f, rect.width * 0.58f, 20f), Mathf.FloorToInt(amount) + " / " + Mathf.FloorToInt(capacity));
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(rect, resource.description);
        }

        private List<QueenIngestibleResourceDef> VisibleResources()
        {
            List<QueenIngestibleResourceDef> resources = new List<QueenIngestibleResourceDef>();
            foreach (QueenIngestibleResourceDef resource in DefDatabase<QueenIngestibleResourceDef>.AllDefsListForReading)
            {
                if (comp.ResourceUnlocked(resource) || comp.GetResourceAmount(resource) > 0f || resource.showWhenLocked)
                {
                    resources.Add(resource);
                }
            }

            return resources;
        }
    }
}
