using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class ITab_CryptimorphicAssessment : ITab
    {
        private Vector2 scroll;
        public ITab_CryptimorphicAssessment()
        {
            size = new Vector2(460f, 480f);
            labelKey = TissueAssessmentDef.Current.tabLabelKey;
        }
        public static bool Unlocked => TissueKnowledge.Unlocked;
        public override bool IsVisible => SelPawn != null && (!TissueAssessmentDef.Current.excludeMatureMorphs || SelPawn.GetMorphComp() == null) && Unlocked;
        protected override void FillTab()
        {
            Pawn pawn = SelPawn;
            if (pawn == null) return;
            Rect area = new Rect(15, 15, size.x - 30, size.y - 30);
            var data = TissueAssessmentDef.Current;
            var rows = TissueSummaryBuilder.Build(pawn, data);

            float width = area.width - 18;
            float height = 70 + rows.Sum(r => Text.CalcHeight(r.Key, width) + 12f);
            Rect content = new Rect(0, 0, width, Mathf.Max(area.height, height));
            Widgets.BeginScrollView(area, ref scroll, content);
            float influence = MutationTissueAssessment.VisibleInfluence(pawn, data.requireVisible);
            Widgets.Label(new Rect(0, 0, content.width, 36), data.saturationKey.Translate(Mathf.Clamp01(influence / CryptimorphicInfluenceDef.Current.matureMorphInfluence).ToStringPercent()));
            Widgets.FillableBar(new Rect(0, 39, content.width, 16), Mathf.Clamp01(influence / CryptimorphicInfluenceDef.Current.matureMorphInfluence));
            float y = 70;
            foreach (var row in rows)
            {
                float rowHeight = Text.CalcHeight(row.Key, width);
                Rect rect = new Rect(0, y, width, rowHeight);
                Widgets.Label(rect, row.Key);
                if (!row.Value.NullOrEmpty()) TooltipHandler.TipRegion(rect, row.Value);
                y += rowHeight + 12f;
            }
            Widgets.EndScrollView();
        }
    }
}
