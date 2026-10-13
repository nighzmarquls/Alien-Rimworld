using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    public class CompInfector : ThingComp
    {
        CompInfectorProperties Props => props as CompInfectorProperties;
        Pawn Parent => parent as Pawn;

        public override void CompTick()
        {
            base.CompTick();

            if (GenTicks.IsTickInterval(250) && parent.Spawned)
            {

                IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(parent.PositionHeld, Props.infectionRange, true);

                foreach(IntVec3 cell in cells)
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

                    if(!parent.Spawned)
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

            if(Rand.Chance(target.GetStatValue(StatDefOf.ToxicEnvironmentResistance)))
            {
                return;
            }

            if (!XMTUtility.IsTargetImmobile(target) && !Parent.IsPsychologicallyInvisible())
            {
                if (Rand.Chance(XMTUtility.GetDodgeChance(target, true)))
                {
                    MoteMaker.ThrowText(target.DrawPos, target.Map, "TextMote_Dodge".Translate(), 1.9f);
                    return;
                }
            }


            if(XMTUtility.IsInorganic(target))
            {
                return;
            }

            IEnumerable<BodyPartRecord> source = from x in target.health.hediffSet.GetNotMissingParts()
                                                 where
                                                 x.depth == BodyPartDepth.Outside
                                                 select x;

            Hediff hediff = HediffMaker.MakeHediff(Props.infectionHediff, target, source.RandomElement());

            target.health.AddHediff(hediff, hediff.Part);

            parent.Destroy();

        }
    }

    public class CompInfectorProperties : CompProperties
    {
        public HediffDef infectionHediff;
        public float infectionRange = 1.5f;
        public CompInfectorProperties()
        {
            this.compClass = typeof(CompInfector);
        }

        public CompInfectorProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
