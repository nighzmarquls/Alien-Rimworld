using AlienRace;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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


        //Xenomorphtype Defs
        public static PawnKindDef           XMT_StarbeastKind;
        public static PawnKindDef           XMT_RoyaltyKind;
        public static PawnKindDef           XMT_Droplet;
        public static PawnKindDef           XMT_FeralStarbeastKind;

        public static ThingDef              XMT_Larva;
        public static ThingDef_AlienRace    XMT_Starbeast_AlienRace;
        public static ThingDef_AlienRace    XMT_Royal_AlienRace;

        public static HediffDef             XMT_Embryo;
        public static HediffDef             StarbeastOrganism;
        public static HediffDef             Undeveloped;
        public static HediffDef             Overdeveloped;
        public static HediffDef             StarbeastStealthHostile;
        public static HediffDef             StarbeastStealthFriendly;

        public static HediffDef             PawnInfoHediff;

        //Xenomorph bodyparts
        public static BodyPartGroupDef      StarbeastTailAttackTool;
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
        public static ThingDef              Starbeast_Fabric;

        //Xenomorph buildings
        public static ThingDef              XMT_Ovamorph;
        public static ThingDef              XMT_CocoonBase;
        public static ThingDef              XMT_CocoonBaseAnimal;
        public static ThingDef              XMT_HiddenNestSpot;
        public static ThingDef              HiveSleepingNest;
        public static ThingDef              HiveHidingSpot;
        public static ThingDef              HiveWebbing;
        public static ThingDef              Hivemass;
        public static ThingDef              AtmospherePylon;

        //Xenomorph related structures
        public static ThingDef              ShipChunkWithEgg;

        //Xenomorph terrain
        public static TerrainDef            AcidBurned;
        public static TerrainDef            HiveFloor;
        public static TerrainDef            BarrenDust;

        //Xenomorph affordance
        public static TerrainAffordanceDef  Resin;

        //Xenomorph joykinds
        public static JoyKindDef            NestTending;
        public static JoyKindDef            HuntingPrey;
        public static JoyKindDef            Communion;

       

        
    }
}

