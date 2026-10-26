using RimWorld;
using Verse;


namespace Xenomorphtype
{
    [DefOf]
    internal class XenoMapDefOf
    {
        static XenoMapDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoMapDefOf));
        }

        public static BiomeDef XMT_DessicatedBlight;
        public static BiomeDef XMT_BarrenDustbowl;
        public static BiomeDef XMT_ChilledWaste;

        public static TileMutatorDef XMT_SettlementAftermath;

        public static GenStepDef XMT_AttackAftermath;
        public static GenStepDef XMT_AbductPopulation;

        public static IncidentDef XMT_GiveQuest_queenNest;
        public static QuestScriptDef XMT_SettlementDistressSignal;
    }
    
}
