using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class XMT_Hivecast : Psycast
    {
        public XMT_Hivecast(Pawn pawn) : base(pawn)
        {
        }
        public XMT_Hivecast(Pawn pawn, AbilityDef def) : base(pawn, def)
        {
            if(XMTUtility.IsXenomorph(pawn))
            {
                return;
            }

            if(def.level > 0)
            {
                float obsession = def.level;

                obsession *= 0.1f;

                CompPawnInfo info = pawn.Info();

                if (info != null)
                {
                    info.WitnessPsychicHorror(obsession);
                    info.GainObsession(obsession);
                }
            }
        }
    }
}
