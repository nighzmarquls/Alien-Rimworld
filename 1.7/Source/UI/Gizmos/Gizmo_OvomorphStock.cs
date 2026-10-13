using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    internal class Gizmo_OvomorphStock : Gizmo
    {
        private const float Width = 180f;
        private const float MaxPipSize = 20f;
        private const float PipGapFactor = 0.25f;
        private static readonly Color EmptyColor = new Color(0.03f, 0.24f, 0.25f);
        private static readonly Color FilledColor = new Color(0.72f, 1f, 0.12f);
        private static readonly Texture2D PipTexture = ContentFinder<Texture2D>.Get("UI/Abilities/Ovomorph");
        private readonly CompOvomorphLayer comp;

        public Gizmo_OvomorphStock(CompOvomorphLayer comp)
        {
            this.comp = comp;
            Order = -95f;
        }

        public override float GetWidth(float maxWidth)
        {
            return Width;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, Width, Gizmo.Height);
            Widgets.DrawWindowBackground(rect);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), "XMT_OvomorphStock".Translate());
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), comp.StoredEggs + " / " + comp.EggCapacity);
            Text.Anchor = TextAnchor.UpperLeft;

            DrawPips(new Rect(rect.x + 8f, rect.y + 30f, rect.width - 16f, rect.height - 36f));
            Widgets.DrawHighlightIfMouseover(rect);
            TooltipHandler.TipRegion(rect, new TipSignal(comp.StockTooltip, comp.parent.thingIDNumber ^ 0x584D540));
            return new GizmoResult(GizmoState.Clear);
        }

        private void DrawPips(Rect rect)
        {
            int capacity = Mathf.Max(1, comp.EggCapacity);
            int columns = 1;
            float pipSize = 0f;
            for (int candidateColumns = 1; candidateColumns <= capacity; candidateColumns++)
            {
                int candidateRows = Mathf.CeilToInt(capacity / (float)candidateColumns);
                float widthUnits = candidateColumns + (candidateColumns - 1) * PipGapFactor;
                float heightUnits = candidateRows + (candidateRows - 1) * PipGapFactor;
                float candidateSize = Mathf.Min(MaxPipSize, rect.width / widthUnits, rect.height / heightUnits);
                if (candidateSize > pipSize + 0.01f || (Mathf.Abs(candidateSize - pipSize) <= 0.01f && candidateColumns > columns))
                {
                    columns = candidateColumns;
                    pipSize = candidateSize;
                }
            }
            float pipGap = pipSize * PipGapFactor;
            float startX = rect.x;
            float startY = rect.y;

            for (int i = 0; i < capacity; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect pip = new Rect(startX + column * (pipSize + pipGap), startY + row * (pipSize + pipGap), pipSize, pipSize);
                Color color = i < comp.StoredEggs
                    ? FilledColor
                    : i == comp.StoredEggs ? Color.Lerp(EmptyColor, FilledColor, comp.EggProductionProgress) : EmptyColor;
                Color previousColor = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(pip, PipTexture, ScaleMode.ScaleToFit, true);
                GUI.color = previousColor;
            }
        }
    }
}
