using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class CompDiseaseCarrier : ThingComp
    {
        CompDiseaseCarrierProperties Props => props as CompDiseaseCarrierProperties;
        public override void CompTick()
        {
            base.CompTick();

            if (GenTicks.IsTickInterval(Props.tickInterval) && parent.Spawned)
            {

                IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(parent.PositionHeld, Props.infectionRange, true);

                foreach (IntVec3 cell in cells)
                {
                    Pawn pawn = cell.GetFirstPawn(parent.MapHeld);
                    if (pawn == null)
                    {
                        continue;
                    }
                    if (pawn == parent)
                    {
                        continue;
                    }

                    if (!parent.Spawned)
                    {
                        break;
                    }

                    TryInfecting(pawn);
                }
            }
        }
        public void TryInfecting(Pawn target)
        {
            if (target == null)
            {
                return;
            }

            if(XMTUtility.IsXenomorph(target))
            {
                return;
            }

            if (Rand.Chance(target.GetStatValue(StatDefOf.ToxicEnvironmentResistance)))
            {
                return;
            }

            if (XMTUtility.IsInorganic(target))
            {
                return;
            }

            Hediff hediff = HediffMaker.MakeHediff(Props.infectionHediff, target);

            target.health.AddHediff(hediff, hediff.Part);
        }
    }

    public class CompDiseaseCarrierProperties : CompProperties
    {
        public HediffDef infectionHediff;
        public float infectionRange = 1.5f;
        public int tickInterval = 250;
        public CompDiseaseCarrierProperties()
        {
            this.compClass = typeof(CompDiseaseCarrier);
        }

        public CompDiseaseCarrierProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
