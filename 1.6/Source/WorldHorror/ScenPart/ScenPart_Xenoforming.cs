
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class ScenPart_Xenoforming : ScenPart
    {
        private FloatRange levelRange;

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            Rect scenPartRect = listing.GetScenPartRect(this, ScenPart.RowHeight * 3f + 31f);
            Widgets.FloatRange(new Rect(scenPartRect.x, scenPartRect.y + ScenPart.RowHeight, scenPartRect.width, 31f), listing.CurHeight.GetHashCode(), ref levelRange, 0f, 1f, "ConfigurableLevel");

        }

        public override void PreConfigure()
        {
            base.PostWorldGenerate();
            Log.Message("Loading ScenPart for: " + levelRange);
            XenoformingUtility.SetXenoforming(levelRange.RandomInRange * 100);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref levelRange, "levelRange");
        }
    }
}
