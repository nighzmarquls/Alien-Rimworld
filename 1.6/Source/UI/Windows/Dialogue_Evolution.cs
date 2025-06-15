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
        const float cardWidth = 100f;
        const float cardHeight = 40f;
        const float cardBuffer = 4f;

        private Pawn queen;

        private CompQueen comp;

        private TaggedString text;

        private float scrollHeight;

        private Vector2 scrollPosition;

        private static List<TabRecord> tmpTabs = new List<TabRecord>();

        List<List<RoyalEvolutionDef>> evolutionDefs = new List<List<RoyalEvolutionDef>>();

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
            PrepEvolutionList();
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

        private int GetRankingOfEvo(RoyalEvolutionDef def, int currentRanking = 0)
        {
            int highestRanking = currentRanking;

            if (def.prerequisites?.Count > 0)
            {
                highestRanking++;

                foreach(RoyalEvolutionDef prereq in def.prerequisites)
                {
                    int ranking = GetRankingOfEvo(prereq, highestRanking);

                    if(ranking > highestRanking)
                    {
                        highestRanking = ranking;
                    }
                }
            }

            return highestRanking;
        }

        private void PrepEvolutionList()
        {
            //TODO: Do Better then this.
            HashSet<RoyalEvolutionDef> AllEvos = DefDatabase<RoyalEvolutionDef>.AllDefsListForReading.ToHashSet(); ;
            foreach (RoyalEvolutionDef def in AllEvos)
            {
                int ranking = GetRankingOfEvo(def);

                if(ranking >= evolutionDefs.Count())
                {
                    evolutionDefs.Add(new List<RoyalEvolutionDef> { def });
                }
                else
                {
                    evolutionDefs[ranking].Add(def);
                }
            }
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
            /* for later
            float curY = rect.y;
            foreach (List<RoyalEvolutionDef> rank in evolutionDefs)
            {
                float curX = rect.x;
                foreach (RoyalEvolutionDef def in rank)
                {

                    curX += (cardBuffer + cardWidth);
                }
                curY += (cardBuffer + cardHeight);
            }
            */

            /*
            Vector2 start = default(Vector2);
            Vector2 end = default(Vector2);
            List<RoyalTitlePermitDef> allDefsListForReading = DefDatabase<RoyalTitlePermitDef>.AllDefsListForReading;
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < allDefsListForReading.Count; j++)
                {
                    RoyalTitlePermitDef royalTitlePermitDef = allDefsListForReading[j];

                    Vector2 vector = DrawPosition(royalTitlePermitDef);
                    start.x = vector.x;
                    start.y = vector.y + 25f;
                    RoyalTitlePermitDef prerequisite = royalTitlePermitDef.prerequisite;
                    if (prerequisite != null)
                    {
                        Vector2 vector2 = DrawPosition(prerequisite);
                        end.x = vector2.x + 200f;
                        end.y = vector2.y + 25f;
                        /*if ((i == 1 && selectedPermit == royalTitlePermitDef) || selectedPermit == prerequisite)
                        {
                            Widgets.DrawLine(start, end, TexUI.HighlightLineResearchColor, 4f);
                        }
                        else if (i == 0)
                        {
                            Widgets.DrawLine(start, end, TexUI.DefaultLineResearchColor, 2f);
                        }
                    }
                }
            }
            */
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
            float curY = rect.y;
            GameFont font = Text.Font;
            Text.Font = GameFont.Tiny;
            DrawLines(rect);
            foreach (List<RoyalEvolutionDef> rank in evolutionDefs)
            {
                float curX = rect.x;
                foreach (RoyalEvolutionDef def in rank)
                {
                    Rect cardRect = new Rect(curX, curY, cardWidth, cardHeight);
                    DrawEvolutionCard(def, cardRect);
                    curX += (cardBuffer + cardWidth);
                }
                curY += (cardBuffer + cardHeight);
            }
            curY += cardBuffer*2;

            Text.Font = font;
            return curY - rect.y;
        }
        public override void DoWindowContents(Rect inRect)
        {
            Rect outRect = (inRect);
            outRect.yMax -= 4f + Window.CloseButSize.y;
            Text.Font = GameFont.Small;
            Rect viewRect = new Rect(outRect.x, outRect.y, outRect.width - 16f, scrollHeight);
            float curY = 0f;
            float curX = 0f;
            Widgets.Label(0f, ref curY, viewRect.width, text.Resolve());
            curY += 18f;

            Rect pawnRect = new Rect(0f, curY, viewRect.width / 4, viewRect.width / 4);
            DrawPawn(pawnRect);

            Rect infoRect = new Rect(0f, curY + pawnRect.height, pawnRect.width, viewRect.height);
            DrawInfo(infoRect);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            curX += pawnRect.width + 4f;
            Rect evoRect = new Rect(curX, curY, viewRect.width, viewRect.height);

            curY += DrawEvolutions(evoRect);

            if (Event.current.type == EventType.Layout)
            {
                scrollHeight = Mathf.Max(curY, outRect.height);
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
