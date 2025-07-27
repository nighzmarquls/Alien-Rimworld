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
    public class CompPerfectOrganism : ThingComp
    {
        CompPerfectOrganismProperties Props => props as CompPerfectOrganismProperties;
        Pawn pawn => parent as Pawn;

        private static readonly FloatRange TendingQualityRange = new FloatRange(0.2f, 0.7f);

        public override void PostExposeData()
        {
            base.PostExposeData();

        }
        public override float GetStatFactor(StatDef stat)
        {
            if (stat == StatDefOf.LeatherAmount)
            {
                if (pawn.ageTracker.Adult)
                {
                    float baseAmount = base.GetStatFactor(stat);

                    if(pawn.story != null)
                    {
                        if (pawn.story.traits.HasTrait(ExternalDefOf.Tough))
                        {
                            baseAmount += baseAmount * 2;
                        }
                    }

                    return baseAmount;
                }
                else
                {
                    return 0;
                }
            }

            if(stat == StatDefOf.Fertility)
            {
                if(pawn.ageTracker.Adult)
                {
                    float baseAmount = base.GetStatFactor(stat);
                    return Mathf.Max( baseAmount, 1.0f );
                }
            }

            return base.GetStatFactor(stat);
        }

        public void RemoveImperfection(Hediff hediff)
        {
            Hediff_Addiction addiction = hediff as Hediff_Addiction;
            if (addiction != null)
            {
                pawn.health.RemoveHediff(hediff);
                return;
            }
            if (Props.ImpossibleHediffs != null && Props.ImpossibleHediffs.Count > 0)
            {
                if (Props.ImpossibleHediffs.Contains(hediff.def))
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }

        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);

            if (pawn != null)
            {
                if (pawn.IsHashIntervalTick(500))
                {
                    List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                        
                    for (int num = hediffs.Count - 1; num >= 0; num--)
                    {
                        if (hediffs[num].Bleeding)
                        {
                            hediffs[num].Tended(TendingQualityRange.RandomInRange, TendingQualityRange.TrueMax, 1);
                        }
                    }
                    
                }
                
                if(pawn.health.hediffSet.HasHediff(InternalDefOf.StarbeastOrganism))
                {
                    return;
                }
                Hediff perfection = HediffMaker.MakeHediff(InternalDefOf.StarbeastOrganism, pawn);
                pawn.health.hediffSet.AddDirect(perfection);
            }
        }
    }

    public class CompPerfectOrganismProperties : CompProperties
    {
        public List<HediffDef> ImpossibleHediffs = new List<HediffDef>();
        public CompPerfectOrganismProperties()
        {
            this.compClass = typeof(CompPerfectOrganism);
        }

        public CompPerfectOrganismProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
