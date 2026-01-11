using AlienRace;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    [DefOf]
    public static class InternalDefOf
    {
        static InternalDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(InternalDefOf));
        }

        public static FleshTypeDef          StarbeastFlesh;
        public static FactionDef            XMT_PlayerHive;

        //Xenomorphtype Defs
        public static PawnKindDef           XMT_StarbeastKind;
        public static PawnKindDef           XMT_RoyaltyKind;
        public static PawnKindDef           XMT_Droplet;
        public static PawnKindDef           XMT_FeralStarbeastKind;

        public static ThingDef              XMT_Larva;
        public static ThingDef_AlienRace    XMT_Starbeast_AlienRace;
        public static ThingDef_AlienRace    XMT_Royal_AlienRace;

        public static HediffDef             XMT_Embryo;
        public static HediffDef             XMT_Slowdown;
        public static HediffDef             StarbeastOrganism;
        public static HediffDef             Undeveloped;
        public static HediffDef             Overdeveloped;
        public static HediffDef             StarbeastStealthHostile;
        public static HediffDef             StarbeastStealthFriendly;
        public static HediffDef             AcidCorrosion;

        public static HediffDef             StarbeastCocoon;
        public static HediffDef             PawnInfoHediff;
        public static HediffDef             XMT_Ambushed;
        public static HediffDef             XMT_Enthroned;
        public static HediffDef             XMT_Stabilize;

        //Xenomorph bodyparts
        public static BodyPartGroupDef      StarbeastTailAttackTool;
        public static BodyPartDef           StarbeastBrain;
        public static BodyPartDef           StarbeastHeart;
        public static BodyPartDef           StarbeastTail;
        public static BodyPartDef           StarbeastSkull;
        public static BodyPartDef           StarbeastCrest;
        public static BodyPartDef           XMT_LesserShoulder;

        //Xenomorph filth
        public static ThingDef              Starbeast_Filth_AcidBlood;
        public static ThingDef              Starbeast_Filth_Resin;

        //Xenomorph Climber
        public static ThingDef              PawnClimber;
        public static ThingDef              PawnClimbUnder;

        //Effectors
        public static EffecterDef           ResinBuild;

        //Xenomorph Materials
        public static ThingDef              Starbeast_Resin;
        public static ThingDef              Starbeast_Chitin;
        public static ThingDef              Starbeast_Flesh_Meat;
        public static ThingDef              Starbeast_Fabric;
        public static ThingDef              Starbeast_Jelly;

        public static DesignationCategoryDef    XMT_Hive;

        //Xenomorph related structures
        public static ThingDef              ShipChunkWithEgg;

        //Xenomorph terrain
        public static TerrainDef            AcidBurned;
        public static TerrainDef            LightAcidBurned;
        public static TerrainDef            MediumAcidBurned;

        public static TerrainDef            HiveFloor;
        public static TerrainDef            HeavyHiveFloor;
        public static TerrainDef            SmoothHiveFloor;
        public static TerrainDef            BarrenDust;

        //Xenomorph affordance
        public static TerrainAffordanceDef  Resin;

        //Xenomorph joykinds
        public static JoyKindDef            NestTending;
        public static JoyKindDef            HuntingPrey;
        public static JoyKindDef            Communion;

        //Xenomorph render Offsets
        public static OffsetListDef         XMT_HeadAttachmentOffsets;
        
    }
}

