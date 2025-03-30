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
    internal class ExternalDefOf
    {
        static ExternalDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ExternalDefOf));
        }

        //Vanilla Defs
        public static TraitDef Tough;
        public static TraitDef Masochist;
        public static StatDef CookSpeed;



        //cave flora
        public static ThingDef Bryolux;
        public static ThingDef Agarilux;
        public static ThingDef Glowstool;

        //joykind
        public static JoyKindDef Social;
        public static JoyKindDef Gaming_Dexterity;

        //body parts
        public static BodyPartGroupDef HeadAttackTool;
        public static BodyPartGroupDef Neck;
        public static BodyPartGroupDef Shoulders;
        public static BodyPartGroupDef Arms;
        public static BodyPartGroupDef Hands;
        public static BodyPartGroupDef Mouth;
        public static BodyPartDef Waist;
        public static BodyPartDef Skull;


        //mental breaks
        public static MentalBreakDef MurderousRage;
        

        //mental states
        public static MentalStateDef Crying;
        public static MentalStateDef Binging_Food;

        //items
        public static ThingDef ThrumboHorn;
        public static ThingDef WoolMuffalo;

        //Damage Defs
        public static DamageDef Decayed;

        //Thrumbo Products

        //Big and Small Framework
        [MayRequire("RedMattis.BetterPrerequisites")]
        public static StatDef SM_BodySizeOffset;
        [MayRequire("RedMattis.BetterPrerequisites")]
        public static StatDef SM_BodySizeMultiplier;

        //Water is Cold momo.updates.WaterIsCold
        [MayRequire("momo.updates.WaterIsCold")]
        public static HediffDef WetCold;

        //Dubs Stuff
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static HediffDef Washing;

        [MayRequire("Dubwise.DubsBadHygiene")]
        public static NeedDef Hygiene;

        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef sewagePipeStuff;

        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef PitLatrine;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef BasinStuff;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef KitchenSink;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef ToiletStuff;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef BathtubStuff;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef WaterTowerS;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef WaterTowerL;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef SewageOutlet;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef SewageTreatment;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef SewageSepticTank;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef WaterTrough;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef DBHSwimmingPool;
        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef HotTub;

        [MayRequire("Dubwise.DubsBadHygiene")]
        public static ThingDef airPipe;

        //Rimatomics
        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef waterPipe;
        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef coolingPipe;
        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef waterValve;
        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef coolantValve;
        

        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef CoolingWater;
        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef CoolingTower;
        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef ReactorCoreC;
        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef ReactorCoreB;
        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef ReactorCoreA;
        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef Turbine;
        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef BigTurbine;
        [MayRequire("Dubwise.Rimatomics")]
        public static ThingDef CoolingRadiator;

        //Vanilla Biotech
        [MayRequire("ludeon.rimWorld.biotech")]
        public static GeneDef DarkVision;

        //Vanilla Expanded
        //public static Hediff VEF_AcidBurn;
        //public static Hediff VEF_AcidBuildup;

        //Vanilla Expanded Androids
        [MayRequire("vanillaracesexpanded.android")]
        public static GeneDef VREA_SyntheticBody;

        //Alpha Genes
        [MayRequire("sarg.alphagenes")]
        public static DamageDef AG_AcidSpit;

        //Save Our Ship
        [MayRequire("kentington.saveourship2")]
        public static TerrainDef EmptySpace;

        [MayRequire("kentington.saveourship2")]
        public static ThingDef ShipHullTile;

        //RJW

        //Bodyparts
        [MayRequire("rim.job.world")]
        public static BodyPartDef Anus;
        
    }
}
