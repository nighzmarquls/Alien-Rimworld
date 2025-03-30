using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype 
{
    public class CompHostHunter : ThingComp
    {
        int nextHuntCheckTick = -1;
        protected CompHostHunterProperties Props => props as CompHostHunterProperties;

        protected Pawn Parent => parent as Pawn;

        public override void PostExposeData()
        {
            base.PostExposeData();

        }

        public virtual bool ShouldHunt()
        {
            return Parent.CurJobDef != XenoWorkDefOf.ImplantHunt && !Parent.Downed;
        }

        public virtual void StartHunt(Pawn prey)
        {
            Job attachJob = JobMaker.MakeJob(XenoWorkDefOf.ImplantHunt, prey);

            Parent.jobs.StartJob(attachJob, JobCondition.InterruptForced);
        }

        public virtual Pawn GetPreyTarget()
        {

            List<Pawn> listOfPawn = parent.Map.mapPawns.AllPawnsSpawned.ToList();
            listOfPawn.Shuffle();
            foreach (Pawn pawn in listOfPawn)
            {
                if (XMTUtility.IsAcidBlooded(pawn))
                {
                    continue;
                }

                if (XMTUtility.IsInorganic(pawn))
                {
                    continue;
                }

                return pawn;
            }
            return null;
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!Parent.Spawned)
            {
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (tick > nextHuntCheckTick)
            {

                nextHuntCheckTick = tick + Mathf.CeilToInt(Props.huntCheckHourInterval * 2500);

                if (ShouldHunt())
                {
                    Pawn prey = GetPreyTarget();

                    if (prey != null)
                    {
                        StartHunt(prey);
                    }
                }

            }
        }

        public virtual void PreattachTarget(Pawn target)
        {

        }

        public virtual bool TryResist(Pawn target)
        {
            if (target.apparel != null)
            {
                if (target.meleeVerbs.TryMeleeAttack(parent))
                {
                    if ((parent as Pawn).Dead)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public virtual List<BodyPartRecord> GetTargetBodyParts(Pawn target)
        {
            return (from x in target.health.hediffSet.GetNotMissingParts()
                                           where x.depth == BodyPartDepth.Outside
                                           select x).ToList();
        }
        public void TryAttachToHost(Pawn target)
        {
            if (target == null)
            {
                return;
            }

            if (target.Faction != null && !target.Faction.IsPlayer)
            {
                if (Parent.Faction != null)
                {
                    //Blame the source of the thing.
                    //TODO: Ideology check if they venerate xenomorphs.
                    target.Faction.TryAffectGoodwillWith(Parent.Faction, -25, reason: HistoryEventDefOf.AttackedMember);
                }
                else
                {
                    //Blame the Player

                    target.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -25, reason: HistoryEventDefOf.AttackedMember);
                }

                target.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee);
            }

            if (XMTUtility.IsXenomorph(parent))
            {
                XMTUtility.WitnessLarva(target.PositionHeld, target.MapHeld, 0.01f, 0.1f);
            }

            if (!XMTUtility.IsTargetImmobile(target))
            {
                if (Rand.Chance(XMTUtility.GetDodgeChance(target, true)))
                {
                    MoteMaker.ThrowText(target.DrawPos, target.Map, "TextMote_Dodge".Translate(), 1.9f);
                    return;
                }
                if (TryResist(target))
                {
                    MoteMaker.ThrowText(target.DrawPos, target.Map, "Resisted".Translate());
                    return;
                }
            }

            if (target.jobs != null)
            {
                target.jobs.StopAll();
            }

            XMTUtility.GiveMemory(target, Props.thoughtGivenOnAttach);

            List<BodyPartRecord> source = GetTargetBodyParts(target);

            source.Shuffle();

            Hediff hediff = HediffMaker.MakeHediff(Props.parasiteHediff, target, source.First());

            hediff.Severity = 0;

            PreattachTarget(target);

            target.health.AddHediff(hediff, source.First(), new DamageInfo(DamageDefOf.Stun, 10, 999, instigator: Parent));
        }
    }

    public class CompHostHunterProperties : CompProperties
    {
        public HediffDef parasiteHediff;
        public float huntCheckHourInterval = 1;
        public ThoughtDef thoughtGivenOnAttach;
        public CompHostHunterProperties()
        {
            this.compClass = typeof(CompHostHunter);
        }

        public CompHostHunterProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
