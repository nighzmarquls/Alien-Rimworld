using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    public class MutationSurgeryExtension : DefModExtension
    {
        public ThingDef harvestedPart;
        public float completeSeverity = 1f;
        public bool amputate;
        public bool neurological;
        public int meatYield = 2;
        public bool releasesAcid;
        public int scatteredCuts;
    }
}
