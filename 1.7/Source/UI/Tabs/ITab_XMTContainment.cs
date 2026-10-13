using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class ITab_XMTContainment : ITab
    {
        private Building_ContainmentHarness Harness => SelThing as Building_ContainmentHarness;
        private Pawn Occupant => Harness?.ContainedThing as Pawn;

        public override bool IsVisible => Harness != null && Occupant != null;

        public ITab_XMTContainment()
        {
            size = new Vector2(320f, 360f);
            labelKey = "XMT_ContainmentTab";
            tutorTag = "Entity";
        }

        protected override void FillTab()
        {
            Building_ContainmentHarness harness = Harness;
            Pawn pawn = Occupant;
            CompXMT_Containment containment = harness?.ContainmentComp;
            if (pawn == null || containment == null)
            {
                return;
            }

            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            listing.Label("XMT_ContainmentOccupant".Translate(pawn.Named("PAWN")));

            string qualityTip = "XMT_ContainmentQualityDescription".Translate() + "\n\n" +
                containment.QualityExplanation();
            listing.Label("XMT_ContainmentQualityInspect".Translate(
                containment.ContainmentQuality.ToString("F1")), tooltip: qualityTip);
            listing.Label((containment.Powered ? "XMT_ContainmentPowered" : "XMT_ContainmentUnpowered").Translate());
            listing.Label((containment.AcidImmune ? "XMT_ContainmentAcidImmune" :
                "XMT_ContainmentNotAcidImmune").Translate());

            if (pawn.playerSettings != null)
            {
                Rect medicalRect = listing.GetRect(24f);
                Widgets.Label(new Rect(medicalRect.x, medicalRect.y, medicalRect.width * 0.45f,
                    medicalRect.height), "AllowMedicine".Translate() + ":");
                Rect buttonRect = new Rect(medicalRect.x + medicalRect.width * 0.5f, medicalRect.y,
                    medicalRect.width * 0.5f, medicalRect.height);
                MedicalCareUtility.MedicalCareSelectButton(buttonRect, pawn);
            }

            if (XMTUtility.IsXenomorph(pawn) && pawn.Info() != null)
            {
                listing.GapLine();
                listing.Label("XMT_ContainmentExtractionOptions".Translate());
                DrawExtractionOptions(listing, pawn);
            }

            listing.End();
            size = new Vector2(320f, Mathf.Max(220f, listing.CurHeight + 28f));
        }

        private static void DrawExtractionOptions(Listing_Standard listing, Pawn pawn)
        {
            CompPawnInfo info = pawn.Info();
            bool jellyAvailable = XenoGeneDefOf.XMT_Jelly_Extraction?.IsFinished == true;
            bool acidAvailable = XenoGeneDefOf.XMT_Acid_Utilization?.IsFinished == true;
            bool pheromoneAvailable = XenoGeneDefOf.XMT_CryptimorphicPheromones?.IsFinished == true;

            DrawCheckbox(listing, "XMT_JellyExtraction".Translate(), ref info.extractJelly,
                jellyAvailable ? "XMT_JellyExtractionDescription".Translate() : "XMT_RequiresJellyExtraction".Translate(),
                !jellyAvailable);
            DrawCheckbox(listing, "XMT_ResinExtraction".Translate(), ref info.extractResin,
                jellyAvailable ? "XMT_ResinExtractionDescription".Translate() : "XMT_RequiresJellyExtraction".Translate(),
                !jellyAvailable);
            DrawCheckbox(listing, "XMT_AcidExtraction".Translate(), ref info.extractAcid,
                acidAvailable ? "XMT_AcidExtractionDescription".Translate() : "XMT_RequiresAcidExtraction".Translate(),
                !acidAvailable);
            DrawCheckbox(listing, "XMT_PheromoneExtraction".Translate(), ref info.extractPheromone,
                pheromoneAvailable ? "XMT_PheromoneExtractionDescription".Translate() : "XMT_RequiresPheromoneExtraction".Translate(),
                !pheromoneAvailable);
        }

        private static void DrawCheckbox(Listing_Standard listing, string label, ref bool value,
            string tooltip, bool disabled)
        {
            Rect rect = listing.GetRect(28f);
            Widgets.CheckboxLabeled(rect, label, ref value, disabled);
            TooltipHandler.TipRegion(rect, tooltip);
        }
    }
}
