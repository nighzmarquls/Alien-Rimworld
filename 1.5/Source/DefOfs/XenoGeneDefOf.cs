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
    internal class XenoGeneDefOf
    { 
        static XenoGeneDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(InternalDefOf));
        }

        // human xen biotech researchdefs
        public static ResearchTabDef XMT_HumanResearchTab;

        // xeno biotech researchdefs
        public static ResearchProjectDef XMT_Starbeast_Genetics;

        // xeno items 
        public static ThingDef      XMT_Genepack;

        public static HediffDef     XMT_GeneIntegration;

        // helpless hediff
        public static HediffDef     XMT_Helpless;

        // xeno influence hediffs
        public static HediffDef     XMT_HorrorPregnant;

        // host gene parts
        public static HediffDef     XMT_ThrumboHorn;
        
        // host genes
        public static GeneDef       XMT_Chemfuel_Metabolism;
        public static GeneDef       XMT_Muffalo_Ruff;

        // mutation set
        public static XMT_MutationsHealthSet   XMT_MutationsSet;
        public static XMT_MutationsHealthSet   XMT_LovinMutationSet;

        // health set
        public static XMT_InfluenceHealthSet   XMT_InfluencesSet;

        // goo horrors
        public static XMT_GooHorrorSet  XMT_GooGenericSet;

        // hybrid genes
        public static XMT_GeneSetDef    XMT_HybridGenes;
        public static XMT_GeneSetDef    XMT_UnknownGenes;

        // transformations
        public static XMT_TransformationSet XMT_DefaultTransformationSet;

        public static GeneDef           XMT_Libido;

        // biotechGenes
        public static GeneDef           Sterile;
        public static GeneDef           Fertile;
    }
}
