

using RimWorld;
using Verse;

namespace Xenomorphtype
{
    [DefOf]
    internal class XenoBuildingDefOf
    {
        static XenoBuildingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoBuildingDefOf));
        }

        //AcidImmune Building List
        public static XMT_ThingDefList XMT_AcidImmuneBuildings;
        //Xenomorph buildings
        public static ThingDef XMT_JellyWell;
        public static ThingDef XMT_Ovomorph;
        public static ThingDef XMT_CocoonBase;
        public static ThingDef XMT_CocoonBaseAnimal;
        public static ThingDef XMT_HiddenNestSpot;
        public static ThingDef HiveSleepingCocoon;
        public static ThingDef HiveHidingSpot;
        public static ThingDef HiveWebbing;
        public static ThingDef Hivemass;
        public static ThingDef AtmospherePylon;
        public static ThingDef XMT_Ovothrone;
        public static ThingDef XMT_AmbushSpot;

        public static TerrainDef HiveFloor;
        
    }
}