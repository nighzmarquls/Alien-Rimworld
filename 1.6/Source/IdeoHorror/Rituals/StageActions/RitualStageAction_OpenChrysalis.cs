using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class RitualStageAction_OpenChrysalis : RitualStageAction
    {
        public override void Apply(LordJob_Ritual ritual)
        {
            FillableChrysalis targetChrysalis = ritual.selectedTarget.Thing as FillableChrysalis;

            if (targetChrysalis != null)
            {
                targetChrysalis.OpenChrysalis();
            }
        }

        public override void ExposeData()
        {
        }
    }
}
