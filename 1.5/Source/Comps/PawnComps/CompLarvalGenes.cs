using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

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

        public float LeapRange => Props.leapRange;
        CompLarvalGenesProperties Props => props as CompLarvalGenesProperties;
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref father, "father", saveDestroyedThings: true);
            Scribe_References.Look(ref mother, "mother", saveDestroyedThings: true);
            Scribe_Values.Look(ref latched, "latched", defaultValue: false);
            Scribe_Values.Look(ref spent, "spent", defaultValue: false);
            Scribe_Values.Look(ref hoursSpent, "hoursSpent", defaultValue: 0);
        }
        public override void CompTick()
        {
            base.CompTick();

            if (latched)
            {
               
                if (Parent != null)
                {
                    Parent.jobs.StopAll();
                    Parent.DeSpawn(DestroyMode.Vanish);
                }
            }
            else if (spent)
            {

                if (Parent.IsHashIntervalTick(2500))
                {
                    Decay();
                }
            }
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn myPawn)
        {
            if (Parent.Faction == null || !Parent.Faction.IsPlayer)
            {
                yield break;
            }

            if (!XMTUtility.IsXenomorph(myPawn))
            {
                yield break;
            }

            TargetingParameters ImplantParameters = TargetingParameters.ForPawns();

            ImplantParameters.validator = delegate (TargetInfo target)
            {
                if (target.Thing == Parent)
                {
                    return false;
                }
                return XMTUtility.IsHost(target.Thing);
            };

            FloatMenuOption ImplantOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Implant", delegate
            {
                Find.Targeter.BeginTargeting(ImplantParameters, delegate (LocalTargetInfo target)
                {
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.ImplantHunt, target);
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);

                });

            }, priority: MenuOptionPriority.Default), myPawn, parent);

            yield return ImplantOption;
        }

        protected void Decay()
        {
            if(Parent.Dead)
            {
                return;
            }

            hoursSpent++;
            if (hoursSpent >= Props.hoursTilDeathAfterImplant)
            {
                Parent.Kill(new DamageInfo(ExternalDefOf.Decayed, 1,999));
            }
              
        }
        public void TryEmbrace(Pawn target)
        {

            if (target == null)
            {
                return;
            }

            if(target.health.hediffSet.HasHediff(Props.larvaHediff))
            {
                return;
            }

            if (target.Faction != null && !target.Faction.IsPlayer)
            {
                int goodWill = -25;

                if(ModsConfig.IdeologyActive)
                {
                    goodWill += target.Faction.ideos.PrimaryIdeo.HasPrecept(XenoPreceptDefOf.XMT_Parasite_Reincarnation) ? 30 : 0;
                    goodWill += target.Faction.ideos.PrimaryIdeo.HasPrecept(XenoPreceptDefOf.XMT_Biomorph_Study) ? 15 : 0;
                    goodWill += target.Faction.ideos.PrimaryIdeo.HasPrecept(XenoPreceptDefOf.XMT_Biomorph_Worship) ? 20 : 0;
                }

                if (Parent.Faction != null)
                {
                    //Blame the source of the thing.

                    target.Faction.TryAffectGoodwillWith(Parent.Faction, goodWill, reason: XenoPreceptDefOf.XMT_Parasite_Attached);
                }
                else
                {
                    //Blame the Player

                    target.Faction.TryAffectGoodwillWith(Faction.OfPlayer, goodWill, reason: XenoPreceptDefOf.XMT_Parasite_Attached);
                }

                target.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee);
            }

            XMTUtility.WitnessLarva(Parent.PositionHeld, Parent.MapHeld, 0.5f);

            if (!XMTUtility.IsTargetImmobile(target) && !Parent.IsPsychologicallyInvisible())
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

            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Parasite_Attached, target.Named(HistoryEventArgsNames.Doer), parent.Named(HistoryEventArgsNames.Victim)), true);

            XMTUtility.GiveMemory(target, HorrorMoodDefOf.ParasiteLatchedMood);

            IEnumerable <BodyPartRecord> source = from x in target.health.hediffSet.GetNotMissingParts()
                                                 where
                                                    XMTUtility.IsPartHead(x)
                                                 select x;

            Hediff hediff = HediffMaker.MakeHediff(Props.larvaHediff, target, source.First());

            HediffComp_LarvalAttachment LarvalAttachment = hediff.TryGetComp<HediffComp_LarvalAttachment>();

            if ( LarvalAttachment != null && !latched)
            {
               
                Pawn pawn = (parent as Pawn);
                latched = true;
                LarvalAttachment.mother = mother;
                LarvalAttachment.father = father;
                LarvalAttachment.genes = genes;
                LarvalAttachment.kind = pawn.kindDef;
                LarvalAttachment.name = pawn.Name?.ToString();
                LarvalAttachment.age = pawn.ageTracker.AgeBiologicalYearsFloat;

            }

            target.health.AddHediff(hediff, source.First());
        }

        public bool TryResist(Pawn target)
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
                XMTUtility.DamageApparelByBodyPart(target, BodyPartGroupDefOf.FullHead, 160f);
                return target.apparel.BodyPartGroupIsCovered(BodyPartGroupDefOf.FullHead);
            }
            return false;
        }
    }
    public class CompLarvalGenesProperties : CompProperties
    {
        public HediffDef larvaHediff;
        public float leapRange;
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
