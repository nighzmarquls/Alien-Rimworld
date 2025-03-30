using AlienRace;
using RimWorld;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using Verse;
using Verse.Sound;


namespace Xenomorphtype
{
    internal class Dialogue_GeneExpression : GeneCreationDialogBase
    {
        protected override List<GeneDef> SelectedGenes => _chosenGenes;
        protected List<GeneDef> _originalGenes;
        protected List<GeneDef> _chosenGenes;
        protected List<GeneDef> AvailableGenes => _hiveGenes;
        protected List<GeneDef> _hiveGenes;
        protected override string Header => "Gene Expression Control";
        public override Vector2 InitialSize => new Vector2(Mathf.Min(UI.screenWidth, 1036), UI.screenHeight - 4);
        protected override string AcceptButtonLabel => "Accept Genes";

        protected Thing target;

        protected override bool CanAccept()
        {
            List<GeneDef> selectedGenes = SelectedGenes;
            foreach (GeneDef selectedGene in SelectedGenes)
            {
                if (selectedGene.prerequisite != null && !selectedGenes.Contains(selectedGene.prerequisite))
                {
                    Messages.Message("MessageGeneMissingPrerequisite".Translate(selectedGene.label).CapitalizeFirst() + ": " + selectedGene.prerequisite.LabelCap, null, MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }
            }
            return true;
        }
        protected override void Accept()
        {
            if (XMTSettings.LogBiohorror)
            {
                Log.Message("attempting to alter genes on " + target);
            }

            int differences = 0; 
            foreach(GeneDef gene in SelectedGenes)
            {
                if(_originalGenes.Contains(gene))
                {
                    continue;
                }
                differences++;
            }

            foreach(GeneDef gene in _originalGenes)
            {
                if (!SelectedGenes.Contains(gene))
                {
                    differences++;
                }
            }

            if (differences > 0)
            {
                Pawn targetPawn = target as Pawn;

                if (targetPawn != null)
                {
                    Hediff geneIntegration = HediffMaker.MakeHediff(XenoGeneDefOf.XMT_GeneIntegration, targetPawn);

                    geneIntegration.Severity = (1.0f * differences) / 24;

                    targetPawn.health.AddHediff(geneIntegration);

                    if(targetPawn.genes != null)
                    {
                        targetPawn.genes.xenotypeName = xenotypeName;
                    }
                    BioUtility.AssignAlteredGeneExpression(ref target, SelectedGenes);

                    AlienPartGenerator.AlienComp testComp = targetPawn.GetComp<AlienPartGenerator.AlienComp>();
                    if (testComp != null)
                    {
                        Log.Message("Found Alien Comp on " + targetPawn);
                        testComp.RegenerateAddonsForced();
                    }
                    else
                    {
                        Log.Message("Did not find Alien Comp on " + targetPawn);
                    }
                }
                else
                {
                    BioUtility.AssignAlteredGeneExpression(ref target, SelectedGenes);
                }

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("applied altered genes to " + target);
                }
            }

            Close();
        }

        public Dialogue_GeneExpression(Thing thing)
        {
            target = thing;
            _originalGenes = BioUtility.GetGeneForExpressionList(thing);
            _chosenGenes = _originalGenes.ListFullCopy();
            _hiveGenes = BioUtility.GetAllHiveGenes(thing.Map);
            xenotypeName = string.Empty;
            forcePause = true;
            absorbInputAroundWindow = true;
            alwaysUseFullBiostatsTableHeight = true;
            searchWidgetOffsetX = GeneCreationDialogBase.ButSize.x * 2f + 4f;
        }

        protected override void DrawGenes(Rect rect)
        {
            GUI.BeginGroup(rect);
            Rect rect2 = new Rect(0f, 0f, rect.width - 16f, scrollHeight);
            float curY = 0f;
            Widgets.BeginScrollView(rect.AtZero(), ref scrollPosition, rect2);
            Rect containingRect = rect2;
            containingRect.y = scrollPosition.y;
            containingRect.height = rect.height;
            DrawSection(rect, SelectedGenes, "SelectedGenepacks".Translate(), ref curY, ref selectedHeight, adding: false, containingRect);
            curY += 8f;
            DrawSection(rect, AvailableGenes, "GenepackLibrary".Translate(), ref curY, ref unselectedHeight, adding: true, containingRect);
            if (Event.current.type == EventType.Layout)
            {
                scrollHeight = curY;
            }

            Widgets.EndScrollView();
            GUI.EndGroup();
        }

        private void DrawSection(Rect rect, List<GeneDef> genes, string label, ref float curY, ref float sectionHeight, bool adding, Rect containingRect)
        {

            float curX = 4f;
            Rect rect2 = new Rect(10f, curY, rect.width - 16f - 10f, Text.LineHeight);
            Widgets.Label(rect2, label);
            if (!adding)
            {
                Text.Anchor = TextAnchor.UpperRight;
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(rect2, "ClickToAddOrRemove".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            curY += Text.LineHeight + 3f;
            float num = curY;
            Rect rect3 = new Rect(0f, curY, rect.width, sectionHeight);
            Widgets.DrawRectFast(rect3, Widgets.MenuSectionBGFillColor);
            curY += 4f;
            if (!genes.Any())
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(rect3, "(" + "NoneLower".Translate() + ")");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                for (int i = 0; i < genes.Count; i++)
                {
                    GeneDef gene = genes[i];

                    if (curX + GeneCreationDialogBase.GeneSize.x + 8 > rect.width - 16f)
                    {
                        curX = 4f;
                        curY += GeneCreationDialogBase.GeneSize.y + 8f + 14f;
                    }

                    if (adding && SelectedGenes.Contains(gene))
                    {
                        Widgets.DrawLightHighlight(new Rect(curX, curY, GeneCreationDialogBase.GeneSize.x + 8f, GeneCreationDialogBase.GeneSize.y + 8f));
                        
                        curX += GeneCreationDialogBase.GeneSize.x + 22f;
                    }
                    else if (DrawGene(gene, ref curX, curY, containingRect))
                    {
                        if (adding)
                        {
                            SoundDefOf.Tick_High.PlayOneShotOnCamera();
                            _chosenGenes.Add(gene);
                        }
                        else
                        {
                            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                            _chosenGenes.Remove(gene);
                        }

                        if (!xenotypeNameLocked)
                        {
                            xenotypeName = GeneUtility.GenerateXenotypeNameFromGenes(SelectedGenes);
                        }

                        OnGenesChanged();
                        break;
                    }
                }
            }

            curY += GeneCreationDialogBase.GeneSize.y + 12f;
            if (Event.current.type == EventType.Layout)
            {
                sectionHeight = curY - num;
            }

        }

        private bool DrawGene(GeneDef gene, ref float curX, float curY, Rect containingRect)
        {
            bool result = false;
            if (gene == null)
            {
                return result;
            }

            bool overridden = leftChosenGroups.Any((GeneLeftChosenGroup x) => x.overriddenGenes.Contains(gene));
            Rect geneRect = new Rect(curX, curY + 4f, GeneCreationDialogBase.GeneSize.x, GeneCreationDialogBase.GeneSize.y);
            string extraTooltip = null;

            if (leftChosenGroups.Any((GeneLeftChosenGroup x) => x.leftChosen == gene))
            {
                extraTooltip = GroupInfo(leftChosenGroups.FirstOrDefault((GeneLeftChosenGroup x) => x.leftChosen == gene));
            }
            else if (cachedOverriddenGenes.Contains(gene))
            {
                extraTooltip = GroupInfo(leftChosenGroups.FirstOrDefault((GeneLeftChosenGroup x) => x.overriddenGenes.Contains(gene)));
            }
            else if (randomChosenGroups.ContainsKey(gene))
            {
                extraTooltip = ("GeneWillBeRandomChosen".Translate() + ":\n" + randomChosenGroups[gene].Select((GeneDef x) => x.label).ToLineList("  - ", capitalizeItems: true)).Colorize(ColoredText.TipSectionTitleColor);
            }

            if (Mouse.IsOver(geneRect))
            {
                Widgets.DrawHighlight(geneRect);
            }

            GeneUIUtility.DrawGeneDef(gene, geneRect, GeneType.Endogene, () => extraTooltip, doBackground: false, clickable: false, overridden);
            
            if (!containingRect.Overlaps(geneRect))
            {
                curX = geneRect.xMax + 14f;
                return false;
            }

            if (Widgets.ButtonInvisible(geneRect))
            {
                result = true;
            }

            curX += GeneCreationDialogBase.GeneSize.x + 4f;
            
            curX = Mathf.Max(curX, geneRect.xMax + 14f);



            return result;

            static string GroupInfo(GeneLeftChosenGroup group)
            {
                if (group == null)
                {
                    return null;
                }

                return ("GeneOneActive".Translate() + ":\n  - " + group.leftChosen.LabelCap + " (" + "Active".Translate() + ")" + "\n" + group.overriddenGenes.Select((GeneDef x) => (x.label + " (" + "Suppressed".Translate() + ")").Colorize(ColorLibrary.RedReadable)).ToLineList("  - ", capitalizeItems: true)).Colorize(ColoredText.TipSectionTitleColor);
            }
        }

        protected override void UpdateSearchResults()
        {
            
        }
    }
}
