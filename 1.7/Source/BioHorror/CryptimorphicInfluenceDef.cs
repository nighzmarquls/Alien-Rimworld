using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    public class CryptimorphicInfluenceDef : Def
    {
        public float perfectOrganismOffset = 1f;
        public float matureMorphInfluence = 4f;
        public float maternalLifecycleThreshold = 2.2f;
        public float normalDepositMl = 20f;
        public float normalDepositInfluence = 0.1f;
        public static CryptimorphicInfluenceDef Current => DefDatabase<CryptimorphicInfluenceDef>.GetNamed("XMT_CryptimorphicInfluence");
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (perfectOrganismOffset <= 0 || matureMorphInfluence <= 0 || normalDepositMl <= 0 || normalDepositInfluence < 0)
                yield return "Cryptimorphic influence reference values must be positive.";
        }
    }
}
