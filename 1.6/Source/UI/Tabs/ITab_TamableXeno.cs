
using RimWorld;
using System.Text;
using UnityEngine;
using Verse;
using static System.Net.Mime.MediaTypeNames;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    public class ITab_TamableXeno : ITab
    {
        private static float optionHeight = 28f;
        private static float spacer = 5f;
        public override bool IsVisible
        {
            get
            {
                return SelPawn != null && SelPawn.IsAdvancedTameable();
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

        static ITab_TamableXeno()
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
                if (def == InternalDefOf.XMT_Starbeast_AlienRace ||
                    def == InternalDefOf.XMT_Royal_AlienRace ||
                    def.thingClass == typeof(Building_HoldingPlatform))
                {
                    def.inspectorTabs?.Add(typeof(ITab_TamableXeno));
                    def.inspectorTabsResolved?.Add(InspectTabManager.GetSharedInstance(typeof(ITab_TamableXeno)));
                }
        }

        public ITab_TamableXeno()
        {
            size = new Vector2(280f, 0f);
            labelKey = "XMT_Tame";
            tutorTag = "Entity";
        }

        public override void OnOpen()
        {
            base.OnOpen();
        }

        protected override void FillTab()
        {
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.maxOneColumn = true;
            listing_Standard.Begin(rect);

            Rect LabelRect =  listing_Standard.Label("XMT_Tame".Translate());

            float height = (optionHeight+spacer)*5;

            Rect TamingOptionsRect = listing_Standard.GetRect(height).Rounded();

            float yoffset = LabelRect.height;

            string disabledText = null;
            if (!TamingUtility.CanTameBribery(SelPawn))
            {
                disabledText = "XMT_TameBriberyDisabled".Translate();
            }

            Rect bribeRect = new Rect(0f, yoffset, rect.width, optionHeight);
            Widgets.CheckboxLabeled(bribeRect, "XMT_TameBribery".Translate(), ref SelPawn.GetMorphComp().ShouldTameBribe, disabledText != null);
            Widgets.DrawHighlightIfMouseover(bribeRect);
            TaggedString bribeDescription = "XMT_TameBriberyDescription".Translate();
            if (disabledText != null)
            {
                bribeDescription += "\n\n" + disabledText.Colorize(ColoredText.WarningColor);
            }

            TooltipHandler.TipRegion(bribeRect, bribeDescription);

            yoffset += spacer + optionHeight;

            disabledText = null;
            if (!TamingUtility.CanTameConditioning(SelPawn))
            {
                disabledText = "XMT_TameConditionDisabled".Translate();
            }

            Rect conditionRect = new Rect(0f, yoffset, rect.width, optionHeight);
            Widgets.CheckboxLabeled(conditionRect, "XMT_TameCondition".Translate(), ref SelPawn.GetMorphComp().ShouldTameCondition, disabledText != null);
            Widgets.DrawHighlightIfMouseover(conditionRect);
            TaggedString conditionDescription = "XMT_TameConditionDescription".Translate();
            if (disabledText != null)
            {
                conditionDescription += "\n\n" + disabledText.Colorize(ColoredText.WarningColor);
            }

            TooltipHandler.TipRegion(conditionRect, conditionDescription);

            yoffset += spacer + optionHeight;

            disabledText = null;
            if (!TamingUtility.CanTameThreat(SelPawn))
            {
                disabledText = "XMT_TameThreatDisabled".Translate();
            }

            Rect threatRect = new Rect(0f, yoffset, rect.width, optionHeight);
            Widgets.CheckboxLabeled(threatRect, "XMT_TameThreat".Translate(), ref SelPawn.GetMorphComp().ShouldTameHostage, disabledText != null);
            Widgets.DrawHighlightIfMouseover(threatRect);
            TaggedString threatDescription = "XMT_TameThreatDescription".Translate();
            if (disabledText != null)
            {
                threatDescription += "\n\n" + disabledText.Colorize(ColoredText.WarningColor);
            }

            TooltipHandler.TipRegion(threatRect, threatDescription);

            yoffset += spacer + optionHeight;

            disabledText = null;
            if (!TamingUtility.CanTamePheromones(SelPawn))
            {
                disabledText = "XMT_TameSmellDisabled".Translate();
            }

            Rect smellRect = new Rect(0f, yoffset, rect.width, optionHeight);
            Widgets.CheckboxLabeled(smellRect, "XMT_TameSmell".Translate(), ref SelPawn.GetMorphComp().ShouldTamePheromone, disabledText != null);
            Widgets.DrawHighlightIfMouseover(smellRect);
            TaggedString smellDescription = "XMT_TameSmellDescription".Translate();
            if (disabledText != null)
            {
                smellDescription += "\n\n" + disabledText.Colorize(ColoredText.WarningColor);
            }

            TooltipHandler.TipRegion(smellRect, smellDescription);

            yoffset += spacer + optionHeight;

            disabledText = null;
            if (!TamingUtility.CanTameSocial(SelPawn))
            {
                disabledText = "XMT_TameSocialDisabled".Translate();
            }

            Rect socialRect = new Rect(0f, yoffset, rect.width, optionHeight);
            Widgets.CheckboxLabeled(socialRect, "XMT_TameSocial".Translate(), ref SelPawn.GetMorphComp().ShouldTameSocial, disabledText != null);
            Widgets.DrawHighlightIfMouseover(socialRect);
            TaggedString socialDescription = "XMT_TameSocialDescription".Translate();
            if (disabledText != null)
            {
                socialDescription += "\n\n" + disabledText.Colorize(ColoredText.WarningColor);
            }

            TooltipHandler.TipRegion(socialRect, socialDescription);
            listing_Standard.Gap();
            listing_Standard.Label("XMT_TameChance".Translate() + Mathf.Max(0, SelPawn.GetMorphComp().Taming - 0.25f).ToStringPercent());

            Widgets.EndGroup();

           
           
            
            listing_Standard.End();
            size = new Vector2(280f, listing_Standard.CurHeight + 10f + 24f);
        }

    }
}
