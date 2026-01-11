using AlienRace;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static UnityEngine.GraphicsBuffer;


namespace Xenomorphtype
{
    public class CompMatureMorph : ThingComp
    {
        static private Texture2D ReleaseTexture => ContentFinder<Texture2D>.Get("UI/Designators/ReleasePioneer");
        public static Color nymphSkinColor = new Color(1, 0.85f, 0.65f);
        public IntVec3 NestPosition => XMTHiveUtility.GetNestPosition(Parent.Map);
        public bool NeedEggs => XMTHiveUtility.NeedEggs(Parent.Map);

        CompJellyMaker JellyMaker = null;
        CompMatureMorphProperties Props => props as CompMatureMorphProperties;
        Pawn Parent => parent as Pawn;
        public float abductRange => Props.abductRange;
        
        int canNuzzleTick = 0;
        int canAbductTick = 0;
        int canOvomorphTick = 0;
        int canMatureTick = 0;
        int canMischiefTick = 0;
        int canHuntTick = 0;
        int canTendLairTick = 0;
        int canTunnelTick = 0;

        //Taming mechanics

        public bool ShouldTameSocial = false;
        public bool ShouldTameCondition = false;
        public bool ShouldTamePheromone = false;
        public bool ShouldTameBribe = false;
        public bool ShouldTameHostage = false;

        public bool AnyTamingDesired => !Tamed && (ShouldTameSocial || ShouldTameCondition || ShouldTamePheromone || ShouldTameBribe || ShouldTameHostage);
        public bool Tamed
        {
            get
            {
                return Taming > 0.5f;
            }
        }

        public bool Integrated
        {
            get
            {

                if(XMTUtility.QueenIsPlayer())
                {
                    return true;
                }

                if(Faction.OfPlayer.def.defName == InternalDefOf.XMT_PlayerHive.defName)
                {
                    return true;
                }

                return Loyalty >= 1f;
            }
        }

        public float Loyalty
        {
            get
            {
                return (tamingSocializing + tamingPheromones) - (tamingHostage + tamingConditioning);
            }
        }
        public float Taming
        {
            get
            {
                return tamingSocializing + tamingConditioning + tamingPheromones + tamingBribes + tamingHostage;
            }
        }
        public float tamingSocializing = 0;
        public float tamingConditioning = 0;
        public float tamingPheromones = 0;
        public float tamingBribes = 0;
        public float tamingHostage = 0;

        //taming mechanics done
        int IntervalCheck => Mathf.CeilToInt(Props.IntervalHours * 2500);

        int DailyCheck => 60000;

        bool lairUnloaded = true;

        bool destroyNextTick = false;
        DestroyMode delayedDestroyMode = DestroyMode.Vanish;

        int destroyTicks = 5;

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref ShouldTameSocial, "ShouldTameSocial", false);
            Scribe_Values.Look(ref tamingSocializing, "tamingSocializing", 0);
            Scribe_Values.Look(ref ShouldTameCondition, "ShouldTameCondition", false);
            Scribe_Values.Look(ref tamingConditioning, "tamingConditioning", 0);
            Scribe_Values.Look(ref ShouldTamePheromone, "ShouldTamePheromone", false);
            Scribe_Values.Look(ref tamingPheromones, "tamingPheromones", 0);
            Scribe_Values.Look(ref ShouldTameBribe, "ShouldTameBribe", false);
            Scribe_Values.Look(ref tamingBribes, "tamingBribes", 0);
            Scribe_Values.Look(ref ShouldTameHostage, "ShouldTameHostage", false);
            Scribe_Values.Look(ref tamingHostage, "tamingHostage", 0);

            Scribe_Values.Look(ref destroyNextTick, "destroyNextTick", false);
            Scribe_Values.Look(ref delayedDestroyMode, "delayedDestroyMode", DestroyMode.Vanish);

            Scribe_Values.Look(ref canNuzzleTick, "canNuzzleTick", 0);
            Scribe_Values.Look(ref canAbductTick, "canAbductTick", 0);
            Scribe_Values.Look(ref canOvomorphTick, "canOvomorphTick", 0);

            Scribe_Values.Look(ref canMatureTick, "canMatureTick", 0);

            Scribe_Values.Look(ref canMischiefTick, "canMischiefTick", 0);

            Scribe_Values.Look(ref canHuntTick, "canHuntTick", 0);

            Scribe_Values.Look(ref canTendLairTick, "canTendLairTick", 0);
            Scribe_Values.Look(ref canTunnelTick, "canTunnelTick", 0);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Parent.Downed)
            {
                yield break;
            }

            if (Parent.Faction == null || !Parent.Faction.IsPlayer)
            {
                yield break;
            }

            if (!Parent.ageTracker.Adult)
            {
                yield break;
            }

            if (!XMTUtility.QueenIsPlayer())
            {
                yield break;
            }

            if (XMTUtility.IsQueen(Parent))
            {
                yield break;
            }

           

            Command_Action releaseAction = new Command_Action();
            releaseAction.defaultLabel = "XMT_ReleasePioneer".Translate();
            releaseAction.defaultDesc = "XMT_ReleasePioneerDescription".Translate();
            releaseAction.icon = ReleaseTexture;
            releaseAction.action = delegate
            {
                PawnBanishUtility.Banish(Parent, false);
                Parent.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee, "", forced: true, forceWake: true, false);
            };

            yield return releaseAction; 
        }

        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            if (Parent == null)
            {
                return;
            }

            if(Parent.IsHashIntervalTick(DailyCheck))
            {
                if (!XMTUtility.IsQueen(Parent))
                {
                    if (Parent.ageTracker.Adult)
                    {
                        if (Rand.Chance(Props.slowDownDailyChance))
                        {
                            Hediff slowDown = Parent.health.GetOrAddHediff(InternalDefOf.XMT_Slowdown);
                        }
                    }
                }
            }

            if (Parent.Spawned)
            {
                if (destroyNextTick)
                {
                    if (destroyTicks <= 0)
                    {
                        Parent.ClearAllReservations();
                        Parent.jobs.ClearDriver();
                        Parent.Destroy(delayedDestroyMode);
                        return;
                    }
                    else
                    {
                        destroyTicks -= delta;
                    }
                }

                if (lairUnloaded)
                {
                    SeekNewLair();
                    lairUnloaded = false;
                }
            }

            if(Parent.Downed)
            {
                return;
            }

            if(Parent.MapHeld == null)
            {
                return;
            }

            if (Parent.IsHashIntervalTick(IntervalCheck))
            {
                if (Parent.CarriedBy != null)
                {
                    if (!XMTUtility.IsXenomorph(Parent.CarriedBy))
                    {
                        Thing something;
                        Pawn other = Parent.CarriedBy;

                        if(!XMTUtility.IsXenomorphFriendly(other))
                        {
                            Parent.CarriedBy.carryTracker.TryDropCarriedThing(Parent.PositionHeld, ThingPlaceMode.Near, out something);
                            Parent.interactions.StartSocialFight(other);
                        }
                    }
                }

                if (Parent.guest.IsPrisoner)
                {
                    if (!XMTHiveUtility.PlayerXenosOnMap(Parent.MapHeld))
                    {
                        if (Taming > 0)
                        {
                            if (Rand.Chance(1 - Taming))
                            {
                                Parent.guest.SetGuestStatus(null);
                                Parent.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                            }
                        }
                    }
                }
                else
                {
                    if(Tamed && !Integrated)
                    {
                        if(!TamingUtility.CanTameConditioning(Parent))
                        {
                            tamingConditioning = 0;
                        }
                        else
                        {
                            tamingConditioning -= 0.01f;
                        }

                        if (!TamingUtility.CanTamePheromones(Parent))
                        {
                            tamingPheromones = 0;
                        }

                        if (!TamingUtility.CanTameThreat(Parent))
                        {
                            tamingHostage = 0;
                        }

                        if (Parent.needs != null && Parent.needs.food != null)
                        {
                            if(Parent.needs.food.Starving)
                            {
                                tamingBribes = 0;
                            }
                        }

                        if (!Tamed)
                        {
                            Parent.SetFaction(null);
                            Parent.guest.SetGuestStatus(null);
                        }
                    }
                }
            }

        }

        public void UpdateSkinByAge()
        {
            if (Parent.ageTracker.Adult && Parent.story.SkinColor == nymphSkinColor)
            {
                if (Parent.def is ThingDef_AlienRace alien)
                {
                    if (alien.alienRace.generalSettings.alienPartGenerator.colorChannels[0].entries[0].first is ColorGenerator_Options Options)
                    {
                        Parent.story.skinColorOverride = Options.NewRandomizedColor();
                    }
                }

                if (Parent.genes != null)
                {
                    foreach(Gene gene in Parent.genes.GenesListForReading)
                    {
                        if (!gene.Active)
                        {
                            continue;
                        }
                        if(gene.def.skinColorOverride != null)
                        {
                            Log.Message(Parent + " has active gene for skincolor " + gene.def);
                            Parent.story.skinColorOverride = gene.def.skinColorOverride;
                        }
                    }
                }
                
               
               
                return;
            }
            else if(!Parent.ageTracker.Adult && Parent.story.skinColorOverride != nymphSkinColor)
            {
                
                Parent.story.skinColorOverride = nymphSkinColor;
            }
        }

        public override float GetStatOffset(StatDef stat)
        {
            if (Parent.ageTracker.Adult)
            {
                return 0;
            }

            if(stat == StatDefOf.Flammability)
            {
                return 2;
            }

            return 0;
        }
        public override float GetStatFactor(StatDef stat)
        {
            if (stat == StatDefOf.MinimumContainmentStrength)
            {
                return Mathf.Max(0 , 1 + tamingConditioning + (tamingHostage*2)) - (tamingSocializing + tamingPheromones + (tamingBribes*0.5f));
            }

            if (Parent.ageTracker.Adult)
            {
                return 1;
            }

            if(stat == StatDefOf.IncomingDamageFactor)
            {
                return 2;
            }
            if(stat == StatDefOf.MeleeDamageFactor)
            {
                return 0.25f;
            }

            return 1;
        }
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

            Pawn aggressor = dinfo.Instigator as Pawn;

            if (Parent.Downed || Parent.Dead)
            {
                return;
            }

            if (aggressor == null && dinfo.Instigator != null)
            {
                if (Parent.Faction == null)
                {
                    if (Parent.mindState.mentalStateHandler.InMentalState)
                    {
                        return;
                    }
                    Parent.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage,reason: "", forced: true, forceWake: true, false);
                }
                return;
            }

            if (aggressor.Dead)
            {
                return;
            }

            if (aggressor == Parent)
            {
                return;
            }

            if (!Parent.ageTracker.Adult)
            {
                return;
            }

            if (XMTUtility.IsXenomorph(aggressor))
            {
                if (Parent.mindState.mentalStateHandler.InMentalState && Parent.mindState.mentalStateHandler.CurStateDef != MentalStateDefOf.SocialFighting)
                {
                    Parent.mindState.mentalStateHandler.Reset();
                }
                if(Parent.mindState.mentalStateHandler.CurStateDef == MentalStateDefOf.SocialFighting)
                {
                    return;
                }
                Parent.interactions.StartSocialFight(aggressor);
                return;
            }

            if (XMTUtility.NotPrey(aggressor))
            {
                return;
            }

            CompPawnInfo info = aggressor.Info();

            if (info != null)
            {
                info.ApplyThreatPheromone(Parent);
            }

            if (Parent.HostileTo(aggressor))
            {
                return;
            }
        }

        public bool ShouldTendNest()
        {
            if (Find.TickManager.TicksGame < canTendLairTick)
            {
                return false;
            }

            if (!Parent.DevelopmentalStage.Adult())
            {
                return false;
            }

            bool HiveNeedsTending = XMTHiveUtility.ShouldTendNest(Parent.MapHeld) || XMTHiveUtility.ShouldBuildNest(Parent.MapHeld);

            if (Parent.Faction == null)
            {
                if (HiveNeedsTending)
                {
                    canTendLairTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 5000);
                    return true;
                }
                return false;
            }

            if (Parent.needs.joy.tolerances.BoredOf(InternalDefOf.NestTending))
            {
                if (HiveNeedsTending)
                {
                    int hiveCount = XMTHiveUtility.TotalHivePopulation(parent.Map);
                    int stackLimit = HorrorMoodDefOf.TooMuchNestWork.stackLimit;
                    int maxOverwork = stackLimit - hiveCount;

                    if (XMTUtility.QueenPresent())
                    {
                        maxOverwork -= 4;
                    }

                    if (maxOverwork > 0)
                    {
                        XMTUtility.GiveMemory(Parent, HorrorMoodDefOf.TooMuchNestWork, maxOverwork);
                    }
                }
                else
                {
                    return false;
                }
            }
            canTendLairTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
            return true;

        }

        public bool ShouldBeInNest()
        {
            if (Parent.Faction != null && Parent.Faction.IsPlayer)
            {
                return false;
            }

            if (XMTHiveUtility.NoNestOnMap(Parent.Map))
            {
                return false;
            }

            if (parent.MapHeld.skyManager.CurSkyGlow > 0.45f)
            {
                float brightness = Parent.MapHeld.glowGrid.GroundGlowAt(NestPosition);
                if(brightness < 0.045)
                {
                    return true;
                }
            }
            return false;
        }

        public bool ShouldDoMichief()
        {
            if (Find.TickManager.TicksGame < canMischiefTick)
            {
                return false;
            }

            if (parent.MapHeld.skyManager.CurSkyGlow > 0.55f)
            {
                return false;
            }

            if (!Parent.DevelopmentalStage.Adult())
            {
                return false;
            }

            if (Parent.health.hediffSet.HasNaturallyHealingInjury())
            {
                return false;
            }

            if (Parent.Faction == null || !Parent.Faction.IsPlayer)
            {
                canMischiefTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
                return true;
            }

            if (Parent.Faction.IsPlayer)
            {
                if (XMTSettings.PlayerSabotage)
                {
                    if (Parent.needs.joy.tolerances.BoredOf(ExternalDefOf.Gaming_Dexterity))
                    {
                        return false;
                    }
                    if (Parent.needs.mood.CurLevelPercentage < 0.5f || Parent.needs.joy.CurLevelPercentage < 0.25f)
                    {
                        canMischiefTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
                        return true;
                    }
                }
            }

            return false;
        }

        public bool ShouldSnuggle()
        {
            if (Find.TickManager.TicksGame < canNuzzleTick)
            {
                return false;
            }

            if (Parent.Faction == null)
            {
                if (Parent.genes != null)
                {
                    Gene_PsychicBonding bonding = Parent.genes.GetFirstGeneOfType<Gene_PsychicBonding>();
                    if (bonding != null)
                    {
                        if (bonding.CanBondToNewPawn)
                        {
                            canNuzzleTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
                            return true;
                        }

                    }
                }
                return false;
            }

            if (!Parent.DevelopmentalStage.Adult())
            {
                return false;
            }

            if (Parent.Faction.IsPlayer)
            {

                if (Parent.needs.joy.tolerances.BoredOf(ExternalDefOf.Social))
                {
                    return false;
                }

                if (Parent.needs.mood.CurLevelPercentage > 0.5f && Parent.needs.joy.CurLevelPercentage < 0.25)
                {
                    canNuzzleTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
                    return true;
                }

            }
            return false;
        }

        public bool ShouldOvomorphCandidate()
        {
            if (Find.TickManager.TicksGame < canOvomorphTick)
            {
                return false;
            }

            if (XMTHiveUtility.NoNestOnMap(Parent.Map))
            {
                return false;
            }

            if (!Parent.DevelopmentalStage.Adult())
            {
                return false;
            }

            if (NeedEggs)
            {
                canOvomorphTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
                return true;
            }

            return false;
        }

        public bool ShouldGorge()
        {
            if (Parent.needs == null)
            {
                return false;
            }

            if (Parent.needs.food == null)
            {
                return false;
            }

            if (Find.TickManager.TicksGame < canHuntTick)
            {
                return false;
            }

            if (!Parent.DevelopmentalStage.Adult())
            {
                if (Parent.needs.food.CurLevelPercentage < 0.9f)
                {
                    canHuntTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
                    return true;
                }
            }

            if (Parent.needs.food.CurCategory == HungerCategory.Starving && Parent.CurJobDef != JobDefOf.PredatorHunt && Parent.CurJobDef != JobDefOf.Ingest)
            {
                canHuntTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 600);
                return true;
            }

            if (Parent.needs.food.CurCategory != HungerCategory.Fed)
            {
                canHuntTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
                return true;
            }
            return false;
        }

        public bool ShouldAbductHost()
        {
            if (Find.TickManager.TicksGame < canAbductTick)
            {
                return false;
            }

            if (!Parent.DevelopmentalStage.Adult())
            {
                return false;
            }

            if (Parent.IsCarryingPawn())
            {
                return false;
            }

            if(XMTHiveUtility.ShouldBuildNest(Parent.Map))
            {
                return false;
            }

            if(Parent.ideo != null && Parent.ideo.Ideo is Ideo PawnIdeo)
            {
                if (Parent.IsFreeNonSlaveColonist)
                {
                    if (PawnIdeo.HasPrecept(XenoPreceptDefOf.XMT_Parasite_Abhorrent))
                    {
                        XMTUtility.GiveMemory(Parent, HorrorMoodDefOf.XMT_ImpureUrge);
                        canAbductTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
                        return false;
                    }
                }
            }

            if (XMTHiveUtility.NeedAbductions(Parent.Map))
            {
                canAbductTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
                return true;
            }

            return false;
        }

        public bool ShouldHunt()
        {
            if (Find.TickManager.TicksGame < canHuntTick)
            {
                return false;
            }

            if (!Parent.DevelopmentalStage.Adult())
            {
                return false;
            }

            canHuntTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
            return true;
        }

        public bool ShouldMature()
        {
            if (Find.TickManager.TicksGame < canMatureTick)
            {
                return false;
            }

            if (XMTHiveUtility.NoNestOnMap(Parent.Map))
            {
                return false;
            }

            if (Parent.DevelopmentalStage.Adult())
            {
                return false;
            }

            if (Parent.needs.food.CurLevelPercentage < 0.9f)
            {
                return false;
            }

            if (XMTHiveUtility.ShouldBuildNest(Parent.Map))
            {
                return false;
            }

            float brightness = Parent.MapHeld.glowGrid.GroundGlowAt(XMTHiveUtility.GetNestSpot(Parent.Map).Cell);

            if (brightness > 0.5f)
            {
                
                return false;
            }

            canMatureTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.IntervalHours * 2500);
            return true;
        }

        public bool ShouldTunnelToNest()
        {
            if (Find.TickManager.TicksGame < canTunnelTick)
            {
                return false;
            }

            bool CanReachNest = Parent.CanReach(NestPosition, PathEndMode.Touch, Danger.Deadly, canBashDoors: true, canBashFences: true);

            if (CanReachNest)
            {
                return false;
            }

            canTunnelTick = Find.TickManager.TicksGame + Mathf.CeilToInt(2500);
            return true;
        }
        public Job GetCandidateFeedJob(Pawn candidate)
        {
            if (Parent.needs.food == null || Parent.needs.food.CurLevelPercentage == 1)
            {
                Job job = null;
                if (candidate.ParentHolder is EggSack eggSack)
                {
                    job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PerformTrophallaxis, candidate,eggSack);
                    
                    FeralJobUtility.ReserveThingForJob(Parent, job, eggSack);
                }
                else
                {
                    job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PerformTrophallaxis, candidate);
                    FeralJobUtility.ReserveThingForJob(Parent, job, candidate);
                }
                return job;
            }
            return null;
        }
        public Pawn GetSnuggleTarget()
        {
            Pawn nuzzleTarget = null;

            int bestOpinion = int.MinValue;
            List<Pawn> freeColonistsSpawned = Parent.Map.mapPawns.AllHumanlikeSpawned;
            foreach (Pawn colonist in freeColonistsSpawned)
            {
                if (XMTUtility.IsXenomorph(colonist))
                {
                    continue;
                }
                int opinion = Parent.relations.OpinionOf(colonist);

                CompPawnInfo info = Parent.Info();

                if (opinion > bestOpinion)
                {
                    bestOpinion = opinion;
                    nuzzleTarget = colonist;
                }

            }

            if (bestOpinion < 0)
            {
                nuzzleTarget = null;
            }


            return nuzzleTarget;
        }

        int EvaluateHost(Pawn candidate)
        {
            int score = 0;
            if (XMTUtility.IsXenomorph(candidate))
            {
                return int.MinValue;
            }

            if (XMTUtility.IsInorganic(candidate))
            {
                score -= 30;

            }

            if (Parent.Faction == null || !Parent.Faction.IsPlayer)
            {
                if (candidate.Faction != null && candidate.Faction.IsPlayer)
                {
                    score += 20;
                }
            }
            else
            {
                if (candidate.IsPrisonerOfColony)
                {
                    score += 30;
                }

                if (candidate.Faction == null || !candidate.Faction.IsPlayer)
                {
                    score += 10;
                }
            }


            score -= Mathf.CeilToInt(candidate.Map.glowGrid.GroundGlowAt(candidate.Position) * 200);


            if (candidate.Downed)
            {
                score += 100;
            }

            if (candidate.RaceProps.Humanlike)
            {
                score += 10;
            }

            if (NeedEggs)
            {
                score += 30;
            }
            else if (XMTUtility.IsHost(candidate))
            {
                score += 50;
                if (candidate.health.capacities.GetLevel(PawnCapacityDefOf.Breathing) > 0.80f)
                {
                    score += 10;
                }

                if (candidate.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) > 0.50f)
                {
                    score += 10;
                }

                if (candidate.genes != null)
                {
                    score += candidate.genes.GenesListForReading.Count;
                }
            }

            if (!candidate.Awake())
            {
                score += 10;
            }

            score -= Parent.relations.OpinionOf(candidate);

            return score;
        }

        public Pawn BestAbductCandidate(IEnumerable<Pawn> candidates)
        {
            Pawn bestCandidate = null;
            int bestScore = int.MinValue;
            if (XMTHiveUtility.NoNestOnMap(Parent.Map))
            {
                return null;
            }

            bool onlyOtherFactionAbduct = false;

            if (Parent.ideo != null && Parent.ideo.Ideo is Ideo PawnIdeo)
            {
                if(PawnIdeo.HasPrecept(XenoPreceptDefOf.XMT_Parasite_OtherFaction))
                {
                    onlyOtherFactionAbduct = true;
                }
            }

            foreach (Pawn candidate in candidates)
            {
                if (XMTUtility.NotPrey(candidate))
                {
                    continue;
                }

                if (XMTUtility.PawnLikesTarget(Parent,candidate))
                {
                    continue;
                }

                CocoonBase cocoon = candidate.CurrentBed() as CocoonBase;
                if (cocoon != null)
                {
                    if (cocoon.def == XenoBuildingDefOf.XMT_CocoonBase ||
                        cocoon.def == XenoBuildingDefOf.XMT_CocoonBaseAnimal)
                    {
                        continue;
                    }
                }

                if (!FeralJobUtility.IsThingAvailableForJobBy(Parent, candidate))
                {
                    continue;
                }

                int score = EvaluateHost(candidate);

                if (score > bestScore)
                {
                    if (candidate.IsColonist || candidate.IsColonyAnimal)
                    {
                        if (onlyOtherFactionAbduct)
                        {
                            XMTUtility.GiveMemory(Parent, HorrorMoodDefOf.XMT_ImpureUrge);
                            continue;
                        }

                        if(Integrated)
                        {
                            continue;
                        }
                    }

                    bestCandidate = candidate;
                    bestScore = score;
                }
            }

            return bestCandidate;
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            if (dinfo != null)
            {
                Thing instigator = dinfo.Value.Instigator;
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Cryptobio_Killed, instigator.Named(HistoryEventArgsNames.Doer), Parent.Named(HistoryEventArgsNames.Victim)),true);
            }

            base.Notify_Killed(prevMap, dinfo);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            XMTHiveUtility.AddHiveMate(Parent, Parent.MapHeld);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
   
            XMTHiveUtility.RemoveHiveMate(Parent, map);
        }
        private void SeekNewLair()
        {
            if (Parent.CurrentBed() != null)
            {
                XMTHiveUtility.TryPlaceResinFloor(Parent.PositionHeld, Parent.MapHeld);
            }
        }

        public void TryMetamorphosis()
        {
            if (Props.maturingHediff == null)
            {
                return;
            }
            Hediff hediff = HediffMaker.MakeHediff(Props.maturingHediff, Parent);

            CocoonBase cocoonBase = XMTHiveUtility.TryPlaceCocoonBase(Parent.Position, Parent) as CocoonBase;
            if (cocoonBase != null)
            {
                Parent.jobs.Notify_TuckedIntoBed(cocoonBase);
            }
            Parent.health.AddHediff(hediff);
        }

        public void TryCocooning(Pawn target)
        {

            if (target.jobs != null)
            {
                target.jobs.StopAll();
            }

            if (Parent.needs.joy != null)
            {
                Parent.needs.joy.GainJoy(0.25f, InternalDefOf.NestTending);
            }

            if (target.health.hediffSet != null)
            {
                IEnumerable<Hediff> wounds = target.health.hediffSet.GetHediffsTendable();

                if (wounds.Any())
                {
                    foreach (Hediff wound in wounds)
                    {
                        HediffComp_LarvalAttachment larval = wound.TryGetComp<HediffComp_LarvalAttachment>();
                        if (larval != null)
                        {
                            continue;
                        }
                        wound.Tended(1, 1);
                    }
                }
            }

            if (XMTUtility.IsXenomorphFriendly(target) || XMTUtility.PawnLikesTarget(Parent, target))
            {
                return;
            }

            Hediff hediff = HediffMaker.MakeHediff(Props.cocoonHediff, target);
            target.health.AddHediff(hediff);
            XMTUtility.GiveInteractionMemory(target, HorrorMoodDefOf.Cocooned, Parent);
            XMTUtility.GiveMemory(Parent, HorrorMoodDefOf.CapturedHost);

            if (Parent.Faction != null && Parent.Faction != target.Faction)
            {
                target.GetLord()?.Notify_PawnAttemptArrested(target);
                GenClamor.DoClamor(target, 10f, ClamorDefOf.Harm);

                if (!target.IsPrisoner && !target.IsSlave)
                {
                    QuestUtility.SendQuestTargetSignals(target.questTags, "Arrested", target.Named("SUBJECT"));
                    if (target.Faction != null)
                    {
                        QuestUtility.SendQuestTargetSignals(target.Faction.questTags, "FactionMemberArrested", target.Faction.Named("FACTION"));
                    }
                }
            }
            CocoonBase cocoonBase = XMTHiveUtility.TryPlaceCocoonBase(Parent.Position, target) as CocoonBase;
            if (cocoonBase != null)
            {
                
                if (Parent.Faction != null && Parent.Faction.IsPlayer)
                {
                    cocoonBase.SetFaction(Parent.Faction);
                }
                if (target.IsPrisoner)
                {
                    cocoonBase.ForPrisoners = true;
                }
                RestUtility.TuckIntoBed(cocoonBase, Parent, target, false);
            }

            Parent.mindState.lastAttackedTarget = target;
            Parent.mindState.lastAttackTargetTick = Find.TickManager.TicksGame;
        }

        public void TryLardering(Pawn target)
        {
            if (!XMTUtility.IsTargetImmobile(target))
            {
                if (Rand.Chance(XMTUtility.GetDodgeChance(target, Parent.IsPsychologicallyInvisible())))
                {
                    MoteMaker.ThrowText(target.DrawPos, target.Map, "TextMote_Dodge".Translate(), 1.9f);
                    return;
                }
            }
            Hediff hediff = HediffMaker.MakeHediff(Props.larderHediff, target);

            if (Parent.Faction != null && Parent.Faction.IsPlayer)
            {
                hediff.SetVisible();
            }

            XMTUtility.GiveInteractionMemory(target, HorrorMoodDefOf.Ovomorphed, Parent);
            XMTUtility.GiveMemory(Parent, HorrorMoodDefOf.OvomorphedVictim);

            if (Parent.needs.joy != null)
            {
                Parent.needs.joy.GainJoy(0.12f, InternalDefOf.NestTending);
            }

            HediffComp_BuildingMorphing building = hediff.TryGetComp<HediffComp_BuildingMorphing>();

            if (building != null)
            {
                building.InheritFromInjector(Parent);
            }

            target.health.AddHediff(hediff);
        }
        public void TryOvomorphing(Pawn target)
        {
            if (!XMTUtility.IsTargetImmobile(target))
            {
                if (Rand.Chance(XMTUtility.GetDodgeChance(target, Parent.IsPsychologicallyInvisible())))
                {
                    MoteMaker.ThrowText(target.DrawPos, target.Map, "TextMote_Dodge".Translate(), 1.9f);
                    return;
                }
            }


            Hediff hediff = HediffMaker.MakeHediff(Props.OvomorphHediff, target);

            XMTUtility.GiveInteractionMemory(target, HorrorMoodDefOf.Ovomorphed, Parent);
            XMTUtility.GiveMemory(Parent, HorrorMoodDefOf.OvomorphedVictim);

            if (Parent.needs.joy != null)
            {
                Parent.needs.joy.GainJoy(0.12f, InternalDefOf.NestTending);
            }

            HediffComp_BuildingMorphing building = hediff.TryGetComp<HediffComp_BuildingMorphing>();

            if(Parent.Faction != null && Parent.Faction.IsPlayer)
            {
                int progress = 250;
                ResearchUtility.ProgressEvolutionTech(progress, Parent);
                hediff.SetVisible();
            }
            if(building != null)
            {
                building.InheritFromInjector(Parent);
            }

            target.health.AddHediff(hediff);
        }

        public bool InitiateGrabCheck(Pawn target)
        {
            if (target == null)
            {
                return false;
            }

            if (Parent.needs.joy != null)
            {
                Parent.needs.joy.GainJoy(0.12f, ExternalDefOf.Gaming_Dexterity);
            }

            CompPawnInfo info = target.Info();
            if (info != null)
            {
                if (info.IsObsessed())
                {
                    return true;
                }
            }

            if (target.IsPrisonerInPrisonCell())
            {
                return true;
            }

            XMTUtility.WitnessHorror(target.PositionHeld, target.MapHeld, 0.25f);

            if (Parent.Faction != null && Parent.Faction != target.Faction && !Parent.IsPsychologicallyInvisible())
            {
                target.GetLord()?.Notify_PawnAttemptArrested(target);
                GenClamor.DoClamor(target, 10f, ClamorDefOf.Harm);

                if (!target.IsPrisoner && !target.IsSlave)
                {
                    if (target.Faction != null)
                    {
                        if(target.Faction.RelationWith(Parent.Faction, true) == null)
                        {
                            target.Faction.TryMakeInitialRelationsWith(Parent.Faction);
                        }
                        //TODO: Ideology check if they venerate xenomorphs.
                        target.Faction.TryAffectGoodwillWith(Parent.Faction, -75, reason: HistoryEventDefOf.AttackedMember);
                    }
                }
            }

            if(XMTUtility.IsTargetImmobile(target))
            {
                return true;
            }


            if (Rand.Chance(XMTUtility.GetDodgeChance(target, Parent.IsPsychologicallyInvisible())))
            {
                MoteMaker.ThrowText(target.DrawPos, target.Map, "TextMote_Dodge".Translate(), 1.9f);

                return false;
            }
            

            if(Rand.Chance(XMTUtility.GetDefendGrappleChance(Parent,target)))
            {
                GenClamor.DoClamor(target, 10f, ClamorDefOf.Harm);
                target.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 1, 999f, -1, parent));
                return false;
            }
            return true;
        }
        public void TryGrab(Pawn target)
        {
            if(target == null)
            {
                return;
            }

            if (target.jobs != null)
            {
                target.jobs.StopAll();
            }

            Hediff hediff = HediffMaker.MakeHediff(Props.grabHediff, target);

            bool likesIt = false;
            if (target.story != null && target.story.traits != null)
            {
                if(target.story.traits.HasTrait(ExternalDefOf.Masochist))
                {
                    likesIt = true;
                }
            }

            CompPawnInfo info = target.Info();

            if (info != null)
            {
                XMTUtility.WitnessHorror(target.PositionHeld, target.MapHeld, 0.1f);
                if(info.IsObsessed())
                {
                    likesIt = true;
                }
            }


            if (likesIt)
            {
                XMTUtility.GiveInteractionMemory(target, HorrorMoodDefOf.GrabbedObsessed, Parent);
            }
            else
            {
                XMTUtility.GiveInteractionMemory(target, HorrorMoodDefOf.Grabbed, Parent);
                Parent.mindState.lastAttackedTarget = target;
                Parent.mindState.lastAttackTargetTick = Find.TickManager.TicksGame;
            }

            XMTUtility.GiveMemory(Parent, HorrorMoodDefOf.GrabbedPrey);

            target.health.AddHediff(hediff);
        }


        public bool FindMichief(out Job michief)
        {
            michief = null;

            if (XMTHiveUtility.NestOnMap(Parent.Map))
            {
                Thing offensive = XMTHiveUtility.GetMostOffensiveThingInNest(NestPosition, Parent.Map);

                if (offensive != null )
                {
                    michief = JobMaker.MakeJob(XenoWorkDefOf.XMT_Sabotage, offensive);
                    return true;
                }
            }

            Thing BestSabotageTarget = XMTUtility.GetPowerSabotageTarget(Parent);

            if (BestSabotageTarget != null)
            {
                michief = JobMaker.MakeJob(XenoWorkDefOf.XMT_Sabotage, BestSabotageTarget);
                return true;
            }
            return false;
        }

        protected bool GetOvomorphMoveJob(out Job job)
        {
            job = null;

            Ovomorph OvomorphCandidate = XMTHiveUtility.GetOvomorph(Parent.Map, requireReady:true, forPawn:Parent);
            if (OvomorphCandidate != null)
            { 
                Pawn hostCandidate = XMTHiveUtility.GetHost(Parent.Map, forPawn:Parent);

                if (hostCandidate != null)
                {
                    if (XMTUtility.IsXenomorphFriendly(hostCandidate) || XMTUtility.PawnLikesTarget(Parent, hostCandidate))
                    {
                        return false;
                    }
                    if (!hostCandidate.Spawned)
                    {
                        XMTHiveUtility.RemoveHost(hostCandidate, Parent.Map);
                        XMTHiveUtility.RemoveCocooned(hostCandidate, Parent.Map);
                    }
                    else
                    {
                        IEnumerable<IntVec3> eggSites = GenRadial.RadialCellsAround(hostCandidate.Position, 1.5f, false);
                        IntVec3 clearSite = IntVec3.Invalid;
                        foreach (IntVec3 eggSite in eggSites)
                        {
                            if (eggSite.GetEdifice(Parent.Map) == null && FeralJobUtility.IsPlaceAvailableForJobBy(Parent, eggSite))
                            {
                                clearSite = eggSite;
                                break;
                            }
                        }

                        if (clearSite != IntVec3.Invalid)
                        {
                            if (Parent.needs.joy != null)
                            {
                                Parent.needs.joy.GainJoy(0.12f, InternalDefOf.NestTending);
                            }
                            
                            job = JobMaker.MakeJob(XenoWorkDefOf.XMT_MoveOvomorph, OvomorphCandidate, clearSite);
                            job.count = 1;

                            FeralJobUtility.ReserveThingForJob(Parent, job, hostCandidate);
                            FeralJobUtility.ReserveThingForJob(Parent, job, OvomorphCandidate);
                            FeralJobUtility.ReservePlaceForJob(Parent, job, clearSite);
                            
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        protected bool GetJellyJob(out Job job)
        {
            if(JellyMaker == null)
            {
                JellyMaker = parent.GetComp<CompJellyMaker>();
            }

            if(JellyMaker != null)
            {
                return JellyMaker.GetJellyMakingJob(out job);
            }

            job = null;
            return false;
        }
        protected bool GetFeedJob(out Job job)
        {
            job = null;
            if (XMTSettings.LogJobGiver)
            {
                Log.Message(Parent + " is trying to get feeding job.");
            }
            Pawn FeedCandidate = XMTHiveUtility.GetHungriestCocooned(Parent.Map, forPawn: Parent);
            if (FeedCandidate != null)
            {
                if (!FeedCandidate.Spawned)
                {
                    XMTHiveUtility.RemoveHost(FeedCandidate, Parent.Map);
                    XMTHiveUtility.RemoveCocooned(FeedCandidate, Parent.Map);
                    XMTHiveUtility.RemoveOvomorphing(FeedCandidate, Parent.Map);
                }
                else
                {
                    job = GetCandidateFeedJob(FeedCandidate);
                    if (job != null)
                    {
                        return true;
                    }
                }
            }

            FeedCandidate = XMTHiveUtility.GetHungriestHivemate(Parent.Map, forPawn: Parent);

            if (FeedCandidate != null)
            {
                if (!FeedCandidate.Spawned)
                {
                    XMTHiveUtility.RemoveHiveMate(FeedCandidate, Parent.Map);
                }
                else
                {
                    job = GetCandidateFeedJob(FeedCandidate);
                    if (job != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        protected bool GetOvomorphingJob(out Job job)
        {
            job = null;
            if(!XMTUtility.NoQueenPresent())
            {
                return false;
            }
            //Log.Message(pawn + " thinks a candidate should be Ovomorphed.");
            Pawn candidate = XMTHiveUtility.GetOvomorphingCandidate(Parent.Map, forPawn: Parent);
            if (candidate != null)
            {
                if (XMTUtility.IsXenomorphFriendly(candidate) || XMTUtility.PawnLikesTarget(Parent, candidate))
                {
                    return false;
                }
                //Log.Message(pawn + " is going to Ovomorph " + target);
                if (!candidate.Spawned)
                {
                    XMTHiveUtility.RemoveHost(candidate, Parent.Map);
                }
                else
                {
                    job =  JobMaker.MakeJob(XenoWorkDefOf.XMT_ApplyOvomorphing, candidate);
                    FeralJobUtility.ReserveThingForJob(Parent, job, candidate);
                    return true;
                }
            }

            return false;
            
        }

        protected bool GetLarderJob(out Job job)
        {
            job = null;
            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_LarderSerum))
            {
                return false;
            }
            
            Pawn candidate = XMTHiveUtility.GetBestLarderCandidate(Parent.Map, forPawn: Parent);
            if (candidate != null)
            {
                if (XMTUtility.IsXenomorphFriendly(candidate) || XMTUtility.PawnLikesTarget(Parent, candidate))
                {
                    return false;
                }
                job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ApplyLardering, candidate);
                FeralJobUtility.ReserveThingForJob(Parent, job, candidate);
                return true;
            }

            return false;
        }

        protected bool GetPruningJob(out Job job)
        {
            job = null;
            
            MeatballLarder PruningLarder = XMTHiveUtility.GetMostPrunableLarder(Parent.Map, Parent);
            if (PruningLarder != null)
            {
                if (PruningLarder.CanBePruned())
                {
                    job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PruneLarder, PruningLarder);
                    FeralJobUtility.ReserveThingForJob(Parent, job, PruningLarder);
                    return true;
                }
            }
            
            return false;
        }

        protected Job GetTunnelJob(Pawn pawn, IntVec3 dest)
        {
            PawnPath path = pawn.Map.pathFinder.FindPathNow(pawn.Position, dest, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings));
            IntVec3 cellBefore;
            Thing thing = path.FirstBlockingBuilding(out cellBefore, pawn);
            if (thing != null)
            {
                ThingDef doorDef = XenoBuildingDefOf.HiveWebbing;
                Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_HiveBuilding, thing.Position);
                job.plantDefToSow = XenoBuildingDefOf.HiveWebbing;
                if (job != null)
                {
                    return job;
                }
            }
            return null;
        }

        protected bool GetNestBuildJob(out Job job)
        {
            if (ShouldTunnelToNest())
            {
                //Log.Message(Parent + " thinks the nest is inaccessible.");
                job = GetTunnelJob(Parent, NestPosition);
                if (job != null)
                {
                   //Log.Message(Parent + " is clearing a path to the nest.");
                    return true;
                }
            }

            job = XMTHiveUtility.GetNestBuildJob(Parent);

            if (job != null)
            {
                return true;
            }
            return false;
        }

        protected bool GetNestCoolingJob(out Job job)
        {
            job = null;
            IntVec3 cell = XMTHiveUtility.GetValidCocoonCell(Parent.Map, Parent);
            if (cell.IsValid)
            {
                ThingDef cooler = XenoBuildingDefOf.AtmospherePylon;
                job = JobMaker.MakeJob(XenoWorkDefOf.XMT_HiveBuilding, cell);
                FeralJobUtility.ReservePlaceForJob(Parent, job, cell);

                job.plantDefToSow = cooler;
                if (job != null)
                {
                    return true;
                }
            }
            return false;
        }

        public bool GetHuntJob(out Job job)
        {
            job = null;

            IEnumerable<Designation> huntTargets = Parent.Map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt);

            if (huntTargets.Any())
            {
                List<Designation> hunts = huntTargets.ToList();
                hunts.Shuffle();

                foreach (Designation hunt in hunts)
                {
                    if (hunt.target.TryGetPawn(out Pawn prey))
                    {
                        if(!FeralJobUtility.IsThingAvailableForJobBy(Parent,prey))
                        {
                            continue;
                        }

                        if (XMTUtility.IsXenomorphFriendly(prey) || XMTUtility.PawnLikesTarget(Parent, prey))
                        {
                            continue;
                        }
                        job = JobMaker.MakeJob(JobDefOf.PredatorHunt, prey);
                        job.killIncappedTarget = true;
                        Parent.needs.joy.GainJoy(0.12f, ExternalDefOf.Gaming_Dexterity);
                        FeralJobUtility.ReserveThingForJob(Parent, job, prey);
                        return true;

                    }
                }
            }

            return false;
        }

        public bool GetEnemyJob(out Job job)
        {
            job = null;

            IEnumerable<Designation> enemyTargets = Parent.Map.designationManager.SpawnedDesignationsOfDef(XenoWorkDefOf.XMT_Enemy);

            if (enemyTargets.Any())
            {
                List<Designation> materials = enemyTargets.ToList();
                materials.Shuffle();

                foreach (Designation material in materials)
                {
                    if (material.target.Thing is Pawn pawn)
                    {
                        if (pawn.Info().XenomorphPheromoneValue() < 0)
                        {
                            continue;
                        }

                        if (!FeralJobUtility.IsThingAvailableForJobBy(Parent, pawn))
                        {
                            continue;
                        }

                        job = JobMaker.MakeJob(XenoWorkDefOf.XMT_MarkEnemy, pawn);
                        FeralJobUtility.ReserveThingForJob(Parent, job, pawn);
                        return true;

                    }
                }
            }

            return false;
        }

        public bool GetBefriendJob(out Job job)
        {
            job = null;

            IEnumerable<Designation> friendTargets = Parent.Map.designationManager.SpawnedDesignationsOfDef(XenoWorkDefOf.XMT_Friend);

            if (friendTargets.Any())
            {
                List<Designation> materials = friendTargets.ToList();
                materials.Shuffle();

                foreach (Designation material in materials)
                {
                    if (material.target.Thing is Pawn pawn)
                    {
                        if (XMTUtility.IsXenomorphFriendly(pawn))
                        {
                            continue;
                        }

                        if (!FeralJobUtility.IsThingAvailableForJobBy(Parent, pawn))
                        {
                            continue;
                        }

                        job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Snuggle, pawn);
                        FeralJobUtility.ReserveThingForJob(Parent, job, pawn);
                        return true;

                    }
                }
            }

            return false;
        }
        public bool GetArtJob(out Job job)
        {
            job = null;

            IEnumerable<Designation> artTargets = Parent.Map.designationManager.SpawnedDesignationsOfDef(XenoWorkDefOf.XMT_CorpseArt);

            if (artTargets.Any())
            {
                List<Designation> materials = artTargets.ToList();
                materials.Shuffle();

                foreach (Designation material in materials)
                {
                    if (material.target.Thing is Corpse corpse)
                    {
                        if(!FeralJobUtility.IsThingAvailableForJobBy(Parent,corpse))
                        {
                            continue;
                        }
                        
                        job = JobMaker.MakeJob(XenoWorkDefOf.XMT_CorpseSculpture, corpse);
                        FeralJobUtility.ReserveThingForJob(Parent, job, corpse);
                        return true;

                    }
                }
            }

            return false;
        }

        public bool GetAbductJob(out Job job)
        {
            job = null;

            IEnumerable<Designation> abductTargets = Parent.Map.designationManager.SpawnedDesignationsOfDef(XenoWorkDefOf.XMT_Abduct);

            if (abductTargets.Any())
            {
                List<Designation> tames = abductTargets.ToList();
                tames.Shuffle();

                foreach (Designation hunt in tames)
                {
                    if (hunt.target.TryGetPawn(out Pawn prey))
                    {
                        if (!FeralJobUtility.IsThingAvailableForJobBy(Parent, prey))
                        {
                            continue;
                        }

                        if (XMTUtility.GetDefendGrappleChance(Parent,prey) >= 0.25f)
                        {
                            continue;
                        }


                        IntVec3 cell = XMTHiveUtility.GetValidCocoonCell(Parent.Map, Parent);
                        job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AbductHost, prey, cell);
                        FeralJobUtility.ReservePlaceForJob(Parent, job, cell);
                        FeralJobUtility.ReserveThingForJob(Parent, job, prey);
                        job.count = 1;
                        return true;

                    }
                }
            }

            return false;
        }


        public bool GetCocoonPrisonerJob(out Job job)
        {
            job = null;

            List<Pawn> prisoners = Parent.Map.mapPawns.PrisonersOfColony;

            prisoners.Shuffle();

            foreach (Pawn prisoner in prisoners)
            {
                if (FeralJobUtility.IsThingAvailableForJobBy(Parent, prisoner))
                { 
                    if (!prisoner.PositionHeld.InAllowedArea(Parent))
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }

                if (XMTUtility.IsXenomorphFriendly(prisoner) || XMTUtility.PawnLikesTarget(Parent, prisoner))
                {
                    continue;
                }

                if (XMTUtility.GetDefendGrappleChance(Parent, prisoner) >= 0.25f)
                {
                    continue;
                }

                if(XMTUtility.IsCocooned(prisoner))
                {
                    continue;
                }

                job = JobMaker.MakeJob(XenoWorkDefOf.XMT_CocoonTarget, prisoner);
                job.count = 1;
                FeralJobUtility.ReserveThingForJob(Parent, job, prisoner);
                return true;
            }
            

            return false;
        }

        public bool GetNestJobByWorkType(WorkTypeDef workType, out Job job)
        {
            job = null;
           
            if(workType == XenoWorkDefOf.Childcare)
            {
                if (GetOvomorphMoveJob(out job))
                {
                    return true;
                }
                
                if(GetOvomorphingJob(out job))
                {
                    return true;
                }
                return false;
            }

            if (workType == XenoWorkDefOf.Doctor)
            {
                if (Parent.needs.food != null && Parent.needs.food.CurLevelPercentage == 1)
                {
                    if (GetFeedJob(out job))
                    {
                        return true;
                    }
                }
                return false;
            }

            if (workType == XenoWorkDefOf.Hunting)
            {
                if (GetEnemyJob(out job))
                {
                    return true;
                }

                if (GetHuntJob(out job))
                {
                    return true;
                }

                return false;
            }

            if (workType == XenoWorkDefOf.Handling)
            {
                if(GetBefriendJob(out job))
                {
                    return true;
                }

                if(GetAbductJob(out job))
                {
                    return true;
                }
                return false;
            }

            if (workType == XenoWorkDefOf.Warden)
            {
                if (Parent.needs.food != null && Parent.needs.food.CurLevelPercentage == 1)
                {
                    if (GetFeedJob(out job))
                    {
                        return true;
                    }
                }

                if (GetCocoonPrisonerJob(out job))
                {
                    return true;
                }

                return false;
            }


            if (workType == XenoWorkDefOf.Cooking)
            {
                if (GetJellyJob(out job))
                {
                    return true;
                }
                return false;
            }

            if(workType == XenoWorkDefOf.Construction)
            {
                if (XMTHiveUtility.ShouldCoolNest(Parent.Map))
                {
                    if (GetNestCoolingJob(out job))
                    {
                        return true;
                    }
                }
                return false;
            }

            if (workType == XenoWorkDefOf.Growing)
            {
                /*
                Auto Larder Implanting.
                */

                if (GetLarderJob(out job))
                {
                    return true;
                }
                
                return false;
            }

            if (workType == XenoWorkDefOf.PlantCutting)
            {
                if (GetPruningJob(out job))
                {
                    return true;
                }

                return false;
            }

            if (workType == XenoWorkDefOf.Art)
            {
                if (GetArtJob(out job))
                {
                    return true;
                }

                return false;
            }

            return false;
        }
        public Job GetTendNestJob()
        {
            Job job;
            if (Parent.workSettings.EverWork)
            {
                foreach(WorkGiver work in Parent.workSettings.WorkGiversInOrderNormal)
                {
                    if(GetNestJobByWorkType(work.def.workType, out job))
                    {
                        return job;
                    }    
                }
            }
            else
            {
                if (GetOvomorphMoveJob(out job))
                {
                    return job;
                }

                if (GetBefriendJob(out job))
                {
                    return job;
                }

                if (GetAbductJob(out job))
                {
                    return job;
                }

                if (Parent.needs.food != null && Parent.needs.food.CurLevelPercentage == 1)
                {
                    if (GetFeedJob(out job))
                    {
                        return job;
                    }
                }
                else
                {
                    if (GetPruningJob(out job))
                    {
                        return job;
                    }
                }

                if (Parent.Faction == null && XMTHiveUtility.ShouldBuildNest(Parent.Map))
                {
                    if (GetNestBuildJob(out job))
                    {
                        return job;
                    }
                }

                if (XMTHiveUtility.ShouldCoolNest(Parent.Map))
                {
                    if (GetNestCoolingJob(out job))
                    {
                        return job;
                    }
                }

                if(GetJellyJob(out job))
                {
                    return job;
                }

                if (GetLarderJob(out job))
                {
                    return job;
                }

                if (GetEnemyJob(out job))
                {
                    return job;
                }
            }
            return null;
        }

        public void EnterHidingSpot(IntVec3 hidingSpot)
        {
            ThingDef cocoonDef = XenoBuildingDefOf.HiveHidingSpot;
            if (cocoonDef == null)
            {
                Log.Warning(Parent + " is trying to hide without HiveHidingSpot Defined");
                return;
            }

            if(Parent.BodySize > 1.5f)
            {
                return;
            }

            CocoonBase cocoon = ThingMaker.MakeThing(cocoonDef) as CocoonBase;
            if (cocoon == null)
            {
                Log.Warning(Parent + " could not form hidingspot");
                return;
            }
            cocoon.SetFaction(Parent.Faction);


            IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(hidingSpot, 5, true);
            foreach (IntVec3 cell in cells)
            {
                if(!cell.Standable(Parent.Map))
                {
                    continue;
                }

                if(cell.TryGetFirstThing(Parent.Map,out CocoonBase thing))
                {
                    continue;
                }
                cocoon = GenSpawn.Spawn(cocoon, cell, Parent.Map, WipeMode.VanishOrMoveAside) as CocoonBase;
                break;
            }

            if(cocoon != null)
            {
                if(Parent.needs.TryGetNeed(NeedDefOf.Rest) is Need_Rest rest)
                {
                    if(rest.CurLevelPercentage >= 1.0f)
                    {
                        rest.CurLevelPercentage = 0.5f;
                    }
                }
                Parent.jobs.Notify_TuckedIntoBed(cocoon);
            }
            

        }
        public void EnterHiberation()
        {
            ThingDef cocoonDef = Props.hibernationCocoon;
            if (cocoonDef == null)
            {
                Log.Warning(Parent + " is trying to hibernate without a hibernationCocoon ThingDef");
                return;
            }

            HibernationCocoon cocoon = ThingMaker.MakeThing(cocoonDef) as HibernationCocoon;
            if(cocoon == null)
            {
                Log.Warning(Parent + " could not form cocoon");
                return;
            }
            cocoon.SetFaction(Parent.Faction);

            cocoon = GenSpawn.Spawn(cocoon, Parent.Position, Parent.Map,WipeMode.VanishOrMoveAside) as HibernationCocoon;

            cocoon.Rotation = Rot4.Random;
            bool flag = Parent.DeSpawnOrDeselect();
            if (cocoon.TryAcceptThing(Parent) && flag)
            {
                Find.Selector.Select(Parent, playSound: false, forceDesignatorDeselect: false);
            }

        }

        public void ClearAllTickLimits()
        {
            canNuzzleTick = 0;
            canAbductTick = 0;
            canOvomorphTick = 0;
            canMatureTick = 0;
            canMischiefTick = 0;
            canHuntTick = 0;
            canTendLairTick = 0;
            canTunnelTick = 0;
        }

        internal void DelayedDestroy(DestroyMode mode)
        {
            destroyNextTick = true;
            delayedDestroyMode = mode;
        }

        public void TryAmbushAbduct(Pawn targetPawn)
        {
            targetPawn.TakeDamage(new DamageInfo(DamageDefOf.Stun, 8));
            if (InitiateGrabCheck(targetPawn))
            {
                if (Parent.playerSettings != null)
                {
                    Parent.playerSettings.hostilityResponse = HostilityResponseMode.Ignore;
                }
                Hediff hediff = HediffMaker.MakeHediff(InternalDefOf.XMT_Ambushed, targetPawn);
                targetPawn.health.AddHediff(hediff);
                Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AbductHost, targetPawn, XMTHiveUtility.GetNestPosition(targetPawn.Map));
                job.count = 1;
                Parent.jobs.StartJob(job, JobCondition.InterruptForced);
            }
            else
            {
                if (Parent.playerSettings != null)
                {
                    Parent.playerSettings.hostilityResponse = HostilityResponseMode.Attack;
                }
            }
        }

        public void TryAmbushAttack(Pawn targetPawn)
        {
            if(Parent.playerSettings != null)
            {
                Parent.playerSettings.hostilityResponse = HostilityResponseMode.Attack;
            }
            targetPawn.TakeDamage(new DamageInfo(DamageDefOf.Stun, 8));
            Parent.Map.reservationManager.ReleaseAllForTarget(targetPawn);
            Job job = JobMaker.MakeJob(JobDefOf.PredatorHunt, targetPawn);
            Parent.jobs.StartJob(job, JobCondition.InterruptForced);
        }
    }

    public class CompMatureMorphProperties : CompProperties
    {
        public float        abductRange = 3f;
        public float        IntervalHours = 0.1f;
        public float        slowDownDailyChance = 0.25f;
        public HediffDef    grabHediff;
        public HediffDef    cocoonHediff;
        public HediffDef    OvomorphHediff;
        public HediffDef    larderHediff;
        public HediffDef    maturingHediff;
        public ThingDef     hibernationCocoon;
        public CompMatureMorphProperties()
        {
            this.compClass = typeof(CompMatureMorph);
        }

        public CompMatureMorphProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
