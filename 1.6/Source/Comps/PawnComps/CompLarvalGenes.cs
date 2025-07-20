using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    public class CompLarvalGenes : CompHiveGeneHolder
    {
        static private Texture2D Implant => ContentFinder<Texture2D>.Get("UI/Abilities/Implant");

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
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if(!XMTUtility.PlayerXenosOnMap(Parent.MapHeld))
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

                if(!XMTUtility.IsHost(target.Thing))
                {
                    return false;
                }

                return target.Map.reachability.CanReach(parent.Position, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly);
            };

            Command_Action ImplantHost_Action = new Command_Action();
            ImplantHost_Action.defaultLabel = "XMT_Implant".Translate();
            ImplantHost_Action.defaultDesc = "XMT_ImplantDescription".Translate();
            ImplantHost_Action.icon = Implant;
            ImplantHost_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(ImplantParameters, delegate (LocalTargetInfo target)
                {
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.ImplantHunt, target);
                    Parent.Reserve(target, job);
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);

                });

            };

            yield return ImplantHost_Action;
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
        public void TryEmbrace(Pawn target)
        {
            if (target == null)
            {
                return;
            }

            if (target.health.hediffSet.HasHediff(Props.larvaHediff))
            {
                return;
            }


            if (target.Faction != null && !target.Faction.IsPlayer)
            {
                int goodWill = -25;

                if (ModsConfig.IdeologyActive)
                {
                    if (target.Faction.ideos != null)
                    {
                        goodWill += target.Faction.ideos.PrimaryIdeo.HasPrecept(XenoPreceptDefOf.XMT_Parasite_Reincarnation) ? 30 : 0;
                        goodWill += target.Faction.ideos.PrimaryIdeo.HasPrecept(XenoPreceptDefOf.XMT_Biomorph_Study) ? 15 : 0;
                        goodWill += target.Faction.ideos.PrimaryIdeo.HasPrecept(XenoPreceptDefOf.XMT_Biomorph_Worship) ? 20 : 0;
                    }
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

                if (target.mindState != null)
                {
                    target.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee);
                }
            }

            XMTUtility.WitnessLarva(Parent.PositionHeld, Parent.MapHeld, 0.5f);

            if (!XMTUtility.IsTargetImmobile(target) && !Parent.IsPsychologicallyInvisible())
            {
                CompPawnInfo info = target.GetComp<CompPawnInfo>();
                float bonusDodge = 0;
                if (info != null)
                {
                    bonusDodge += info.LarvaAwareness / 4;
                }

                if (Rand.Chance(XMTUtility.GetDodgeChance(target, true) + bonusDodge))
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

            IEnumerable<BodyPartRecord> source = from x in target.health.hediffSet.GetNotMissingParts()
                                                 where
                                                    XMTUtility.IsPartHead(x)
                                                 select x;

            if (!source.Any())
            {
                return;
            }

            Hediff hediff = HediffMaker.MakeHediff(Props.larvaHediff, target, source.First());

            HediffComp_LarvalAttachment LarvalAttachment = hediff.TryGetComp<HediffComp_LarvalAttachment>();

            if (LarvalAttachment != null && !latched)
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
            if (target.meleeVerbs.TryMeleeAttack(parent))
            {
                if ((parent as Pawn).Dead)
                {
                    return true;
                }
            }

            if (target.apparel != null)
            {
                XMTUtility.DamageApparelByBodyPart(target, BodyPartGroupDefOf.FullHead, 160f);
                return target.apparel.BodyPartGroupIsCovered(BodyPartGroupDefOf.FullHead);
            }
            return false;
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

                CompPawnInfo info = aggressor.GetComp<CompPawnInfo>();

                if (info != null)
                {
                    info.ApplyThreatPheromone(Parent, radius:10);
                }
            }
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
