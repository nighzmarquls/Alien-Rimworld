using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static UnityEngine.GraphicsBuffer;
using Verse.Noise;
using Verse.AI;
using AlienRace;
using UnityEngine.SceneManagement;

namespace Xenomorphtype
{
    public class HediffComp_BuildingMorphing : HediffComp_SeverityModifierBase
    {
        Pawn instigator;
        public Pawn Instigator => instigator;
        public Pawn Host;
        bool appliedBodyType = false;
        bool unMorphed = true;
        HediffCompProperties_BuildingMorphing Props => props as HediffCompProperties_BuildingMorphing;

        Thing spawnedThing = null;

        public override void CompExposeData()
        {
            base.CompExposeData();

            Scribe_References.Look(ref instigator, "instigator", saveDestroyedThings: false);
            Scribe_Values.Look(ref unMorphed, "unMorphed", defaultValue: true);
            Scribe_Values.Look(ref appliedBodyType, "appliedBodyType", defaultValue: false);
            Scribe_References.Look(ref Host, "mother", saveDestroyedThings: false);

        }

        public override void CompPostMake()
        {
            base.CompPostMake();
            if (parent != null)
            {
                if (Props.MorphedBuilding != null)
                {
                    XMTHiveUtility.RemoveHost(parent.pawn, parent.pawn.MapHeld);
                    if (Props.MorphedBuilding == XenoBuildingDefOf.XMT_Ovomorph)
                    {
                        XMTHiveUtility.AddOvomorphing(parent.pawn, parent.pawn.MapHeld);
                        Host = parent.pawn;
                    }
                }
            }
        }
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (Host != null)
            {
                XMTHiveUtility.RemoveOvomorphing(Host, Host.MapHeld);
                Host = null;
            }
        }

        public override void CompTended(float quality, float maxQuality, int batchPosition = 0)
        {
            base.CompTended(quality, maxQuality, batchPosition);

            bool XenomorphTending = XMTUtility.WitnessHorror(parent.pawn.PositionHeld, parent.pawn.MapHeld, 0.1f, 1f);

            if (XenomorphTending)
            {
                //xenomorphs just make it worse.
                parent.Severity += 0.1f;
            }

            base.Pawn.health.Notify_HediffChanged(parent);
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            if(parent.Severity >= (Props.TriggerSeverity) && unMorphed)
            {
                TryMorphBuilding();
            }
            else if(Pawn.IsHashIntervalTick(2500) && parent.Severity > Props.ApparantMorphingSeverity)
            {
                XMTUtility.GiveMemory(Pawn, HorrorMoodDefOf.OvomorphedAdvancedMood);
                FilthMaker.TryMakeFilth(Pawn.Position, Pawn.Map, InternalDefOf.Starbeast_Filth_Resin);
                
                if(Props.forcedBodyType != null)
                {
                    if (!appliedBodyType)
                    {
                        //Log.Message(Pawn + " changing body type to " + Props.forcedBodyType);
                        ThingDef_AlienRace alienDef = Pawn.def as ThingDef_AlienRace;
                        if (alienDef != null)
                        {
                            if (alienDef.alienRace != null)
                            {
                                if (alienDef.alienRace.generalSettings.alienPartGenerator.bodyTypes.Contains(Props.forcedBodyType))
                                {
                                    Pawn.story.bodyType = Props.forcedBodyType;
                                }
                            }
                            else
                            {
                                if (Pawn.story != null)
                                {
                                    Pawn.story.bodyType = Props.forcedBodyType;
                                }
                            }
                        }
                        else
                        {
                            if (Pawn.story != null)
                            {
                                Pawn.story.bodyType = Props.forcedBodyType;
                            }
                        }
                    }
                }
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            Log.Message(parent.pawn + " has died with " + parent);

            if (parent.Severity >= Props.DeathTriggerSeverity && unMorphed)
            {
                Log.Message(parent.pawn + " is triggering morphing anyway");
                TryMorphBuilding();
            }
        }

        public void InheritFromInjector(Pawn injector)
        {
            instigator = injector;
        }
        private void TryMorphBuilding()
        {
            unMorphed = false;
            Pawn target = parent.pawn;

            ThingDef newThingDef = Props.MorphedBuilding;
            if (newThingDef == null)
            {
                Log.Warning("No MorphedBuilding Def in HediffComp_BuildingMorphing");
                return;
            }

            XMTUtility.TransformPawnIntoThing(target, newThingDef, out spawnedThing, instigator);
            if(spawnedThing != null)
            {
                FilthMaker.TryMakeFilth(spawnedThing.Position, spawnedThing.Map, InternalDefOf.Starbeast_Filth_Resin,count: 10);
            }
        }

        public override float SeverityChangePerDay()
        {
            return Props.SeverityPerDay;
        }
    }


    public class HediffCompProperties_BuildingMorphing : HediffCompProperties
    {
        public float TriggerSeverity = 1;
        public float DeathTriggerSeverity = 1;
        public float SeverityPerDay = 1;
        public ThingDef MorphedBuilding;

        public BodyTypeDef forcedBodyType = null;
        public float ApparantMorphingSeverity = 0.5f;
        public HediffCompProperties_BuildingMorphing()
        {
            compClass = typeof(HediffComp_BuildingMorphing);
        }
    }
}
