using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class PsychicDefenseSettingsDef : Def
    {
        public float targetedHeatCost = 6f;
        public float ambientHeatCost = 8f;
        public int ambientIntervalTicks = 600;
        public float minimumContestChance = 0.05f;
        public float maximumContestChance = 0.95f;
        public float basePsylinkPower = 1f;

        public List<string> alwaysHarmfulVanillaAbilities = new List<string>();
        public List<string> internallyEnumeratedVanillaAbilities = new List<string>();
        public List<string> alwaysHarmfulVefAbilities = new List<string>();
        public List<string> ignoredVefAbilities = new List<string>();
    }
}
