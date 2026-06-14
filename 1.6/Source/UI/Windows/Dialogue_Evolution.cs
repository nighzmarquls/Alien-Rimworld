using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Xenomorphtype
{
    internal class Dialogue_Evolution : Window
    {
        const float cardWidth = 98f;
        const float cardHeight = 40f;
        const float cardPadding = 4f;
        const float graphHorizontalSpacing = cardPadding * 2f;
        const float graphVerticalSpacing = cardPadding * 12f;
        const float graphBottomPadding = cardPadding * 2f;
        const float leftPanelWidth = 285f;
        const float panelGap = 8f;
        const float dependencyCornerRadius = 6f;
        const int dependencyCornerMinimumSegments = 2;
        const int dependencyCornerMaximumSegments = 4;
        const float dependencyCornerPixelsPerSegment = 5f;
        const float dependencyCornerMinimumTurnAngle = 3f;
        const float dependencyCornerMinimumSegmentLength = 1f;
        const float dependencyPointComparisonTolerance = 0.01f;
        const float dependencyLineHaloWidthMultiplier = 2.4f;
        const float dependencyLineHaloAlphaMultiplier = 0.28f;

        private static readonly Color mutedDependencyLineColor = new Color(0.45f, 0.55f, 0.52f, 0.42f);
        private static readonly Color selectedDependentLineColor = new Color(0.12f, 0.82f, 0.72f, 0.78f);
        private static readonly Color selectedPrerequisiteLineColor = new Color(0.45f, 1f, 0.28f, 1f);
        private static readonly Color selectedMissingPrerequisiteLineColor = new Color(0.72f, 1f, 0.22f, 1f);
        private static readonly Color purchasedEvolutionColor = new Color(0.72f, 0.86f, 0.22f, 1f);
        private static readonly Color replacedEvolutionColor = new Color(0.42f, 0.55f, 0.34f, 1f);

        private Pawn queen;

        private CompQueen comp;

        private TaggedString text;

        private float scrollHeight;

        private float scrollWidth;

        private Vector2 scrollPosition;

        private EvolutionGraphLayout evolutionLayout;

        private readonly EvolutionGraphEdgeRouter edgeRouter = new EvolutionGraphEdgeRouter();
        private readonly XMTLineTextureCache dependencyLineTextureCache = new XMTLineTextureCache();

        public static RoyalEvolutionDef selectedEvolution;

        List<RoyalEvolutionDef> originalEvolutions;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(UI.screenWidth, 1250), UI.screenHeight - 4);

        public Dialogue_Evolution(TaggedString text, Pawn pawn, CompQueen compQueen)
        {
            this.text = text;
            forcePause = true;
            queen = pawn;
            comp = compQueen;
            absorbInputAroundWindow = true;
            originalEvolutions = compQueen.ChosenEvolutions.ToArray().ToList();
            evolutionLayout = new EvolutionGraphLayout(cardWidth, cardHeight, graphHorizontalSpacing, graphVerticalSpacing);
        }

        public override void PostClose()
        {
            base.PostClose();
            selectedEvolution = null;
            dependencyLineTextureCache.Dispose();
        }

        private void DrawInfo(Rect rect)
        {
            float num = 0f;
            Rect infoRect = new Rect(rect);
            Widgets.BeginGroup(infoRect);
            if (selectedEvolution != null)
            {
                Text.Font = GameFont.Medium;
                Rect titleRect = new Rect(0f, num, infoRect.width, 0f);
                Widgets.LabelCacheHeight(ref titleRect, selectedEvolution.LabelCap);
                Text.Font = GameFont.Small;
                num += titleRect.height;
                if (!selectedEvolution.description.NullOrEmpty())
                {
                    Rect descriptionRect = new Rect(0f, num, infoRect.width, 0f);
                    Widgets.LabelCacheHeight(ref descriptionRect, selectedEvolution.description);
                    num += descriptionRect.height + 16f;
                }

                if(selectedEvolution.evoPointCost > 0 && !comp.ChosenEvolutions.Contains(selectedEvolution))
                {
                    Rect priceRect = new Rect(0f, num, infoRect.width, 0f);
                    TaggedString advancementCost = "Advancement Cost: " + selectedEvolution.evoPointCost;

                    if (comp.AvailableEvoPoints < selectedEvolution.evoPointCost)
                    {
                        advancementCost = advancementCost.Colorize(ColorLibrary.RedReadable);
                    }
                    
                    Widgets.LabelCacheHeight(ref priceRect, advancementCost.Resolve());
                    num += priceRect.height + 16f;
                }

                Rect requirementRect = new Rect(0f, num, infoRect.width, 0f);
                TaggedString requirementText = "";

                if (selectedEvolution.prerequisites != null)
                {
                    requirementText = requirementText + "\n" + "XMT_EvolutionRequires".Translate();
                    foreach (RoyalEvolutionDef preref in selectedEvolution.prerequisites)
                    {
                        requirementText = requirementText + preref.label.Colorize(comp.HasActiveEvolution(preref) ? Color.white : ColorLibrary.RedReadable) + " ";

                    }
                    Widgets.LabelCacheHeight(ref requirementRect, requirementText.Resolve());
                    num += requirementRect.height + 4f;
                }

                Rect incompatibleRect = new Rect(0f, num, infoRect.width, 0f);
                string incompatibleText = "";
                if (selectedEvolution.incompatible != null && selectedEvolution.incompatible.Count > 0)
                {
                    incompatibleText = incompatibleText + "\n" + "XMT_EvolutionIncompatible".Translate();
                    foreach (RoyalEvolutionDef incompatible in selectedEvolution.incompatible)
                    {
                        incompatibleText = incompatibleText + incompatible.label.Colorize(comp.HasActiveEvolution(incompatible) ? ColorLibrary.RedReadable : Color.white) + " ";
                    }
                }

                if (comp.TryGetIncompatibleEvolution(selectedEvolution, out RoyalEvolutionDef blocker))
                {
                    incompatibleText = incompatibleText + "\n" + "XMT_EvolutionBlockedByIncompatible".Translate(blocker.LabelCap).Colorize(ColorLibrary.RedReadable);
                }

                if (!incompatibleText.NullOrEmpty())
                {
                    Widgets.LabelCacheHeight(ref incompatibleRect, incompatibleText);
                    num += incompatibleRect.height + 4f;
                }

                if (!comp.ChosenEvolutions.Contains(selectedEvolution) && selectedEvolution.AvailableForPawn(queen) && EvolutionUnlocked(selectedEvolution,comp) && !comp.TryGetIncompatibleEvolution(selectedEvolution, out RoyalEvolutionDef _))
                {
                    if (Widgets.ButtonText(new Rect(rect.x, num, Window.CloseButSize.x, Window.CloseButSize.y), "XMT_EvolutionAdd".Translate()))
                    {
                        BuyEvolution(selectedEvolution);
                    }
                }

                if (comp.ChosenEvolutions.Contains(selectedEvolution))
                {
                    if (comp.HasDependencies(selectedEvolution, out RoyalEvolutionDef[] dependencies))
                    {
                        TaggedString dependencyText = "XMT_EvolutionPrerequisiteFor".Translate();
                        foreach(RoyalEvolutionDef dep in dependencies)
                        {
                            dependencyText += dep.label.Colorize(Color.red) + "\n ";
                        }

                        Widgets.LabelCacheHeight(ref requirementRect, dependencyText.Resolve());
                    }
                    else
                    {
                        if (Widgets.ButtonText(new Rect(rect.x, num, Window.CloseButSize.x, Window.CloseButSize.y), "XMT_EvolutionRemove".Translate()))
                        {
                            RemoveEvolution(selectedEvolution);
                        }
                    }
                }
            }

            Widgets.EndGroup();
        }

        private void BuyEvolution(RoyalEvolutionDef evolution)
        {
            comp.AddEvolution(evolution);
        }

        private void RemoveEvolution(RoyalEvolutionDef evolution)
        {
            comp.RemoveEvolution(evolution);
        }

        private void DrawPawn(Rect rect)
        {
            Widgets.BeginGroup(rect);
            Rect position = new Rect(0f, 0f, rect.width, rect.height).ContractedBy(4f);
            RenderTexture image = PortraitsCache.Get(queen, new Vector2(position.width, position.height), Rot4.East, cameraZoom : 0.5f);
            GUI.DrawTexture(position, image);

            Widgets.EndGroup();
        }

        private bool EvolutionUnlocked(RoyalEvolutionDef def, CompQueen compqueen)
        {

            if (def.prerequisites == null || def.prerequisites.Count == 0)
            {
                return true;
            }

            foreach(RoyalEvolutionDef prereq in  def.prerequisites) {
                if (!compqueen.HasActiveEvolution(prereq))
                {
                    return false;
                }
            }

            return true;
        }

        private void DrawLines(Rect rect)
        {
            List<EvolutionGraphEdgeRoute> routes = edgeRouter.BuildRoutes(evolutionLayout.Nodes);

            if (selectedEvolution != null)
            {
                foreach (EvolutionGraphEdgeRoute route in routes)
                {
                    if (route.Child.Def != selectedEvolution && route.Parent.Def != selectedEvolution)
                    {
                        DrawDependencyLine(route, mutedDependencyLineColor, 1.5f);
                    }
                }

                foreach (EvolutionGraphEdgeRoute route in routes)
                {
                    if (route.Parent.Def == selectedEvolution && route.Child.Def != selectedEvolution)
                    {
                        DrawDependencyLine(route, selectedDependentLineColor, 2.5f);
                    }
                }

                foreach (EvolutionGraphEdgeRoute route in routes)
                {
                    if (route.Child.Def == selectedEvolution)
                    {
                        Color color = comp.HasActiveEvolution(route.Parent.Def) ? selectedPrerequisiteLineColor : selectedMissingPrerequisiteLineColor;
                        DrawDependencyLine(route, color, 4f);
                    }
                }

                return;
            }

            foreach (EvolutionGraphEdgeRoute route in routes)
            {
                DrawDependencyLine(route, TexUI.DefaultLineResearchColor, 2f);
            }
        }

        private void DrawDependencyLine(EvolutionGraphEdgeRoute route, Color color, float width)
        {
            if (route.Points.Count < 3 || dependencyCornerRadius <= 0f || dependencyCornerMaximumSegments <= 0)
            {
                DrawDependencyPolyline(route, route.Points, color, width);
                return;
            }

            List<Vector2> points = new List<Vector2>();
            points.Add(route.Points[0]);

            for (int i = 1; i < route.Points.Count - 1; i++)
            {
                Vector2 previous = route.Points[i - 1];
                Vector2 corner = route.Points[i];
                Vector2 next = route.Points[i + 1];

                if (!TryGetRoundedCorner(previous, corner, next, out Vector2 cornerStart, out Vector2 cornerEnd, out int cornerSegments))
                {
                    AddDependencyPoint(points, corner);
                    continue;
                }

                AddDependencyPoint(points, cornerStart);
                for (int segment = 1; segment < cornerSegments; segment++)
                {
                    float progress = (float)segment / cornerSegments;
                    AddDependencyPoint(points, GetRoundedCornerPoint(cornerStart, corner, cornerEnd, progress));
                }
                AddDependencyPoint(points, cornerEnd);
            }

            AddDependencyPoint(points, route.Points[route.Points.Count - 1]);
            DrawDependencyPolyline(route, points, color, width);
        }

        private void DrawDependencyPolyline(EvolutionGraphEdgeRoute route, List<Vector2> points, Color color, float width)
        {
            dependencyLineTextureCache.DrawPolyline(GetDependencyLineCacheKey(route), points, color, width, dependencyLineHaloWidthMultiplier, dependencyLineHaloAlphaMultiplier);
        }

        private int GetDependencyLineCacheKey(EvolutionGraphEdgeRoute route)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (route.Parent.Def != null ? route.Parent.Def.shortHash : 0);
                hash = hash * 31 + (route.Child.Def != null ? route.Child.Def.shortHash : 0);
                return hash;
            }
        }

        private bool TryGetRoundedCorner(Vector2 previous, Vector2 corner, Vector2 next, out Vector2 cornerStart, out Vector2 cornerEnd, out int cornerSegments)
        {
            Vector2 incoming = previous - corner;
            Vector2 outgoing = next - corner;
            float incomingLength = incoming.magnitude;
            float outgoingLength = outgoing.magnitude;

            if (incomingLength <= dependencyCornerMinimumSegmentLength || outgoingLength <= dependencyCornerMinimumSegmentLength)
            {
                cornerStart = corner;
                cornerEnd = corner;
                cornerSegments = 0;
                return false;
            }

            float turnAngle = 180f - Vector2.Angle(incoming, outgoing);
            if (turnAngle <= dependencyCornerMinimumTurnAngle)
            {
                cornerStart = corner;
                cornerEnd = corner;
                cornerSegments = 0;
                return false;
            }

            float radius = Mathf.Min(dependencyCornerRadius, incomingLength / 2f, outgoingLength / 2f);
            if (radius <= dependencyCornerMinimumSegmentLength)
            {
                cornerStart = corner;
                cornerEnd = corner;
                cornerSegments = 0;
                return false;
            }

            cornerStart = corner + incoming.normalized * radius;
            cornerEnd = corner + outgoing.normalized * radius;
            cornerSegments = GetRoundedCornerSegmentCount(radius, turnAngle);
            return true;
        }

        private int GetRoundedCornerSegmentCount(float radius, float turnAngle)
        {
            float scaledCurveSize = radius * Mathf.Max(0.5f, turnAngle / 90f);
            int segments = Mathf.CeilToInt(scaledCurveSize / dependencyCornerPixelsPerSegment);
            return Mathf.Clamp(segments, dependencyCornerMinimumSegments, dependencyCornerMaximumSegments);
        }

        private Vector2 GetRoundedCornerPoint(Vector2 cornerStart, Vector2 corner, Vector2 cornerEnd, float progress)
        {
            Vector2 first = Vector2.Lerp(cornerStart, corner, progress);
            Vector2 second = Vector2.Lerp(corner, cornerEnd, progress);
            return Vector2.Lerp(first, second, progress);
        }

        private void AddDependencyPoint(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 || !Approximately(points[points.Count - 1], point))
            {
                points.Add(point);
            }
        }

        private bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < dependencyPointComparisonTolerance && Mathf.Abs(a.y - b.y) < dependencyPointComparisonTolerance;
        }
        private void DrawEvolutionCard(RoyalEvolutionDef def, Rect rect)
        {
            Color color = Widgets.NormalOptionColor;
            Color bgColor = EvolutionUnlocked(def, comp) ? TexUI.OldFinishedResearchColor : TexUI.AvailResearchColor;
            Color borderColor;
            bool chosen = comp.ChosenEvolutions.Contains(def);
            bool replaced = comp.IsEvolutionReplaced(def);
            bool incompatible = comp.TryGetIncompatibleEvolution(def, out RoyalEvolutionDef _);
            bool unlocked = EvolutionUnlocked(def, comp);

            if (chosen)
            {
                bgColor = replaced ? replacedEvolutionColor : purchasedEvolutionColor;
                if (!replaced)
                {
                    color = Color.black;
                }
            }

            if (selectedEvolution == def)
            {
                borderColor = TexUI.HighlightBorderResearchColor;
                bgColor += TexUI.HighlightBgResearchColor;
            }
            else
            {
                borderColor = TexUI.DefaultBorderResearchColor;
            }

            if (!chosen && (!def.AvailableForPawn(queen) || incompatible || !unlocked))
            {
                color = Color.red;
            }

            if (Widgets.CustomButtonText(ref rect, string.Empty, bgColor, color, borderColor))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                selectedEvolution = selectedEvolution == def ? null : def;
            }

            TextAnchor anchor = Text.Anchor;
            Color color2 = GUI.color;
            GUI.color = color;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, def.LabelCap);
            GUI.color = color2;
            Text.Anchor = anchor;
        }
        private float DrawEvolutions(Rect rect)
        {
            GameFont font = Text.Font;
            Text.Font = GameFont.Tiny;
            evolutionLayout.UpdateRects(rect);
            ClearSelectionOnEmptyGraphClick(rect);
            DrawLines(rect);
            foreach (EvolutionGraphNode node in evolutionLayout.Nodes)
            {
                DrawEvolutionCard(node.Def, node.Rect);
            }

            Text.Font = font;
            return evolutionLayout.Size.y + graphBottomPadding;
        }

        private void ClearSelectionOnEmptyGraphClick(Rect rect)
        {
            if (selectedEvolution == null || Event.current.type != EventType.MouseDown || Event.current.button != 0 || !rect.Contains(Event.current.mousePosition))
            {
                return;
            }

            foreach (EvolutionGraphNode node in evolutionLayout.Nodes)
            {
                if (node.Rect.Contains(Event.current.mousePosition))
                {
                    return;
                }
            }

            selectedEvolution = null;
            Event.current.Use();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect outRect = (inRect);
            outRect.yMax -= 4f + Window.CloseButSize.y;
            Text.Font = GameFont.Small;
            float curY = 0f;
            Widgets.Label(0f, ref curY, outRect.width, text.Resolve());
            curY += 18f;

            float leftWidth = Mathf.Min(leftPanelWidth, outRect.width * 0.35f);
            Rect leftPanelRect = new Rect(outRect.x, curY, leftWidth, outRect.height - curY);
            Rect graphOuterRect = new Rect(leftPanelRect.xMax + panelGap, curY, outRect.width - leftPanelRect.width - panelGap, outRect.height - curY);
            Rect graphViewRect = new Rect(0f, 0f, Mathf.Max(graphOuterRect.width - 16f, scrollWidth), scrollHeight);

            Rect pawnRect = new Rect(leftPanelRect.x, leftPanelRect.y, leftPanelRect.width, leftPanelRect.width);
            DrawPawn(pawnRect);

            Rect infoRect = new Rect(leftPanelRect.x, pawnRect.yMax, leftPanelRect.width, leftPanelRect.height - pawnRect.height);
            DrawInfo(infoRect);

            Widgets.BeginScrollView(graphOuterRect, ref scrollPosition, graphViewRect);

            Rect evoRect = new Rect(0f, 0f, graphViewRect.width, graphViewRect.height);

            float graphHeight = DrawEvolutions(evoRect);

            if (Event.current.type == EventType.Layout)
            {
                scrollHeight = Mathf.Max(graphHeight, graphOuterRect.height);
                scrollWidth = Mathf.Max(evolutionLayout.Size.x + 16f, graphOuterRect.width - 16f);
            }

            Widgets.EndScrollView();

            curY = outRect.yMax + 4f;
            Rect leftRect = new Rect(0f, curY, inRect.width, Window.CloseButSize.y);
            AcceptanceReport acceptanceReport = CanClose();
            Rect rect2 = new Rect(leftRect.xMax - Window.CloseButSize.x, leftRect.y, Window.CloseButSize.x, Window.CloseButSize.y);

            if (Widgets.ButtonText(new Rect(leftRect.x, leftRect.y, Window.CloseButSize.x, Window.CloseButSize.y), "Cancel".Translate()))
            {
                comp.ChosenEvolutions = originalEvolutions;
                Close();
            }

            DrawBottomText(Window.CloseButSize.x + cardPadding, ref curY, inRect.width);

            if (!acceptanceReport.Accepted)
            {
                TextAnchor anchor = Text.Anchor;
                GameFont font = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                Rect rect3 = leftRect;
                rect3.xMax = rect2.xMin - 4f;
                Widgets.Label(rect3, acceptanceReport.Reason.Colorize(ColoredText.WarningColor));
                Text.Font = font;
                Text.Anchor = anchor;
            }

            if (Widgets.ButtonText(rect2, "OK".Translate()))
            {
                Accept();
            }
        }

        private void Accept()
        {
            int difference = 0;

            foreach(RoyalEvolutionDef evo in comp.ChosenEvolutions)
            {
                if(originalEvolutions.Contains(evo))
                {
                    continue;
                }
                difference+= evo.evoPointCost;
            }

            foreach(RoyalEvolutionDef evo in originalEvolutions)
            {
                if (comp.ChosenEvolutions.Contains(evo))
                {
                    continue;
                }
                difference+= evo.evoPointCost;
            }

            if (difference > 0)
            {
                InterruptQueenJobForEvolution();

                Hediff geneIntegration = HediffMaker.MakeHediff(XenoGeneDefOf.XMT_GeneIntegration, queen);

                geneIntegration.Severity = (1.0f * difference) / 12;

                queen.health.AddHediff(geneIntegration);
                FilthMaker.TryMakeFilth(queen.PositionHeld, queen.MapHeld, InternalDefOf.Starbeast_Filth_Resin, count: 10*difference);
            }

            Close();
        }

        private void InterruptQueenJobForEvolution()
        {
            if (queen?.MapHeld == null || queen.jobs == null)
            {
                return;
            }

            queen.pather?.StopDead();
            FeralJobUtility.ForceClearFeralJobReservationsClaimedBy(queen.MapHeld, queen);

            if (queen.CurJob != null)
            {
                queen.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            FeralJobUtility.ForceClearFeralJobReservationsClaimedBy(queen.MapHeld, queen);
        }

        private AcceptanceReport CanClose()
        {

            return AcceptanceReport.WasAccepted;
        }

        private void DrawBottomText(float curX, ref float curY, float width)
        {
            string bottomText = "Advancements " + comp.TotalSpentEvoPoints + " of " + comp.TotalEvoPoints;

            Widgets.Label(curX, ref curY, width, bottomText);
        }
    }
}
