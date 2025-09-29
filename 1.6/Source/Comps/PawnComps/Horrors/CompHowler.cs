using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Xenomorphtype
{
    internal class CompHowler : ThingComp
    {
        CompHowlerProperties Props => props as CompHowlerProperties;
        int nextCryTick = -1;

        Pawn Parent => parent as Pawn;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextCryTick, "nextCryTick", -1);
        }
        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (Parent != null)
            {
                IncidentParms parms = new IncidentParms();
                parms.forced = true;
                parms.target = prevMap;
                if(IncidentDefOf.ManhunterPack.Worker.TryExecute(parms))
                {
                    if (ModsConfig.RoyaltyActive)
                    {
                        FleckMaker.Static(Parent.Position, prevMap, FleckDefOf.PsycastAreaEffect, 10f);
                    }
                }
            }
        }
        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
            {
                return;
            }
            if (Parent != null)
            {
                if (Parent.Awake())
                {
                    int currentTick = Find.TickManager.TicksGame;
                    if (Find.TickManager.TicksGame > nextCryTick)
                    {
                        nextCryTick = currentTick + Mathf.CeilToInt(Props.howlIntervalHours * 2500);

                        IEnumerable<Pawn> listeners = GenRadial.RadialDistinctThingsAround(Parent.Position, Parent.Map, Props.howlRadius, true).OfType<Pawn>();
                        if (Props.howlSound != null)
                        {
                            Props.howlSound.PlayOneShot(new TargetInfo(Parent));
                        }

                        if (ModsConfig.RoyaltyActive)
                        {
                            FleckMaker.Static(Parent.Position, Parent.Map, FleckDefOf.PsycastAreaEffect, 5f);
                        }

                        if (!listeners.Any())
                        {
                            return;
                        }
                        foreach (Pawn victim in listeners)
                        { 
                            
                            if(victim == null)
                            {
                                continue;
                            }

                            if (victim.Downed)
                            {
                                continue;
                            }

                            if (victim?.health?.capacities != null)
                            {
                                if (!victim.health.capacities.CapableOf(PawnCapacityDefOf.Hearing))
                                {
                                    continue;
                                }
                            }

                            if (!victim.Awake())
                            {
                                XMTUtility.GiveMemory(victim, ThoughtDefOf.SleepDisturbed);
                            }

                            if(victim.NonHumanlikeOrWildMan())
                            {
                                CompHowler howler = victim.GetComp<CompHowler>();
                                if (howler != null)
                                {
                                    continue;
                                }

                                if (Rand.Chance(0.5f))
                                {
                                    victim.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter, "terrible howling", forced: true, forceWake: true, false);
                                }
                                else
                                {
                                    victim.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee, "terrible howling", forced: true, forceWake: true, false);
                                }
                                
                            }
                        }

                    }
                }
            }
        }
    }

    public class CompHowlerProperties : CompProperties
    {
        public float howlIntervalHours = 8;
        public float howlRadius = 5;
        public SoundDef howlSound;
        public CompHowlerProperties()
        {
            this.compClass = typeof(CompHowler);
        }
        public CompHowlerProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
