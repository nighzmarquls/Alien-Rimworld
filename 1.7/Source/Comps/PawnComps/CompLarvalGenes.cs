using RimWorld;
using System;
using Verse;

namespace Xenomorphtype
{
    public class CompLarvalGenes : CompHiveGeneHolder
    {

        public Pawn mother;
        public Pawn father;
        public bool latched;
        public bool spent;
        int hoursSpent = 0;

        Pawn Parent => parent as Pawn;

        CompLarvalGenesProperties Props => props as CompLarvalGenesProperties;
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref father, "father", saveDestroyedThings: false);
            Scribe_References.Look(ref mother, "mother", saveDestroyedThings: false);
            Scribe_Values.Look(ref latched, "latched", defaultValue: false);
            Scribe_Values.Look(ref spent, "spent", defaultValue: false);
            Scribe_Values.Look(ref hoursSpent, "hoursSpent", defaultValue: 0);
        }
        public override void CompTick()
        {
            base.CompTick();

            if (!parent.Spawned || latched)
            {
                return;
            }

            if (spent)
            {

                if (Parent.IsHashIntervalTick(2500))
                {
                    Decay();
                }
            }

        }

        protected void Decay()
        {
            if (Parent.Dead)
            {
                return;
            }

            hoursSpent++;
            if (hoursSpent >= Props.hoursTilDeathAfterImplant)
            {
                Parent.Kill(new DamageInfo(ExternalDefOf.Decayed, 1, 999));
            }

        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

            Pawn aggressor = dinfo.Instigator as Pawn;

            if (Parent.Downed)
            {
                return;
            }

            if (aggressor != null)
            {
                if (aggressor.Dead)
                {
                    return;
                }

                if (aggressor == Parent)
                {
                    return;
                }

                if (XMTUtility.IsXenomorph(aggressor))
                {
                    return;
                }

                CompPawnInfo info = aggressor.Info();

                if (info != null)
                {
                    info.ApplyThreatPheromone(Parent, radius:10);
                }
            }
        }
    }
    public class CompLarvalGenesProperties : CompProperties
    {
        public int hoursTilDeathAfterImplant;
        public CompLarvalGenesProperties()
        {
            this.compClass = typeof(CompLarvalGenes);
        }

        public CompLarvalGenesProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
