using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;
using Verse;


namespace Xenomorphtype
{
    public class HediffComp_MatureToAdult : HediffComp_SeverityModifierBase
    {
        HediffCompProperties_MatureToAdult Props => props as HediffCompProperties_MatureToAdult;
        int     tickInterval = 2500;
        bool    fullyMatured = false;
        bool    attacked = false;
        int     matureTick = -1;
        public override bool CompShouldRemove
        {
            get
            {
                if (base.CompShouldRemove)
                {
                    return true;
                }
                return fullyMatured;
            }
        }

        GeneSet _genes = null;
        GeneSet Genes
        {
            get
            {
                if (_genes == null)
                {
                    _genes = new GeneSet();

                    BioUtility.ExtractCryptimorphGenesToGeneset(ref _genes, Pawn.genes.GenesListForReading);
                }
                return _genes;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref matureTick, "matureTick", defaultValue: -1);
            Scribe_Values.Look(ref attacked, "attacked", defaultValue: false);
            Scribe_Values.Look(ref fullyMatured, "fullyMatured", defaultValue: false);
        }
        public override void CompPostMake()
        {
            base.CompPostMake();
            tickInterval = Mathf.CeilToInt(Props.learningIntervalHours * 2500);
        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            int tick = Find.TickManager.TicksGame;
            if (parent.pawn.IsHashIntervalTick(tickInterval))
            {
                TryLearning();
                FilthMaker.TryMakeFilth(Pawn.PositionHeld, Pawn.MapHeld, InternalDefOf.Starbeast_Filth_Resin);
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message(parent + " will mature in " + (matureTick - tick));
                }
            }

           
            if (matureTick <= 0)
            {
                float ticksToMature = 60000/GeneMaturationFactor();
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message(parent + " will mature in " + ticksToMature);
                }
                matureTick = tick + Mathf.FloorToInt(ticksToMature);
            }

            if (parent.Severity >= 1.0f || parent.pawn.ageTracker.Adult || tick > matureTick)
            {
                TryMatureNow();
            }
        }
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            attacked = true;
            parent.Severity += 0.25f;
        }

        private void TryLearning()
        {
            if (parent.pawn.Faction != null && parent.pawn.Faction.IsPlayer)
            {
                if (parent.pawn.skills != null && parent.pawn.skills.skills.Where((SkillRecord x) => !x.TotallyDisabled).TryRandomElement(out var result))
                {
                    result.Learn(8000f* GeneMaturationFactor(), direct: true);
                    parent.pawn.needs.learning.Learn(1);
                    parent.pawn.ageTracker.growthPoints += 5.0f* GeneMaturationFactor();
                }
            }
        }
        private void TryMatureNow()
        {
            parent.pawn.health.hediffSet.hediffs.RemoveWhere(x => x.def == InternalDefOf.Undeveloped || x.def == InternalDefOf.Overdeveloped);
            float ageTarget = 0;

            for (int i = 0; i < InternalDefOf.XMT_Starbeast_AlienRace.race.lifeStageAges.Count; i++)
            {
                LifeStageAge lifeStageAge = InternalDefOf.XMT_Starbeast_AlienRace.race.lifeStageAges[i];
                if (lifeStageAge.def.developmentalStage == DevelopmentalStage.Adult)
                {
                    ageTarget = lifeStageAge.minAge;
                    break;
                }
            }

            if (parent.pawn.ageTracker.AgeBiologicalYearsFloat < ageTarget)
            {
                while (parent.pawn.ageTracker.AgeBiologicalYearsFloat < ageTarget)
                {
                    parent.pawn.ageTracker.AgeTickMothballed(3600000);
                    FilthMaker.TryMakeFilth(Pawn.Position, Pawn.Map, InternalDefOf.Starbeast_Filth_Resin);
                }
            }


            fullyMatured = true;

            if (attacked)
            {
                Pawn.health.RemoveHediff(parent);
                Pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, "", forced: true, forceWake: true, false);
            }

            int progress = 250;
            XMTResearch.ProgressEvolutionTech(progress, Pawn);
        }

        protected float GeneMaturationFactor()
        {
            float complexityFactor = (1 / ((Genes.ComplexityTotal + 1) / 9));

            return complexityFactor * XMTSettings.MaturationFactor;
        }

        public override float SeverityChangePerDay()
        {
            return Mathf.Max(0.0166666666666667f, Props.severityPerDay * GeneMaturationFactor());
        }
    }



    public class HediffCompProperties_MatureToAdult : HediffCompProperties_SeverityPerDay
    {
        public float learningIntervalHours = 1;
        public HediffCompProperties_MatureToAdult()
        {
            compClass = typeof(HediffComp_MatureToAdult);
        }
    }
}
