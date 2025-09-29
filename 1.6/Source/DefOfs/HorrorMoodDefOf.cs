using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xenomorphtype
{
    [DefOf]
    public class HorrorMoodDefOf
    {
        static HorrorMoodDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HorrorMoodDefOf));
        }

        //Traits
        public static TraitDef XMT_Survivor;

        //Universal
        public static ThoughtDef XMT_CommuneWithQueen;

        //Victims
        public static ThoughtDef DeathShriek;
        public static ThoughtDef Cocooned;
        public static ThoughtDef Grabbed;
        public static ThoughtDef GrabbedObsessed;
        public static ThoughtDef Ovamorphed;
        public static ThoughtDef OvamorphedAdvancedMood;
        public static ThoughtDef ParasiteLatchedMood;
        public static ThoughtDef Chestburst;
        public static ThoughtDef VictimSnuggledHappy;
        public static ThoughtDef VictimSnuggledScared;
        public static ThoughtDef VictimTrophallaxisHappy;
        public static ThoughtDef VictimTrophallaxisScared;
        public static ThoughtDef VictimNightmareMood;

        //Starbeasts
        public static ThoughtDef XMT_ImpureUrge;
        public static ThoughtDef TooMuchNestWork;
        public static ThoughtDef HostImpregnated;
        public static ThoughtDef CapturedHost;
        public static ThoughtDef GrabbedPrey;
        public static ThoughtDef OvamorphedVictim;
        public static ThoughtDef SnuggledVictim;
        public static ThoughtDef GaveTrophallaxis;
        public static ThoughtDef RecievedTrophallaxis;
        
    }  
}
