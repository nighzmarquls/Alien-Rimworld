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

        public static DesignationDef XMT_Abduct;
        public static DesignationDef XMT_Release;

        public static JobDef Ritual_Metamorphosis;

        public static JobDef ImplantHunt;
        public static JobDef StealthHunt;
        public static JobDef AbductHost;
        public static JobDef CocoonTarget;
        public static JobDef ApplyOvamorphing;
        public static JobDef ApplyLardering;
        public static JobDef MoveOvamorph;
        public static JobDef StarbeastSnuggle;
        public static JobDef StarbeastSeduce;
        public static JobDef StarbeastMature;
        public static JobDef StarbeastSabotage;
        public static JobDef StarbeastHibernate;
        public static JobDef StarbeastHideInSpot;
        public static JobDef StarbeastHiveBuilding;
        public static JobDef PerformTrophallaxis;
        public static JobDef PruneLarder;
        public static JobDef StarbeastWallClimb;
        public static JobDef StarbeastProduceJelly;
        public static JobDef AlterGenes;
        public static JobDef StarbeastLayOvamorph;
        public static JobDef StarbeastGeneDevour;

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
