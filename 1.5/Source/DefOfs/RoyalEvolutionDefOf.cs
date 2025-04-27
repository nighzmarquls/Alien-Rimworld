using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    [DefOf]
    internal class RoyalEvolutionDefOf
    {
        static RoyalEvolutionDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RoyalEvolutionDefOf));
        }
        public static RoyalEvolutionDef Evo_LarderSerum;
        public static RoyalEvolutionDef Evo_JellyWellSerum;
        public static RoyalEvolutionDef Evo_GeneControl;
        public static RoyalEvolutionDef Evo_GeneStorage;
        public static RoyalEvolutionDef Evo_GeneDigestion;
        public static RoyalEvolutionDef Evo_GeneSelfExpression;

        public static HediffDef XMT_Fertility;
       
    }
}
