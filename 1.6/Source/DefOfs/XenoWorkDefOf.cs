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
    internal class XenoWorkDefOf
    {
        static XenoWorkDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoWorkDefOf));
        }

        public static ThingDef XMT_CorpseSculptureSmall;
        public static ThingDef XMT_CorpseSculptureLarge;
        public static ThingDef XMT_CorpseSculptureGrand;

        public static DesignationDef XMT_Abduct;
        public static DesignationDef XMT_Release;
        public static DesignationDef XMT_CorpseArt;
        public static DesignationDef XMT_Friend;
        public static DesignationDef XMT_Enemy;

        public static JobDef XMT_Ritual_Metamorphosis;

        public static JobDef XMT_ImplantHunt;
        public static JobDef XMT_StealthHunt;
        public static JobDef XMT_AbductHost;
        public static JobDef XMT_CocoonTarget;
        public static JobDef XMT_ApplyOvomorphing;
        public static JobDef XMT_ApplyLardering;
        public static JobDef XMT_MoveOvomorph;
        public static JobDef XMT_Snuggle;
        public static JobDef XMT_MarkEnemy;
        public static JobDef XMT_Seduce;
        public static JobDef XMT_Mature;
        public static JobDef XMT_Sabotage;
        public static JobDef XMT_Hibernate;
        public static JobDef XMT_HideInSpot;
        public static JobDef XMT_HiveBuilding;
        public static JobDef XMT_PerformTrophallaxis;
        public static JobDef XMT_PruneLarder;
        public static JobDef XMT_MergeIntoJellyWell;
        public static JobDef XMT_WallClimb;
        public static JobDef XMT_ProduceJelly;
        public static JobDef XMT_AlterGenes;
        public static JobDef XMT_MutateTarget;
        public static JobDef XMT_CopyGenes;
        public static JobDef XMT_LayOvomorph;
        public static JobDef XMT_GeneDevour;
        public static JobDef XMT_CorpseSculpture;

        public static JobDef XMT_Protofluid;

        public static JobDef XMT_AdvancedTameWithFood;
        public static JobDef XMT_AdvancedTameThreat;
        public static JobDef XMT_AdvancedTame;
        public static JobDef XMT_ExtractJelly;
        public static JobDef XMT_ExtractResin;
        // Vanilla Work Types


        public static WorkTypeDef Firefighter;
        public static WorkTypeDef Patient;
        public static WorkTypeDef Doctor;
        public static WorkTypeDef PatientBedRest;
        public static WorkTypeDef BasicWorker;
        public static WorkTypeDef Warden;
        public static WorkTypeDef Handling;
        public static WorkTypeDef Cooking;
        public static WorkTypeDef Hunting;
        public static WorkTypeDef Construction;
        public static WorkTypeDef Growing;
        public static WorkTypeDef Mining;
        public static WorkTypeDef PlantCutting;
        public static WorkTypeDef Smithing;
        public static WorkTypeDef Tailoring;
        public static WorkTypeDef Art;
        public static WorkTypeDef Crafting;
        public static WorkTypeDef Hauling;
        public static WorkTypeDef Cleaning;
        public static WorkTypeDef Research;
        public static WorkTypeDef Childcare;
    }
}
