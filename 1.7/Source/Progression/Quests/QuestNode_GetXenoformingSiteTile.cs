using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace Xenomorphtype
{
    // Keep vanilla's local landmark/range selection; global outbreaks do not require a land route.
    internal class QuestNode_GetXenoformingSiteTile : QuestNode_GetSiteTile
    {
        private const string SelectedTileKey = "xmtSelectedHillTile";

        protected override bool TestRunInt(Slate slate)
        {
            if (slate.TryGet(SelectedTileKey, out PlanetTile selected) && selected.Valid)
            {
                slate.Set(storeAs.GetValue(slate), selected);
                return true;
            }
            bool distant = Rand.Chance(XenoformingUtility.DistantHillChance(XenoformingUtility.GetXenoforming()));
            if (!distant)
            {
                if (!base.TestRunInt(slate)) return false;
                slate.Set(SelectedTileKey, slate.Get<PlanetTile>(storeAs.GetValue(slate)));
                return true;
            }
            if (!XenoformingUtility.TryFindDistantHillTile(out PlanetTile tile)) return false;
            slate.Set(SelectedTileKey, tile);
            slate.Set(storeAs.GetValue(slate), tile);
            return true;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            if (slate.TryGet(SelectedTileKey, out PlanetTile tile) && tile.Valid)
                slate.Set(storeAs.GetValue(slate), tile);
            else TestRunInt(slate);
        }
    }
}
