
using RimWorld;
using System.Text;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    public class ITab_Xeno : ITab
    {
        private static float optionHeight = 28f;
        private static float spacer = 5f;
        public override bool IsVisible
        {
            get
            {
                if (!ModsConfig.AnomalyActive)
                {
                    return false;
                }

                if (base.SelThing is Building_HoldingPlatform building_HoldingPlatform)
                {
                    return building_HoldingPlatform.HeldPawn != null && XMTUtility.IsXenomorph(building_HoldingPlatform.HeldPawn);
                }

                if (base.SelThing is Pawn && base.SelThing.IsOnHoldingPlatform)
                {
                    return XMTUtility.IsXenomorph(SelThing);
                }

                return false;
            }
        }

        protected override Pawn SelPawn
        {
            get
            {
                Pawn selPawn = base.SelPawn;
                if (selPawn != null)
                {
                    return selPawn;
                }

                if (base.SelThing is Building_HoldingPlatform building_HoldingPlatform)
                {
                    return building_HoldingPlatform.HeldPawn;
                }

                return null;
            }
        }

        static ITab_Xeno()
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
                if (def == InternalDefOf.XMT_Starbeast_AlienRace ||
                    def == InternalDefOf.XMT_Larva ||
                    def == InternalDefOf.XMT_Royal_AlienRace ||
                    def.thingClass == typeof(Building_HoldingPlatform))
                {
                    def.inspectorTabs?.Add(typeof(ITab_Xeno));
                    def.inspectorTabsResolved?.Add(InspectTabManager.GetSharedInstance(typeof(ITab_Xeno)));
                }
        }

        public ITab_Xeno()
        {
            size = new Vector2(280f, 0f);
            labelKey = "TabEntity";
            tutorTag = "Entity";

        }

        public override void OnOpen()
        {
            if (!ModLister.CheckAnomaly("entity itab"))
            {
                CloseTab();
            }
            else
            {
                base.OnOpen();
            }
        }

        protected override void FillTab()
        {
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.maxOneColumn = true;
            listing_Standard.Begin(rect);
            if (SelPawn.ParentHolder is Thing thing && thing.TryGetComp(out CompEntityHolder comp))
            {
                StatDef containmentStrength = StatDefOf.ContainmentStrength;
                float containmentStrength2 = comp.ContainmentStrength;
                string text = containmentStrength.description + "\n\n" + containmentStrength.Worker.GetExplanationFull(StatRequest.For(thing), containmentStrength.toStringNumberSense, containmentStrength2);
                Widgets.DrawHighlightIfMouseover(listing_Standard.Label(containmentStrength.LabelCap + ": " + containmentStrength.ValueToString(containmentStrength2), -1f, new TipSignal(text, GetHashCode() + 1)));
            }

            StringBuilder stringBuilder = new StringBuilder();
            float num = ContainmentUtility.InitiateEscapeMtbDays(SelPawn, stringBuilder);
            int numTicks = Mathf.FloorToInt(num * 60000f);
            TaggedString taggedString = "HoldingPlatformEscapeMTBDays".Translate() + ": ";
            if (num < 0f)
            {
                taggedString += "Never".Translate();
            }
            else
            {
                taggedString += numTicks.ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor);
            }

            string text2 = "HoldingPlatformEscapeMTBDaysDesc".Translate();
            if (stringBuilder.Length > 0)
            {
                text2 = text2 + "\n\n" + stringBuilder;
            }

            Widgets.DrawHighlightIfMouseover(listing_Standard.Label(taggedString, -1f, new TipSignal(text2, GetHashCode() + 2)));
            CompStudiable compStudiable = SelPawn.TryGetComp<CompStudiable>();
            if (compStudiable != null)
            {
                DoStudyPeriodListing(listing_Standard, compStudiable);
            }

            if (compStudiable != null)
            {
                DoKnowledgeGainListing(listing_Standard, compStudiable);
            }

            listing_Standard.Gap(1f);
            Rect rect2 = listing_Standard.GetRect(24f).Rounded();
            TooltipHandler.TipRegionByKey(rect2, "MedicineQualityDescriptionEntity");
            Widgets.DrawHighlightIfMouseover(rect2);
            Rect rect3 = rect2;
            rect3.xMax = rect2.center.x - 4f;
            Rect rect4 = rect2;
            rect4.xMin = rect2.center.x + 4f;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect3, string.Format("{0}:", "AllowMedicine".Translate()));
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawButtonGraphic(rect4);
            MedicalCareUtility.MedicalCareSelectButton(rect4, SelPawn);
            listing_Standard.Gap(4f);
            CompHoldingPlatformTarget compHoldingPlatformTarget = SelPawn.TryGetComp<CompHoldingPlatformTarget>();
            if (compHoldingPlatformTarget != null)
            {
                float height = 132f;
                Rect rect5 = listing_Standard.GetRect(height).Rounded();
                Widgets.DrawMenuSection(rect5);
                Rect internalExtractionRect = rect5.ContractedBy(10f);
                Widgets.BeginGroup(internalExtractionRect);
                Rect EntityOptions = new Rect(0f, 0f, internalExtractionRect.width, 28f);
                if (Widgets.RadioButtonLabeled(EntityOptions, "EntityStudyMode_MaintainOnly".Translate(), compHoldingPlatformTarget.containmentMode == EntityContainmentMode.MaintainOnly))
                {
                    compHoldingPlatformTarget.containmentMode = EntityContainmentMode.MaintainOnly;
                }

                Widgets.DrawHighlightIfMouseover(EntityOptions);
                TooltipHandler.TipRegion(EntityOptions, "EntityStudyMode_MaintainOnlyDesc".Translate());
                EntityOptions.y += 28f;
                if (Widgets.RadioButtonLabeled(EntityOptions, "EntityStudyMode_Study".Translate(), compHoldingPlatformTarget.containmentMode == EntityContainmentMode.Study))
                {
                    compHoldingPlatformTarget.containmentMode = EntityContainmentMode.Study;
                }

                Widgets.DrawHighlightIfMouseover(EntityOptions);
                TooltipHandler.TipRegion(EntityOptions, "EntityStudyMode_StudyDesc".Translate());
                EntityOptions.y += 28f;
                if (Widgets.RadioButtonLabeled(EntityOptions, "EntityStudyMode_Release".Translate(), compHoldingPlatformTarget.containmentMode == EntityContainmentMode.Release))
                {
                    compHoldingPlatformTarget.containmentMode = EntityContainmentMode.Release;
                }

                Widgets.DrawHighlightIfMouseover(EntityOptions);
                TooltipHandler.TipRegion(EntityOptions, "EntityStudyMode_ReleaseDesc".Translate());
                EntityOptions.y += 28f;
                if (Widgets.RadioButtonLabeled(EntityOptions, "EntityStudyMode_Execute".Translate(), compHoldingPlatformTarget.containmentMode == EntityContainmentMode.Execute, !compHoldingPlatformTarget.Props.canBeExecuted))
                {
                    if (!compHoldingPlatformTarget.Props.canBeExecuted)
                    {
                        Messages.Message("CantBeExecuted".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    }
                    else
                    {
                        compHoldingPlatformTarget.containmentMode = EntityContainmentMode.Execute;
                    }
                }

                Widgets.DrawHighlightIfMouseover(EntityOptions);
                TooltipHandler.TipRegion(EntityOptions, "EntityStudyMode_ExecuteDesc".Translate() + (compHoldingPlatformTarget.Props.canBeExecuted ? "" : ("\n\n" + "CantBeExecuted".Translate().ToString())));
                EntityOptions.y += 28f;
                Widgets.EndGroup();
                listing_Standard.Gap();

                height = (optionHeight + spacer)*3;

                Rect ExtractionOptions = listing_Standard.GetRect(height).Rounded();
                Widgets.DrawMenuSection(ExtractionOptions);
                internalExtractionRect = ExtractionOptions.ContractedBy(10f);
                Widgets.BeginGroup(internalExtractionRect);
                string disabledText = null;
                if (!XenoGeneDefOf.XMT_Jelly_Extraction.IsFinished)
                {
                    disabledText = "XMT_RequiresJellyExtraction".Translate();
                }
                else
                {
                    Building_HoldingPlatform heldPlatform = compHoldingPlatformTarget.HeldPlatform;
                    if (heldPlatform != null && heldPlatform.HasAttachedBioferriteHarvester)
                    {
                        disabledText = "BioferriteHarvesterAttached".Translate();
                    }
                }

                float yoffset = 0f;
                Rect jellyRect = new Rect(0f, yoffset, internalExtractionRect.width, 28f);
                Widgets.CheckboxLabeled(jellyRect, "XMT_JellyExtraction".Translate(), ref SelPawn.Info().extractJelly, disabledText != null);
                Widgets.DrawHighlightIfMouseover(jellyRect);
                TaggedString jellyDescription = "XMT_JellyExtractionDescription".Translate();
                if (disabledText != null)
                {
                    jellyDescription += "\n\n" + disabledText.Colorize(ColoredText.WarningColor);
                }

                TooltipHandler.TipRegion(jellyRect, jellyDescription);

                yoffset += spacer + jellyRect.height;

                Rect resinRect = new Rect(0f, yoffset, internalExtractionRect.width, 28f);
                Widgets.CheckboxLabeled(resinRect, "XMT_ResinExtraction".Translate(), ref SelPawn.Info().extractResin, disabledText != null);
                Widgets.DrawHighlightIfMouseover(resinRect);
                TaggedString resinDescription = "XMT_ResinExtractionDescription".Translate();
                if (disabledText != null)
                {
                    resinDescription += "\n\n" + disabledText.Colorize(ColoredText.WarningColor);
                }

                TooltipHandler.TipRegion(resinRect, resinDescription);


                Widgets.EndGroup();
            }

            

            listing_Standard.End();
            size = new Vector2(280f, listing_Standard.CurHeight + 10f + 24f);
        }

        public static void DoStudyPeriodListing(Listing_Standard listing, CompStudiable studiable)
        {
            if (studiable.Props.frequencyTicks > 0)
            {
                Widgets.DrawHighlightIfMouseover(listing.Label("StudyInterval".Translate() + ": " + studiable.Props.frequencyTicks.ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor), -1f, "StudyIntervalDesc".Translate()));
            }
        }

        public static void DoKnowledgeGainListing(Listing_Standard listing, CompStudiable studiable)
        {
            string text = "StudyKnowledgeGainDesc".Translate();
            CompHoldingPlatformTarget compHoldingPlatformTarget = studiable.Pawn.TryGetComp<CompHoldingPlatformTarget>();
            string text2 = "";
            if (compHoldingPlatformTarget != null && compHoldingPlatformTarget.CurrentlyHeldOnPlatform)
            {
                CompEntityHolder comp = compHoldingPlatformTarget.HeldPlatform.GetComp<CompEntityHolder>();
                float studyKnowledgeAmountMultiplier = ContainmentUtility.GetStudyKnowledgeAmountMultiplier(studiable.Pawn, comp);
                text2 += string.Format("  - {0}: x{1:F1}", "FactorContainmentStrength".Translate(), studyKnowledgeAmountMultiplier);
                if (compHoldingPlatformTarget.HeldPlatform.HasAttachedElectroharvester)
                {
                    text2 += string.Format("\n  - {0}: x{1:F1}", "FactorElectroharvester".Translate(), 0.5f);
                }
            }

            if (studiable.Pawn.TryGetComp<CompActivity>(out var comp2))
            {
                if (text2 != "")
                {
                    text2 += "\n";
                }

                text2 += string.Format("  - {0}: x{1:F1}", "FactorActivity".Translate(), comp2.ActivityResearchFactor);
            }

            if (!string.IsNullOrEmpty(text2))
            {
                text = text + "\n\n" + text2;
            }

            Widgets.DrawHighlightIfMouseover(listing.Label("StudyKnowledgeGain".Translate() + ": " + (studiable.AdjustedAnomalyKnowledgePerStudy * 5f).ToStringDecimalIfSmall() + " (" + studiable.KnowledgeCategory.label + ")", -1f, text));
        }
    }
}
