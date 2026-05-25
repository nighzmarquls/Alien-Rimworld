using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;
using Verse.Sound;

namespace Xenomorphtype
{
    internal class Dialogue_Evolution : Window
    {
        const float cardWidth = 98f;
        const float cardHeight = 40f;
        const float cardBuffer = 4f;
        const float leftPanelWidth = 285f;
        const float panelGap = 8f;

        private Pawn queen;

        private CompQueen comp;

        private TaggedString text;

        private float scrollHeight;

        private float scrollWidth;

        private Vector2 scrollPosition;

        private static List<TabRecord> tmpTabs = new List<TabRecord>();

        private EvolutionGraphLayout evolutionLayout;

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
            evolutionLayout = new EvolutionGraphLayout(cardWidth, cardHeight, cardBuffer * 2f, cardBuffer * 6f);
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
                    requirementText = requirementText + "\n" + "requires: ";
                    foreach (RoyalEvolutionDef preref in selectedEvolution.prerequisites)
                    {
                        requirementText = requirementText + preref.label.Colorize(EvolutionUnlocked(preref, comp) ? Color.white : ColorLibrary.RedReadable) + " ";

                    }
                    Widgets.LabelCacheHeight(ref requirementRect, requirementText.Resolve());
                    num += requirementRect.height + 4f;
                }

                if (!comp.ChosenEvolutions.Contains(selectedEvolution) && selectedEvolution.AvailableForPawn(queen) && EvolutionUnlocked(selectedEvolution,comp))
                {
                    if (Widgets.ButtonText(new Rect(rect.x, num, Window.CloseButSize.x, Window.CloseButSize.y), "Add"))//.Translate()))
                    {
                        BuyEvolution(selectedEvolution);
                    }
                }

                if (comp.ChosenEvolutions.Contains(selectedEvolution))
                {
                    if (comp.HasDependencies(selectedEvolution, out RoyalEvolutionDef[] dependencies))
                    {
                        TaggedString dependencyText = "is prerequisite for ";
                        foreach(RoyalEvolutionDef dep in dependencies)
                        {
                            dependencyText += dep.label.Colorize(Color.red) + "\n ";
                        }

                        Widgets.LabelCacheHeight(ref requirementRect, dependencyText.Resolve());
                    }
                    else
                    {
                        if (Widgets.ButtonText(new Rect(rect.x, num, Window.CloseButSize.x, Window.CloseButSize.y), "Remove"))//.Translate()))
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
                if (!compqueen.ChosenEvolutions.Contains(prereq))
                {
                    return false;
                }
            }

            return true;
        }

        private void DrawLines(Rect rect)
        {
            foreach (EvolutionGraphNode node in evolutionLayout.Nodes)
            {
                foreach (EvolutionGraphNode parent in node.Parents)
                {
                    DrawDependencyLine(parent, node, TexUI.DefaultLineResearchColor, 2f);
                }
            }

            if (selectedEvolution == null)
            {
                return;
            }

            foreach (EvolutionGraphNode node in evolutionLayout.Nodes)
            {
                foreach (EvolutionGraphNode parent in node.Parents)
                {
                    if (node.Def == selectedEvolution || parent.Def == selectedEvolution)
                    {
                        DrawDependencyLine(parent, node, TexUI.HighlightLineResearchColor, 4f);
                    }
                }
            }
        }

        private void DrawDependencyLine(EvolutionGraphNode parent, EvolutionGraphNode child, Color color, float width)
        {
            Vector2 start = new Vector2(parent.Rect.center.x, parent.Rect.yMax);
            Vector2 end = new Vector2(child.Rect.center.x, child.Rect.yMin);
            float midY = start.y + ((end.y - start.y) / 2f);

            Widgets.DrawLine(start, new Vector2(start.x, midY), color, width);
            Widgets.DrawLine(new Vector2(start.x, midY), new Vector2(end.x, midY), color, width);
            Widgets.DrawLine(new Vector2(end.x, midY), end, color, width);
        }
        private void DrawEvolutionCard(RoyalEvolutionDef def, Rect rect)
        {
            Color color = Widgets.NormalOptionColor;
            Color bgColor = (EvolutionUnlocked(def, comp) ? TexUI.OldFinishedResearchColor : TexUI.AvailResearchColor);
            Color borderColor;
            if (selectedEvolution == def)
            {
                borderColor = TexUI.HighlightBorderResearchColor;
                bgColor += TexUI.HighlightBgResearchColor;
            }
            else
            {
                borderColor = TexUI.DefaultBorderResearchColor;
            }

            if (!def.AvailableForPawn(queen))
            {
                color = Color.red;
            }

            if (Widgets.CustomButtonText(ref rect, string.Empty, bgColor, color, borderColor))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                selectedEvolution = def;
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
            DrawLines(rect);
            foreach (EvolutionGraphNode node in evolutionLayout.Nodes)
            {
                DrawEvolutionCard(node.Def, node.Rect);
            }

            Text.Font = font;
            return evolutionLayout.Size.y + cardBuffer * 2f;
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

            DrawBottomText(Window.CloseButSize.x + cardBuffer, ref curY, inRect.width);

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
                Hediff geneIntegration = HediffMaker.MakeHediff(XenoGeneDefOf.XMT_GeneIntegration, queen);

                geneIntegration.Severity = (1.0f * difference) / 12;

                queen.health.AddHediff(geneIntegration);
                FilthMaker.TryMakeFilth(queen.PositionHeld, queen.MapHeld, InternalDefOf.Starbeast_Filth_Resin, count: 10*difference);
            }

            Close();
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
