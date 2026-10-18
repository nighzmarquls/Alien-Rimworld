using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class AcidPit : XMTBase_Building, IAttackTarget
    {
        private CompAcidPit acidPit;
        private Graphic fullGraphic;
        private Graphic fillingGraphic;
        Thing IAttackTarget.Thing => this;
        public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;
        public float TargetPriorityFactor => (acidPit ??= GetComp<CompAcidPit>()).Props.targetPriorityFactor;

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor)
        {
            // A temporarily depleted pool remains a defensive threat while its acid regrows.
            return !Spawned || Destroyed;
        }
        public override Graphic Graphic
        {
            get
            {
                acidPit ??= GetComp<CompAcidPit>();
                int stage = acidPit?.GraphicStage ?? 0;
                if (stage == 0)
                    return base.Graphic;
                // Keep the insertion's visual level until output is released; partial pools need not appear full.
                if (stage == 2)
                    return fullGraphic ??= StateGraphic("_full");
                return fillingGraphic ??= StateGraphic("_filling");
            }
        }

        private Graphic StateGraphic(string suffix)
        {
            var data = new GraphicData();
            data.CopyFrom(def.graphicData);
            data.texPath += suffix;
            return data.GraphicColoredFor(this);
        }

        public override void TransformedFrom(Pawn pawn, Pawn instigator)
        {
        }
    }
}
