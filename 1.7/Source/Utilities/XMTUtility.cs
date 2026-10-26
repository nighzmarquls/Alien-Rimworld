using AlienRace;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;



namespace Xenomorphtype
{
    public class XMTUtility
    {
        private static Pawn Queen
        {
            get => HiveQueenUtility.Resolve();
            set
            {
                GameComponent_Xenomorph component = Current.Game?.GetComponent<GameComponent_Xenomorph>();
                if (component == null) return;
                if (value == null) component.Queen = null;
                else HiveQueenUtility.Register(value);
            }
        }
        public static bool IsThingWaryOfTarget(Thing thing, Thing Target)
        {
            if (Target.HostileTo(thing))
            {
                return true;
            }

            Building_TurretGun turret = Target as Building_TurretGun;

            if (turret != null)
            {
                return true;
            }

            return false;
        }

        public static Thing GetPowerSabotageTarget(Pawn pawn)
        {
            Map map = pawn.Map;
            Thing SabotageTarget = null;
            PowerNet bestPowerNet = null;
            int bestScore = int.MinValue;
            foreach (PowerNet net in map.powerNetManager.AllNetsListForReading)
            {
                int score = 0;
                if (net.hasPowerSource)
                {
                    score += net.batteryComps.Count * 2;
                    score += net.powerComps.Count;

                    int lights = 0;
                    foreach (CompPowerTrader powerUser in net.powerComps)
                    {
                        if (powerUser.parent is Building_Door poweredDoor &&
                            XMTDoorUtility.HasPoweredResistance(poweredDoor))
                        {
                            score += XMTDoorUtility.PoweredDoorSabotageScore;
                        }

                        if(!FeralJobUtility.IsThingAvailableForJobBy(pawn,powerUser.parent))
                        {
                            continue;
                        }

                        
                        
                        CompGlower glow = powerUser.parent.GetComp<CompGlower>();
                        if (glow != null)
                        {
                            lights++;

                            float lightscore = glow.Props.glowRadius + (glow.Props.overlightRadius * 2) - 1;
                            if (glow.Props.darklightToggle)
                            {
                                lightscore *= 0.5f;
                            }
                            score += Mathf.CeilToInt(lightscore);
                        }

                        Building_TurretGun turret = powerUser.parent as Building_TurretGun;

                        if (turret != null)
                        {
                            score += 20;
                        }
                    }
                }

                if (score > 0 && score > bestScore)
                {
                    bestScore = score;
                    bestPowerNet = net;
                }
            }

            if (bestPowerNet != null)
            {
                //Get Cables that are in darkness.
                IEnumerable<CompPower> cables = bestPowerNet.transmitters.Where(x =>
                (x as CompPowerBattery == null) &&
                (x as CompPowerTrader == null) &&
                XMTMischiefUtility.IsDarkEnoughForMischief(x.parent.Position, map) &&
                pawn.CanReach(x.parent.Position, PathEndMode.ClosestTouch, Danger.None) &&
                pawn.CanReserve(x.parent));

                if (cables.Any())
                {
                    SabotageTarget = cables.RandomElement().parent;
                }
            }

            return SabotageTarget;
        }
        public static bool GetPartAttachedToPartOnPawn(Pawn pawn, ref BodyPartRecord part)
        {
            BodyPartRecord partRecord = part;

            List<BodyPartRecord> parts = (part.parent != null) ?
                 part.parent.GetPartAndAllChildParts().Where(x => x != partRecord).ToList() : pawn.health.hediffSet.GetNotMissingParts().ToList();
            
            if(parts.Count == 0)
            {
                return false;
            }

            parts.Shuffle();
            bool searching = true;
            int checks = 0;
            bool found = false;
            
            BodyPartRecord targetPart = parts[0];
            while (searching && checks < 50)
            {
                if (!pawn.health.hediffSet.PartIsMissing(targetPart))
                {
                    found = true;
                    searching = false;
                    break;
                }
                parts.Shuffle();
                targetPart = parts[0];
                checks++;
            }

            part = targetPart;
            return found;
        }
        public static Thing TrySpawnPawnFromTarget(Pawn pawn, Thing target)
        {
            if (XMTSettings.LogBiohorror)
            {
                Log.Message("[XMT][Biohorror][PawnTransformation] " + pawn + " is trying to spawn from " + target);
            }
            Thing ValidSpawnTarget = target;

            Pawn TargetPawn = target as Pawn;

            /*
            * there's Thing.PositionHeld and Thing.MapHeld that accounts for the carrier. 
            * there's also Thing.SpawnedParentOrMe which will get the ultimate carried-by object.
            * might be simpler to write
            * you can also do if(target is Pawn TargetPawn) to do the test and cast in one shot
            */

            
            if (TargetPawn != null)
            {
                if (TargetPawn.Dead)
                {
                    ValidSpawnTarget = TargetPawn.Corpse;
                }

                Pawn Carrier = TargetPawn.CarriedBy;
                if (Carrier != null)
                {
                    ValidSpawnTarget = Carrier;
                }

                if (TargetPawn.IsPlayerControlledCaravanMember())
                {
                    TargetPawn.GetCaravan().AddPawn(pawn, true);
                    return pawn;
                }

            }

           if(ValidSpawnTarget.MapHeld == null)
            {
                Find.WorldPawns.PassToWorld(
                    pawn,
                    TargetPawn != null ? PawnDiscardDecideMode.KeepForever : PawnDiscardDecideMode.Decide);
                return pawn;
            }

            return GenSpawn.Spawn(pawn, ValidSpawnTarget.Position, ValidSpawnTarget.Map);
        }

        public static bool IsSpotter(Thing thing)
        {
            Building_TurretGun turret = thing as Building_TurretGun;

            if (turret != null && turret.PowerComp != null)
            {
                if (turret.PowerComp.PowerNet.hasPowerSource)
                {
                    return true;
                }
            }

            Building_ProximityDetector detector = thing as Building_ProximityDetector;

            if (detector != null && detector.PowerComp != null)
            {
                if (detector.PowerComp.PowerNet.hasPowerSource)
                {
                    return true;
                }
            }

            Pawn pawn = thing as Pawn;

            if (pawn != null)
            {
                return !IsXenomorph(pawn);
            }

            return false;
        }

        public static bool SmellsPheromones(Thing thing)
        {
            if (IsXenomorph(thing))
            {
                return true;
            }

            return false;
        }

        public static bool IsMatureMorph(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            CompMatureMorph perfect = pawn.GetMorphComp();

            if (perfect != null)
            {
                return true;
            }

            return false;
        }

        public static bool IsCrawler(Pawn pawn)
        {
            if(pawn == null)
            {
                return false;
            }
            if(pawn.GetCrawlerComp() is CompCrawler)
            {
                return true;
            }
            return false;
        }

        public static bool IsXenomorph(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            CompPerfectOrganism perfect = pawn.GetPerfectComp();

            if (perfect != null)
            {
                return true;
            }

            return false;
        }
        public static bool IsXenomorph(Thing thing)
        {
            Ovomorph Ovomorph = thing as Ovomorph;

            if (Ovomorph != null)
            {
                return true;
            }

            Pawn pawn = thing as Pawn;

            return IsXenomorph(pawn);
        }

        public static bool CacheIsInorganic(Pawn pawn)
        {
            if (pawn == null)
            {
                return true;
            }

            if (!pawn.RaceProps.IsFlesh)
            {
                return true;
            }

            if (pawn.RaceProps.BloodDef == ThingDefOf.Filth_MachineBits)
            {
                return true;
            }

            if (!pawn.RaceProps.hasMeat)
            {
                return true;
            }

            if(pawn.RaceProps.FleshType == FleshTypeDefOf.EntityMechanical)
            {
                return true;
            }

            if (pawn.RaceProps.FleshType == ExternalDefOf.Asimov_Automaton)
            {
                return true;
            }

            if (pawn.def is ThingDef_AlienRace HARdef)
            {
                if (!HARdef.alienRace.compatibility.IsFleshPawn(pawn))
                {

                    return true;
                }
            }

            if (pawn.genes != null)
            {
                foreach(Gene gene in pawn.genes.GenesListForReading)
                {
                    if (gene.def == ExternalDefOf.VREA_SyntheticBody)
                    {
                        return true;
                    }
                    if(gene.def == ExternalDefOf.PMP_PersonaMechanoid)
                    {
                        return true;
                    }
                }
                
            }


            if (pawn.health != null)
            {
                if (ModsConfig.IsActive("kentington.saveourship2"))
                {
                    foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                    {
                        if (hediff.def == ExternalDefOf.SoSHologram ||
                            hediff.def == ExternalDefOf.SoSHologramMachine ||
                            hediff.def == ExternalDefOf.SoSHologramMachine)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool IsInorganic(Pawn pawn)
        {
            return pawn.IsInorganic();
        }

        public static LifeStageDef GetLifeStageByEmbryoMaturity(float input, out float age)
        {
            float HatchingAge = 0;
            float MaturityAge = 0;
            bool foundHatchingAge = false;
            LifeStageDef Lifestage = null;
            for (int i = 0; i < InternalDefOf.XMT_Starbeast_AlienRace.race.lifeStageAges.Count; i++)
            {
                LifeStageAge lifeStageAge = InternalDefOf.XMT_Starbeast_AlienRace.race.lifeStageAges[i];
                if (foundHatchingAge)
                {
                    if (lifeStageAge.minAge < MaturityAge)
                    {
                        Lifestage = lifeStageAge.def;
                    }
                    else
                    {
                        break;
                    }
                }
                else if (lifeStageAge != null)
                {
                    if (!lifeStageAge.def.alwaysDowned)
                    {
                        HatchingAge = lifeStageAge.minAge;
                        Lifestage = lifeStageAge.def;
                        foundHatchingAge = true;
                        MaturityAge = HatchingAge;
                    }
                }
            }
            age = MaturityAge;
            return Lifestage;
        }

        

        //TODO: implement environmental damagelogs.
        public static LogEntry_DamageResult CreateEnvironmentalDamageLogEntry()
        {

            //LogEntry_DamageResult DamageLog = new LogEntry_DamageResult();
            return null;
        }


        public static GrappleCheckReport GetGrappleCheckReport(Pawn attacker, Pawn defender)
        {
            GrappleCheckReport report = new GrappleCheckReport
            {
                Attacker = attacker,
                Defender = defender
            };

            if (attacker == null || defender == null)
            {
                report.BlockedReason = "missing attacker or defender";
                return report;
            }

            if (defender.WorkTagIsDisabled(WorkTags.Violent))
            {
                report.BlockedReason = "defender cannot perform violent work";
                return report;
            }

            report.DefenderStrength = Mathf.Max(defender.GetStatValue(XenoStatDefOf.XMT_GrappleStrength), 0f);
            report.AttackerStrength = Mathf.Max(attacker.GetStatValue(XenoStatDefOf.XMT_GrappleStrength), 0.0001f);
            report.DefenderConsciousFactor = 1f;
            report.DefenderStunFactor = 1f;
            report.AttackerInvisibleFactor = 1f;
            report.BodySizeFactor = Mathf.Clamp(defender.BodySize / Mathf.Max(attacker.BodySize, 0.0001f), 0.05f, 20f);

            if (!defender.Awake())
            {
                report.DefenderConsciousFactor = 0.1f;
            }

            if(defender.stances.stunner.Stunned)
            {
                report.DefenderStunFactor = 0.5f;
            }

            if (attacker.IsPsychologicallyInvisible())
            {
                report.AttackerInvisibleFactor = 2f;
            }

            report.ModifiedDefenderStrength = report.DefenderStrength * report.BodySizeFactor * report.DefenderConsciousFactor * report.DefenderStunFactor;
            report.ModifiedAttackerStrength = report.AttackerStrength * report.AttackerInvisibleFactor;
            report.ResistChance = Mathf.Clamp01(report.ModifiedDefenderStrength / Mathf.Max(report.ModifiedDefenderStrength + report.ModifiedAttackerStrength, 0.0001f));
            return report;
        }

        public static float GetDefendGrappleChance(Pawn attacker, Pawn defender)
        {
            return GetGrappleCheckReport(attacker, defender).ResistChance;

        }

        //Because Ludeon studios woulden't expose this function for some reason
        public static float GetDodgeChance(LocalTargetInfo target, bool surpriseAttack)
        {
            if (surpriseAttack)
            {
                return 0f;
            }

            if (!(target.Thing is Pawn pawn))
            {
                return 0f;
            }

            if (pawn.stances.stunner.Stunned)
            {
                return 0f;
            }

            if (pawn.stances.curStance is Stance_Busy stance_Busy && stance_Busy.verb != null && !stance_Busy.verb.verbProps.IsMeleeAttack)
            {
                return 0f;
            }

            float num = pawn.GetStatValue(StatDefOf.MeleeDodgeChance);
            if (ModsConfig.IdeologyActive)
            {
                if (DarknessCombatUtility.IsOutdoorsAndLit(target.Thing))
                {
                    num += pawn.GetStatValue(StatDefOf.MeleeDodgeChanceOutdoorsLitOffset);
                }
                else if (DarknessCombatUtility.IsOutdoorsAndDark(target.Thing))
                {
                    num += pawn.GetStatValue(StatDefOf.MeleeDodgeChanceOutdoorsDarkOffset);
                }
                else if (DarknessCombatUtility.IsIndoorsAndDark(target.Thing))
                {
                    num += pawn.GetStatValue(StatDefOf.MeleeDodgeChanceIndoorsDarkOffset);
                }
                else if (DarknessCombatUtility.IsIndoorsAndLit(target.Thing))
                {
                    num += pawn.GetStatValue(StatDefOf.MeleeDodgeChanceIndoorsLitOffset);
                }
            }

            return num;
        }

        //Because Ludeon studios woulden't expose this function for some reason
        public static bool IsTargetImmobile(LocalTargetInfo target)
        {
            Thing thing = target.Thing;

            if(thing is Plant)
            {
                return true;
            }

            if (thing is Pawn pawn && !pawn.Downed)
            {

                return pawn.GetPosture() != PawnPosture.Standing ;
            }

            return true;
        }

        public static bool DamageApparelByBodyPart(Pawn pawn, BodyPartGroupDef bodypart, float damage)
        {
            if (pawn.apparel.BodyPartGroupIsCovered(bodypart))
            {
                IEnumerable<Apparel> HeadCovering = from x in pawn.apparel.WornApparel
                                                    where
                                                    x.def.apparel.bodyPartGroups.Contains(bodypart)
                                                    select x;

                if (HeadCovering.Any())
                {
                    DamageDef Crush = DamageDefOf.Crush;
                    DamageInfo CoveringDamage = new DamageInfo(Crush, damage, 0f, -1f);
                    foreach (Apparel HeadCover in HeadCovering)
                    {
                        DamageWorker.DamageResult result = HeadCover.TakeDamage(CoveringDamage);

                        if (HeadCover.HitPoints <= 0)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            return false;
        }

        public static Thing SearchRegionForUnreservedWeapon(Pawn pawn)
        {
            Region region = pawn.GetRegion();
            TraverseParms traverseParams = TraverseParms.For(pawn);
            RegionEntryPredicate entryCondition = (Region from, Region r) => r.Allows(traverseParams, isDestination: false);
            Thing found = null;
            RegionProcessor regionProcessor = delegate (Region r)
            {
                List<Thing> list = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.Weapon));

                for (int i = 0; i < list.Count; i++)
                {
                    Thing thing = list[i];

                    if (pawn.Map.reservationManager.IsReserved(thing))
                    {
                        continue;
                    }

                    if (pawn.Faction != null && pawn.Faction.IsPlayer)
                    {
                        if (thing.IsForbidden(pawn.Faction))
                        {
                            continue;
                        }
                    }
                    found = thing;
                    return true;

                }
                return false;
            };
            RegionTraverser.BreadthFirstTraverse(region, entryCondition, regionProcessor, 99999);
            return found;
        }
        public static Thing SearchRegionsForJellyMakable(Region region, Pawn pawn, CompJellyMaker jellyMaker, bool requireLightLevel = false)
        {
            if (jellyMaker == null) return null;
            return SearchRegionsForHaulable(region, pawn, thing => jellyMaker.CanMakeIntoJelly(thing)
                && (!requireLightLevel || CompJellyMaker.IsProperLightLevelForFeralJelly(thing.PositionHeld, thing.MapHeld)));
        }
        public static Thing SearchRegionsForHaulable(Region region, Pawn pawn, System.Predicate<Thing> validator, Danger danger = Danger.Deadly)
        {
            if (region == null || pawn?.Map == null || validator == null) return null;
            Thing found = null;
          
            TraverseParms traverseParams = TraverseParms.For(pawn);

            traverseParams.maxDanger = danger;
            traverseParams.mode = TraverseMode.PassDoors;
            RegionEntryPredicate entryCondition = (Region from, Region r) => r.Allows(traverseParams, isDestination: false);
            RegionProcessor regionProcessor = delegate (Region r)
            {
                List<Thing> list = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));

                for (int i = 0; i < list.Count; i++)
                {
                    Thing thing = list[i];

                    if (validator(thing) &&
                        FeralJobUtility.IsThingAvailableForJobBy(pawn, thing))
                    {
                        found = thing;
                        return true;
                    }
                }
                return false;
            };
            RegionTraverser.BreadthFirstTraverse(region, entryCondition, regionProcessor, 99999);
            return found;
        }

        public static void GiveMemory(Pawn pawn, ThoughtDef thoughtMemory, int maxcount = -1, int stage = -1)
        {
            if (pawn.needs == null || pawn.needs.mood == null || pawn.needs.mood.thoughts == null)
            {
                return;
            }

            Thought_Memory thought_Memory = (Thought_Memory)ThoughtMaker.MakeThought(thoughtMemory);
            thought_Memory.moodPowerFactor = 1;
            if (thought_Memory is Thought_MemorySocial thought_MemorySocial)
            {
                thought_MemorySocial.opinionOffset *= 1;
            }

            if (maxcount >= 0)
            {
                if(pawn.needs.mood.thoughts.memories.NumMemoriesOfDef(thoughtMemory) >= maxcount)
                {
                    return;
                }
            }
            if (stage >= 0)
            {
                thought_Memory.SetForcedStage(stage);
            }

            pawn.needs.mood.thoughts.memories.TryGainMemory(thought_Memory);
        }
        public static void GiveInteractionMemory(Pawn pawn, ThoughtDef thoughtMemory, Pawn other)
        {
            if (pawn.needs == null || pawn.needs.mood == null || pawn.needs.mood.thoughts == null)
            {
                return;
            }

            Thought_Memory thought_Memory = (Thought_Memory)ThoughtMaker.MakeThought(thoughtMemory);
            thought_Memory.moodPowerFactor = 1;
            if (thought_Memory is Thought_MemorySocial thought_MemorySocial)
            {
                thought_MemorySocial.opinionOffset *= 1;
            }

            pawn.needs.mood.thoughts.memories.TryGainMemory(thought_Memory, other);
        }
        public static bool IsMorphing(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            IEnumerable<Hediff> source = from x in pawn.health.hediffSet.hediffs
                                         where
                                         x.TryGetComp<HediffComp_BuildingMorphing>() != null ||
                                         x.TryGetComp<HediffComp_Transformation>() != null
                                         select x;
            if (source.Any())
            {
                return true;
            }

            return false;
        }
        public static bool HasEmbryo(Pawn pawn)
        {

            IEnumerable<Hediff> source = from x in pawn.health.hediffSet.hediffs
                                         where
                                         x.TryGetComp<HediffComp_EmbryoPregnancy>() != null || x.TryGetComp<HediffComp_LarvalAttachment>() != null || x.def == XenoGeneDefOf.XMT_HorrorPregnant
                                         select x;
            if (source.Any())
            {
                return true;
            }

            return false;
        }

        public static bool TriggersOvomorph(Pawn pawn)
        {
            if(IsMorphing(pawn))
            {
                return false;
            }

            return IsHost(pawn);
        }

        public static bool IsAcceptableHost(Pawn pawn)
        {
            //If you are friendly you're not a host.
            if (IsXenomorphFriendly(pawn))
            {
                return false;
            }

            return TriggersOvomorph(pawn);
        }
        public static bool IsHost(Thing thing)
        {
            Pawn pawn = thing as Pawn;

            //If you don't exist you're not a host.
            if (pawn == null)
            {
                return false;
            }

            //If your dead you're not a host.
            if (pawn.Dead)
            {
                return false;
            }

            //If your too small you're not a host.
            if (pawn.BodySize <= 0.58f)
            {
                return false;
            }

            //If your a xenomorph you're not a host.
            if (IsXenomorph(thing))
            {
                return false;
            }

            IEnumerable<BodyPartRecord> source = from x in pawn.health.hediffSet.GetNotMissingParts()
                                                 where
                                                    IsPartHead(x)
                                                 select x;

            //If you have no head you're not a host.
            if (!source.Any())
            {
                return false;
            }

            //If your inorganic you're not a host.
            if (IsInorganic(pawn))
            {
                return false;
            }

            //If you have a larva on your face or an embryo in your body you're not a host.
            if (HasEmbryo(pawn))
            {
                return false;
            }

            return true;
        }

        

        internal static Color GetSkinColorFrom(Pawn pawn)
        {
            Color outcolor = Color.white;

            if (pawn.story != null)
            {
                outcolor = pawn.story.SkinColor;
            }
            else
            {
                PawnKindDef kindDef = pawn.kindDef;

                if (kindDef != null)
                {
                    for (int i = kindDef.lifeStages.Count - 1; i >= 0; i--)
                    {
                        if (kindDef.RaceProps.lifeStageAges[i].minAge < pawn.ageTracker.AgeBiologicalYears)
                        {
                            outcolor = kindDef.lifeStages[i].bodyGraphicData.color;
                            break;
                        }
                    }
                }
            }
            return outcolor;
        }

        internal static bool NotPrey(Pawn target)
        {
            if (target == null || target.RaceProps == null)
            {
                return false;
            }

            return (IsCocooned(target) || IsMorphing(target) || HasEmbryo(target) || IsXenomorph(target) || IsXenomorphFriendly(target) || !target.RaceProps.canBePredatorPrey);
        }

        public static bool IsXenomorphFriendly(Pawn target)
        {
            CompPawnInfo info = target.Info();

            if (info == null)
            {
                return false;
            }

            return info.IsXenomorphFriendly();
        }


        public static bool WitnessAcid(IntVec3 positionHeld, Map mapHeld, float strength, out float bonus, float maxAwareness = 1.0f, float radius = 1.5f, KnowledgeProfileDef knowledgeProfile = null)
        {
            IEnumerable<Pawn> witnesses = GenRadial.RadialDistinctThingsAround(positionHeld, mapHeld, radius, true).OfType<Pawn>();
            bool XenomorphWitness = false;
            bonus = XenoGeneDefOf.XMT_Acid_Utilization?.IsFinished == true ? 1f : 0f;
            foreach (Pawn witness in witnesses)
            {
                if (XMTUtility.IsXenomorph(witness))
                {
                    XenomorphWitness = true;
                }
                else
                {
                    if (witness?.health?.capacities.GetLevel(PawnCapacityDefOf.Sight) > 0)
                    {
                        CompPawnInfo info = witness.Info();
                        if (info != null)
                        {
                            float thisAwareness = KnowledgeUtility.GetAcidRiskKnowledge(witness);
                            if (thisAwareness > bonus)
                            {
                                bonus = thisAwareness;
                            }
                            
                            ResearchUtility.ApplyWitnessExposure(witness, KnowledgeDefOf.XMT_Knowledge_Acid,
                                knowledgeProfile ?? KnowledgeDefOf.XMT_Profile_Acid, strength, positionHeld.GetFirstPawn(mapHeld), 10, acid: true);
                        }
                    }
                }
            }
            return XenomorphWitness;
        }
        public static bool WitnessAcid(IntVec3 positionHeld, Map mapHeld, float strength, float maxAwareness = 1.0f, float radius = 1.5f, KnowledgeProfileDef knowledgeProfile = null)
        {
            if(mapHeld == null)
            {
                return false;
            }

            IEnumerable<Pawn> witnesses = GenRadial.RadialDistinctThingsAround(positionHeld, mapHeld, radius, true).OfType<Pawn>();
            bool XenomorphWitness = false;

            foreach (Pawn witness in witnesses)
            {
                if (XMTUtility.IsXenomorph(witness))
                {
                    XenomorphWitness = true;
                }
                else
                {
                    if (witness?.health?.capacities.GetLevel(PawnCapacityDefOf.Sight) > 0)
                    {
                        CompPawnInfo info = witness.Info();
                        if (info != null)
                        {
                            ResearchUtility.ApplyWitnessExposure(witness, KnowledgeDefOf.XMT_Knowledge_Acid,
                                knowledgeProfile ?? KnowledgeDefOf.XMT_Profile_Acid, strength, positionHeld.GetFirstPawn(mapHeld), 10, acid: true);
                        }
                    }
                }
            }
            return XenomorphWitness;
        }
        public static bool WitnessHorror(IntVec3 positionHeld, Map mapHeld, float strength, float maxAwareness = 1.0f, float radius = 1.5f, KnowledgeProfileDef knowledgeProfile = null)
        {
            IEnumerable<Pawn> witnesses = GenRadial.RadialDistinctThingsAround(positionHeld, mapHeld, radius, true).OfType<Pawn>();
            bool XenomorphWitness = false;
            Thing horror = positionHeld.GetFirstPawn(mapHeld);
 
            foreach (Pawn witness in witnesses)
            {
                if (XMTUtility.IsXenomorph(witness))
                {
                    XenomorphWitness = true;
                }
                else
                {
                    if (witness?.health?.capacities.GetLevel(PawnCapacityDefOf.Sight) > 0)
                    {
                        CompPawnInfo info = witness.Info();
                        if (info != null)
                        {
                            ResearchUtility.ApplyWitnessExposure(witness, KnowledgeDefOf.XMT_Knowledge_Adult,
                                knowledgeProfile ?? KnowledgeDefOf.XMT_Profile_Adult, strength, horror, 10);
                            if (horror != null)
                            {
                                TraumaResponse(horror, info);
                            }
                        }
                    }
                }
            }
            return XenomorphWitness;
        }
        public static bool WitnessOvomorph(IntVec3 positionHeld, Map mapHeld, float strength, float maxAwareness = 1.0f, float radius = 1.5f, KnowledgeProfileDef knowledgeProfile = null)
        {
            IEnumerable<Pawn> witnesses = GenRadial.RadialDistinctThingsAround(positionHeld, mapHeld, radius, true).OfType<Pawn>();
            bool XenomorphWitness = false;

            Thing horror = positionHeld.GetEdifice(mapHeld);

            foreach (Pawn witness in witnesses)
            {
                if (XMTUtility.IsXenomorph(witness))
                {
                    XenomorphWitness = true;
                    GiveMemory(witness, ThoughtDefOf.BabyBorn);
                }
                else
                {
                    if (witness?.health?.capacities.GetLevel(PawnCapacityDefOf.Sight) > 0)
                    {
                        CompPawnInfo info = witness.Info();
                        if (info != null)
                        {
                            ResearchUtility.ApplyWitnessExposure(witness, KnowledgeDefOf.XMT_Knowledge_Ovomorph,
                                knowledgeProfile ?? KnowledgeDefOf.XMT_Profile_Ovomorph, strength, horror, 10);
                            if (horror != null)
                            {
                                TraumaResponse(horror, info);
                            }
                        }
                    }
  
                }
            }
            return XenomorphWitness;
        }

        public static bool WitnessLarva(IntVec3 positionHeld, Map mapHeld, float strength, float maxAwareness = 1.0f, float radius = 1.5f, KnowledgeProfileDef knowledgeProfile = null)
        {
            return WitnessLarva(positionHeld, mapHeld, strength, true, maxAwareness, radius, knowledgeProfile);
        }

        public static bool WitnessLarva(IntVec3 positionHeld, Map mapHeld, float strength, bool allowTraumaResponse,
            float maxAwareness = 1.0f, float radius = 1.5f, KnowledgeProfileDef knowledgeProfile = null)
        {
            IEnumerable<Pawn> witnesses = GenRadial.RadialDistinctThingsAround(positionHeld, mapHeld, radius, true).OfType<Pawn>();
            bool XenomorphWitness = false;
            Thing horror = positionHeld.GetFirstPawn(mapHeld);

            foreach (Pawn witness in witnesses)
            {
                if (witness == null)
                {
                    continue;
                }

                if (IsXenomorph(witness))
                {
                    XenomorphWitness = true;
                    GiveMemory(witness, HorrorMoodDefOf.XMT_LarvaWitnessed);
                }
                else
                {
                    if (witness?.health?.capacities.GetLevel(PawnCapacityDefOf.Sight) > 0)
                    {
                        CompPawnInfo info = witness.Info();
                        if (info != null)
                        {
                            ResearchUtility.ApplyWitnessExposure(witness, KnowledgeDefOf.XMT_Knowledge_Larva,
                                knowledgeProfile ?? KnowledgeDefOf.XMT_Profile_Larva, strength, horror, 10);
                            if (horror != null && allowTraumaResponse)
                            {
                                TraumaResponse(horror, info);
                            }
                        }
                    }
                }
            }
            return XenomorphWitness;
        }
        public static bool WitnessLarva(IntVec3 positionHeld, Map mapHeld, float strength, out float bonus, float maxAwareness = 1.0f, float radius = 1.5f, KnowledgeProfileDef knowledgeProfile = null)
        {
            bonus = 0;
            IEnumerable<Pawn> witnesses = GenRadial.RadialDistinctThingsAround(positionHeld, mapHeld, radius, true).OfType<Pawn>();
            bool XenomorphWitness = false;
            Thing horror = positionHeld.GetFirstPawn(mapHeld);

            foreach (Pawn witness in witnesses)
            {
                if(witness == null)
                {
                    continue;
                }

                if(witness.Downed)
                {
                    continue;
                }

                if (IsXenomorph(witness))
                {
                    XenomorphWitness = true;
                    GiveMemory(witness, HorrorMoodDefOf.XMT_LarvaWitnessed);
                }
                else
                {
                    if (witness?.health?.capacities.GetLevel(PawnCapacityDefOf.Sight) > 0)
                    {
                        CompPawnInfo info = witness.Info();
                        if (info != null)
                        {
                            float thisAwareness = KnowledgeUtility.GetEffectiveKnowledge(witness, KnowledgeDefOf.XMT_Knowledge_Larva);
                            if (thisAwareness > bonus)
                            {
                                bonus = thisAwareness;
                            }
                            ResearchUtility.ApplyWitnessExposure(witness, KnowledgeDefOf.XMT_Knowledge_Larva,
                                knowledgeProfile ?? KnowledgeDefOf.XMT_Profile_Larva, strength, horror, 1);
                            if (horror != null)
                            {
                                TraumaResponse(horror, info);
                            }
                        }
                    }
                }
            }
            return XenomorphWitness;
        }

        public static bool TransformPawnIntoPawn(Pawn target, PawnKindDef targetDef, out Pawn result, Pawn instigator = null, long? biologicalAgeTicksOverride = null)
        {
            result = null;
            if (PreservedBuildingUtility.IsBeingDisposed(target) || HorrorPregnancyUtility.ShouldDeferMotherTransformation(target)) return false;
            if (targetDef == null)
            {
                return false;
            }

            if (target == null)
            {
                return false;
            }
            HorrorGenePayload genePayload = BioUtility.CaptureHorrorGenePayload(target);
            List<ThingDef> removedHediffDrops = new List<ThingDef>();
            //Log.Message("began transforming "+ target + " into " + targetDef);
            PawnGenerationRequest request = new PawnGenerationRequest(
                targetDef, faction: target.Faction, PawnGenerationContext.PlayerStarter, null, true, false, true, false, false, 0, false, true, false, false, false, false, false, false, true, 0, 0, null, 0, null, null, null, null, 0, target.ageTracker.AgeBiologicalYears, target.ageTracker.AgeChronologicalYears);
            if (biologicalAgeTicksOverride.HasValue)
            {
                request.FixedBiologicalAge = (float)biologicalAgeTicksOverride.Value / GenDate.TicksPerYear;
            }

            request.FixedGender = ResolveTransformationGender(target, targetDef);
            //Log.Message("completed request generation for " + target + " into " + targetDef);
            Pawn NewPawn = PawnGenerator.GeneratePawn(request);

            if (NewPawn == null)
            {
                return false;
            }

            //Log.Message("generated pawn " + NewPawn );
            if (NewPawn != null)
            {
                
                NewPawn.ageTracker.DebugSetAge(biologicalAgeTicksOverride ?? target.ageTracker.AgeBiologicalTicks);

                if (NewPawn.BodySize < target.BodySize)
                {
                    if (target.Spawned)
                    {
                        ThingDef meat = NewPawn.RaceProps.meatDef;
                        if(meat == null)
                        {
                            meat = ThingDefOf.Meat_Human;
                        }

                        int excessAmount = Mathf.FloorToInt((target.GetStatValue(StatDefOf.Mass) - NewPawn.GetStatValue(StatDefOf.Mass)) / meat.BaseMass);
                        int sourceButcherYield = Mathf.Max(0, Mathf.FloorToInt(target.GetStatValue(StatDefOf.MeatAmount)));
                        excessAmount = Mathf.Min(excessAmount, sourceButcherYield);
                        if (excessAmount > 0)
                        {
                            DropAmountThing(meat, excessAmount, target.PositionHeld, target.MapHeld, InternalDefOf.Starbeast_Filth_Resin);
                        }
                    }
                }

                if(target.skills != null)
                {
                    if (NewPawn.skills != null)
                    {
                        for (int i = 0; i < NewPawn.skills.skills.Count; i++)
                        {
                            SkillRecord oldSkill = target.skills.GetSkill(NewPawn.skills.skills[i].def);
                            if (oldSkill != null)
                            {
                                NewPawn.skills.skills[i].passion = oldSkill.passion;
                                NewPawn.skills.skills[i].levelInt = oldSkill.levelInt;
                                NewPawn.skills.skills[i].xpSinceLastLevel = oldSkill.xpSinceLastLevel;
                                NewPawn.skills.skills[i].xpSinceMidnight = oldSkill.xpSinceMidnight;
                            }
                        }
                    }
                }

                if(target.story != null)
                {
                    if(NewPawn.story != null)
                    {
                        NewPawn.Name = target.Name;
                        NewPawn.story.skinColorOverride = target.story.SkinColor;

                        NewPawn.story.HairColor = target.story.HairColor;

                        NewPawn.story.Childhood = target.story.Childhood;
                        NewPawn.story.Adulthood = target.story.Adulthood;
                        NewPawn.story.birthLastName = target.story.birthLastName;
                        NewPawn.story.favoriteColor = target.story.favoriteColor;

                        List<Trait> removeList = NewPawn.story.traits.allTraits.ToList();
                        foreach (Trait junk in removeList)
                        {
                            NewPawn.story.traits.RemoveTrait(junk);
                        }

                        foreach (Trait oldTrait in target.story.traits.allTraits)
                        {
                            if (oldTrait.sourceGene == null)
                            {
                                Trait newTrait = oldTrait;
                                NewPawn.story.traits.GainTrait(newTrait);
                            }
                        }
                    }
                }
                else
                {
                    if(NewPawn.story != null)
                    {

                    }
                    else
                    {
                        NewPawn.Name = target.Name;
                    }
                }

                if (target?.health.hediffSet != null)
                {
                    List<Hediff> hediffList = target.health.hediffSet.hediffs.ToList();
                    foreach (Hediff hediff in hediffList)
                    {

                        HediffComp_Transformation transformer = hediff.TryGetComp<HediffComp_Transformation>();
                        if (transformer != null)
                        {
                            //no infinite mutation hediff catastrophe.
                            continue;
                        }

                        if (hediff is Hediff_CryptimorphicExposure exposure && exposure.persistentInfluence > 0f)
                        {
                            var successorExposure = CryptimorphicInfluence.Exposure(NewPawn, true);
                            successorExposure.persistentInfluence = exposure.persistentInfluence;
                            successorExposure.discovered = exposure.discovered;
                            continue;
                        }
                        BodyPartRecord sourcePart = hediff.Part;
                        if (sourcePart == null)
                        {
                            continue;
                        }

                        BodyPartDef partDef = sourcePart.def;
                        string label = sourcePart.Label;
                        BodyPartRecord newPart = FindCorrespondingBodyPart(target, NewPawn, sourcePart, partDef, label);
                        if (newPart != null)
                        {
                            //RJW part stupidity;
                            switch(newPart.Label)
                            {
                                case "genitals":
                                case "chest":
                                case "anus":
                                    continue;
                            }

                            // Missing-part and added-part hediffs make their descendants unavailable.
                            // The source can contain redundant descendant hediffs, so revalidate after
                            // every addition rather than asking AddHediff to attach to a missing part.
                            if (NewPawn.health.hediffSet.PartIsMissing(newPart))
                            {
                                continue;
                            }

                            Hediff newHediff = HediffMaker.MakeHediff(hediff.def, NewPawn, newPart);
                            NewPawn.health.AddHediff(newHediff, newHediff.Part);
                            newHediff.Severity = hediff.Severity;
                            
                            continue;
                        }
                        ThingDef thingToSpawn = hediff.def.spawnThingOnRemoved;
                        if (thingToSpawn != null)
                        {
                            removedHediffDrops.Add(thingToSpawn);
                            continue;
                        }                        
                    }
                }

                BioUtility.TryApplyHorrorGenePayload(NewPawn, genePayload);

                /*
                if(target.records != null)
                {
                    if(NewPawn.records != null)
                    {
                        Log.Message(NewPawn + " copying records");
                        //NewPawn.records.AddTo(RecordDefOf.)
                        IEnumerable<RecordDef> allRecords = DefDatabase<RecordDef>.AllDefs;

                        foreach (RecordDef record in allRecords)
                        {
                            NewPawn.records.AddTo(record, target.records.GetValue(record));
                        }
                    }
                }
                */

                /*
                 * Find.PlayLog.AllEntries.Where((LogEntry e) => e.Concerns(pawn)).ToArray()
                 */
            }

            if (XMTSettings.LogBiohorror)
            {
                Log.Message("[XMT][Biohorror][PawnTransformation] finished assigning source state to " + NewPawn);
            }

            NewPawn = TrySpawnPawnFromTarget(NewPawn, target) as Pawn;

            if (NewPawn == null)
            {
                return false;
            }

            if (!PawnTransformationIdentityUtility.TryTransferAndRetire(target, NewPawn, out _))
            {
                return false;
            }

            PlaceRemovedHediffDrops(NewPawn, removedHediffDrops);
            result = NewPawn;
            return true;
        }

        internal static Gender? ResolveTransformationGender(Pawn source, PawnKindDef targetDef)
        {
            RaceProperties race = targetDef?.race?.race;
            if (race == null)
            {
                return null;
            }
            if (!race.hasGenders)
            {
                return Gender.None;
            }

            bool maleAllowed = true;
            bool femaleAllowed = true;
            if (targetDef.race is ThingDef_AlienRace alienRace)
            {
                float maleProbability = alienRace.alienRace.generalSettings.maleGenderProbability;
                maleAllowed = maleProbability > 0f;
                femaleAllowed = maleProbability < 1f;
            }

            bool IsAllowed(Gender gender)
            {
                return gender == Gender.Male && maleAllowed
                    || gender == Gender.Female && femaleAllowed;
            }

            if (targetDef.fixedGender.HasValue && IsAllowed(targetDef.fixedGender.Value))
            {
                return targetDef.fixedGender.Value;
            }
            if (source != null && IsAllowed(source.gender))
            {
                return source.gender;
            }
            if (femaleAllowed && !maleAllowed)
            {
                return Gender.Female;
            }
            if (maleAllowed && !femaleAllowed)
            {
                return Gender.Male;
            }

            // A gendered race accepting both vanilla genders can choose normally when
            // the source is genderless or carries a value the destination cannot use.
            return null;
        }

        private static BodyPartRecord FindCorrespondingBodyPart(
            Pawn source,
            Pawn successor,
            BodyPartRecord sourcePart,
            BodyPartDef destinationPartDef,
            string destinationLabel)
        {
            if (sourcePart == null || destinationPartDef == null || destinationLabel.NullOrEmpty())
            {
                return null;
            }

            List<BodyPartRecord> sourceMatches = source.def.race.body.AllParts
                .Where(part => part.def == sourcePart.def && part.Label == sourcePart.Label)
                .ToList();
            int occurrence = sourceMatches.IndexOf(sourcePart);
            if (occurrence < 0)
            {
                occurrence = 0;
            }

            List<BodyPartRecord> destinationMatches = successor.def.race.body.AllParts
                .Where(part => part.def == destinationPartDef && part.Label == destinationLabel)
                .ToList();

            // Human, hybrid, starbeast, and royal bodies use different brain defs.
            // Treat their single consciousness source as the same anatomical role so
            // mechlinks and other brain implants survive otherwise-compatible forms.
            if (destinationMatches.Count == 0
                && (sourcePart.def == ExternalDefOf.Brain
                    || sourcePart.def.tags?.Contains(BodyPartTagDefOf.ConsciousnessSource) == true))
            {
                destinationMatches = successor.def.race.body.AllParts
                    .Where(part => part.def.tags?.Contains(BodyPartTagDefOf.ConsciousnessSource) == true)
                    .ToList();
                occurrence = 0;
            }

            return occurrence < destinationMatches.Count ? destinationMatches[occurrence] : null;
        }

        private static void PlaceRemovedHediffDrops(Pawn successor, IEnumerable<ThingDef> dropDefs)
        {
            if (successor == null || dropDefs == null)
            {
                return;
            }

            foreach (ThingDef dropDef in dropDefs)
            {
                if (dropDef == null)
                {
                    continue;
                }

                Thing drop = null;
                try
                {
                    drop = ThingMaker.MakeThing(dropDef);
                    Map map = successor.MapHeld;
                    if (map != null && GenPlace.TryPlaceThing(drop, successor.PositionHeld, map, ThingPlaceMode.Near))
                    {
                        continue;
                    }

                    if (successor.inventory?.innerContainer != null
                        && successor.inventory.innerContainer.TryAdd(drop, false))
                    {
                        continue;
                    }

                    Log.Warning("[XMT][PawnTransformation] Could not place removed implant " + dropDef.defName
                        + " for " + successor + ".");
                }
                catch (Exception exception)
                {
                    Log.Warning("[XMT][PawnTransformation] Failed to create or place removed implant "
                        + dropDef.defName + " for " + successor + ": " + exception.Message);
                }
                finally
                {
                    if (drop != null && drop.holdingOwner == null && !drop.Spawned && !drop.Destroyed)
                    {
                        drop.Destroy(DestroyMode.Vanish);
                    }
                }
            }
        }

        internal static bool TransformThingIntoThing(Thing target, ThingDef targetDef, out Thing result, Pawn instigator = null, Pawn contributor = null)
        {
            result = null;
            if (targetDef == null || target == null || target.Destroyed || target is Pawn)
            {
                return false;
            }

            if (!target.Spawned || target.Map == null)
            {
                return false;
            }

            Map map = target.Map;
            IntVec3 position = target.Position;
            Rot4 rotation = target.Rotation;
            Thing newThing = null;
            CompPawnPreservation sourcePreservation = target.TryGetComp<CompPawnPreservation>();
            List<PreservedPawn> transferredContributors = new List<PreservedPawn>();
            HorrorGenePayload genePayload = BioUtility.CaptureHorrorGenePayload(target);

            try
            {
                newThing = ThingMaker.MakeThing(targetDef);
                if (newThing == null)
                {
                    return false;
                }
                if (newThing is Building) newThing.SetFaction(instigator?.Faction ?? target.Faction);

                BioUtility.TryApplyHorrorGenePayload(newThing, genePayload);
                if (newThing is XMTBase_Building building)
                {
                    building.InheritOrigins(target as XMTBase_Building);
                }

                target.DeSpawn(DestroyMode.Vanish);
                Thing spawnedThing = GenSpawn.Spawn(newThing, position, map, rotation, WipeMode.VanishOrMoveAside);
                if (spawnedThing == null)
                {
                    GenSpawn.Spawn(target, position, map, rotation, WipeMode.VanishOrMoveAside);
                    newThing.Destroy(DestroyMode.Vanish);
                    return false;
                }

                CompPawnPreservation destinationPreservation = newThing.TryGetComp<CompPawnPreservation>();
                if (sourcePreservation != null && destinationPreservation != null)
                    foreach (PreservedPawn entry in sourcePreservation.Entries.ToList())
                    {
                        if (!sourcePreservation.TransferEntry(entry, destinationPreservation))
                            throw new InvalidOperationException("Could not transfer preserved contributor.");
                        transferredContributors.Add(entry);
                    }
                if (contributor != null)
                {
                    if (destinationPreservation == null || !destinationPreservation.TryPreserve(contributor, newThing.def))
                        throw new InvalidOperationException("Could not preserve merging contributor.");
                    (newThing as XMTBase_Building)?.RecordOrigin(contributor);
                }
                // Source-dependent conversion runs after placement and ownership transfer.
                // Its map remains available explicitly even though the source was despawned.
                if (newThing is XMTBase_Building initialized) initialized.TransformedFrom(target, instigator);
                if (sourcePreservation != null && destinationPreservation == null) sourcePreservation.DisposeContents(null, map);
                if (target is XMTBase_Building retired) retired.SuppressTransformationEffects = true;
                target.Destroy(DestroyMode.Vanish);
                result = spawnedThing;
                return true;
            }
            catch (Exception exception)
            {
                Log.Error("Failed to transform " + target + " into " + targetDef + ": " + exception);

                if (target.Destroyed && newThing?.Spawned == true)
                {
                    result = newThing;
                    return true;
                }

                CompPawnPreservation rollbackPreservation = newThing?.TryGetComp<CompPawnPreservation>();
                if (rollbackPreservation != null && sourcePreservation != null)
                    foreach (PreservedPawn entry in transferredContributors)
                        rollbackPreservation.TransferEntry(entry, sourcePreservation);
                if (contributor != null && rollbackPreservation?.Contains(contributor) == true)
                {
                    PreservedPawn entry = rollbackPreservation.Entries.First(item => item.pawn == contributor);
                    rollbackPreservation.GetDirectlyHeldThings().TryDrop(entry.Contents, position, map, ThingPlaceMode.Near, out _);
                    rollbackPreservation.Forget(entry);
                }

                if (newThing != null && !newThing.Destroyed)
                {
                    if (newThing is XMTBase_Building failed) failed.SuppressTransformationEffects = true;
                    if (newThing.Spawned)
                    {
                        newThing.DeSpawn(DestroyMode.Vanish);
                    }
                    newThing.Destroy(DestroyMode.Vanish);
                }

                if (!target.Destroyed && !target.Spawned)
                {
                    GenSpawn.Spawn(target, position, map, rotation, WipeMode.VanishOrMoveAside);
                }

                return false;
            }
        }

        public static bool TransformThingIntoPawn(Thing target, PawnKindDef targetDef, out Pawn result, Pawn instigator = null, long? biologicalAgeTicksOverride = null)
        {
            result = null;
            if (target == null || target.Destroyed || target is Pawn)
            {
                return false;
            }

            if (targetDef?.race?.race == null)
            {
                return false;
            }

            if (!target.Spawned || target.Map == null)
            {
                return false;
            }

            Map map = target.Map;
            IntVec3 position = target.Position;
            Rot4 rotation = target.Rotation;
            Faction faction = target.Faction ?? instigator?.Faction;
            Pawn newPawn = null;
            HorrorGenePayload genePayload = BioUtility.CaptureHorrorGenePayload(target);

            try
            {
                PawnGenerationRequest request = new PawnGenerationRequest(targetDef, faction);
                if (biologicalAgeTicksOverride.HasValue)
                {
                    request.FixedBiologicalAge = (float)biologicalAgeTicksOverride.Value / GenDate.TicksPerYear;
                }
                if (!targetDef.race.race.hasGenders)
                {
                    request.FixedGender = Gender.None;
                }
                else if (targetDef.race is ThingDef_AlienRace alienRace &&
                         alienRace.alienRace.generalSettings.maleGenderProbability <= 0f)
                {
                    request.FixedGender = Gender.Female;
                }

                newPawn = PawnGenerator.GeneratePawn(request);
                if (newPawn == null)
                {
                    return false;
                }

                BioUtility.TryApplyHorrorGenePayload(newPawn, genePayload);

                if (target is XMTBase_Building biologicalBuilding)
                {
                    biologicalBuilding.TransformingInto(newPawn, instigator);
                }

                if (biologicalAgeTicksOverride.HasValue)
                {
                    newPawn.ageTracker.DebugSetAge(biologicalAgeTicksOverride.Value);
                }

                target.DeSpawn(DestroyMode.Vanish);
                Pawn spawnedPawn = GenSpawn.Spawn(newPawn, position, map, rotation, WipeMode.VanishOrMoveAside) as Pawn;
                if (spawnedPawn == null)
                {
                    GenSpawn.Spawn(target, position, map, rotation, WipeMode.VanishOrMoveAside);
                    newPawn.Discard();
                    return false;
                }

                target.Destroy(DestroyMode.Vanish);
                result = spawnedPawn;
                return true;
            }
            catch (Exception exception)
            {
                Log.Error("Failed to transform " + target + " into " + targetDef + ": " + exception);

                if (newPawn != null && !newPawn.Destroyed)
                {
                    if (newPawn.Spawned)
                    {
                        newPawn.DeSpawn(DestroyMode.Vanish);
                    }
                    newPawn.Discard();
                }

                if (!target.Destroyed && !target.Spawned)
                {
                    GenSpawn.Spawn(target, position, map, rotation, WipeMode.VanishOrMoveAside);
                }

                return false;
            }
        }

        internal static bool TransformPawnIntoThing(Pawn target, ThingDef targetDef, out Thing result, Pawn instigator = null, IntVec3? exactPosition = null, Rot4? exactRotation = null)
        {
            result = null;
            if (PreservedBuildingUtility.IsBeingDisposed(target) || HorrorPregnancyUtility.ShouldDeferMotherTransformation(target)) return false;
            if (targetDef == null || target == null || target.Destroyed || target.Discarded) return false;
            Thing original = target.Dead ? target.Corpse : target;
            Map map = original?.MapHeld;
            if (map == null) return false;
            IntVec3 position = original.PositionHeld;
            ThingOwner originalOwner = original.holdingOwner;
            Thing replacement = null;
            try
            {
                replacement = ThingMaker.MakeThing(targetDef);
                if (replacement is Building) replacement.SetFaction(instigator?.Faction ?? target.Faction);
                BioUtility.TryApplyHorrorGenePayload(replacement, BioUtility.CaptureHorrorGenePayload(target));
                bool placed = exactPosition.HasValue ? GenSpawn.Spawn(replacement, exactPosition.Value, map, exactRotation ?? Rot4.North) != null
                    : GenPlace.TryPlaceThing(replacement, position, map, ThingPlaceMode.Near);
                if (!placed)
                    throw new InvalidOperationException("Could not place transformed building.");
                if (replacement is XMTBase_Building building) building.InitializeFrom(target, instigator);
                CompPawnPreservation preservation = replacement.TryGetComp<CompPawnPreservation>();
                if (preservation != null)
                {
                    if (!preservation.TryPreserve(target, replacement.def))
                        throw new InvalidOperationException("Could not preserve transformed pawn.");
                    preservation.HealToBuildingHealth();
                }
                else
                {
                    // This source is retired immediately; do not remove a vital implant and
                    // invoke death/mutation callbacks halfway through a non-preserving conversion.
                    PreservedBuildingUtility.ExtractImplants(target, position, map, false);
                    if (original is Corpse corpse) corpse.Destroy();
                    else target.Destroy();
                }
                result = replacement;
                return true;
            }
            catch (Exception exception)
            {
                Log.Error("[XMT] Pawn-to-building transformation failed: " + exception);
                CompPawnPreservation preservation = replacement?.TryGetComp<CompPawnPreservation>();
                if (preservation?.Contains(target) == true)
                {
                    PreservedPawn entry = preservation.Entries.First(item => item.pawn == target);
                    preservation.GetDirectlyHeldThings().Remove(entry.Contents);
                    preservation.Forget(entry);
                    if (originalOwner != null) originalOwner.TryAddOrTransfer(original, false);
                    else GenPlace.TryPlaceThing(original, position, map, ThingPlaceMode.Near);
                }
                if (replacement != null)
                {
                    if (replacement is XMTBase_Building failed) failed.SuppressTransformationEffects = true;
                    replacement.Destroy();
                }
                return false;
            }
        }
        public static bool PlayerXenosOnMap(Map localMap)
        {
            return XMTHiveUtility.PlayerXenosOnMap(localMap);
        }

        public static void DropAmountThing(ThingDef thingDef, int stackTotal, IntVec3 position, Map targetMap, ThingDef Filth = null)
        {
            if (stackTotal <= 0)
            {
                return;
            }

            if (thingDef == null || targetMap == null || !position.InBounds(targetMap))
            {
                Log.Warning("[XMT] Cannot drop " + stackTotal + " of " + thingDef + " at " + position + ": invalid def, map, or position.");
                return;
            }

            Thing thing = ThingMaker.MakeThing(thingDef);
            thing.stackCount = stackTotal;

            Action<Thing, int> onPlaced = null;
            if (Filth != null)
            {
                HashSet<IntVec3> filthCells = new HashSet<IntVec3>();
                onPlaced = (placedThing, count) =>
                {
                    if (filthCells.Add(placedThing.Position))
                    {
                        FilthMaker.TryMakeFilth(placedThing.Position, targetMap, Filth);
                    }
                };
            }

            // Vanilla merges compatible stacks and splits oversized quantities across nearby cells.
            if (!GenPlace.TryPlaceThing(thing, position, targetMap, ThingPlaceMode.Near, onPlaced))
            {
                Log.Warning("[XMT] Could only place " + (stackTotal - thing.stackCount) + " of " + stackTotal + " " + thingDef + " near " + position + "; " + thing.stackCount + " remain unplaced.");
            }
        }

        public static bool IsQueen(Pawn p)
        {
            CompQueen compQueen = p.GetComp<CompQueen>();
            if (compQueen == null)
            {
                return false;
            }

            return true;
        }

        public static bool NoQueenPresent()
        {
            return Queen == null;
        }

        public static bool QueenPresent()
        {
            return Queen != null;
        }

        public static bool PlayerFactionIsCryptimorph()
        {
            Faction player = Faction.OfPlayerSilentFail;
            return player != null && player.def == InternalDefOf.XMT_PlayerHive;
        }

        public static bool QueenIsPlayer()
        {
            if(Queen == null)
            {
                return false;
            }

            return Queen.Faction == Faction.OfPlayer;
        }

        public static void QueenDied(Pawn p)
        {
            if(Queen == p)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("[XMT][World] Queen reported as dead: " + p);
                }
                Queen = null;
            }
        }

        public static void DeclareQueen(Pawn p) => HiveQueenUtility.Register(p);

        internal static bool IsHiveBuilding(ThingDef buildDef)
        {
            if (buildDef == null)
            {
                return false;
            }

            if(buildDef?.designationCategory?.defName == "XMT_Hive")
            {
                return true;
            }

            return false;
        }

        internal static bool HasQueenWithEvolution(RoyalEvolutionDef EvoDef)
        {
            if(NoQueenPresent())
            {
                return false;
            }

            CompQueen compQueen = Queen.GetComp<CompQueen>();

            if(compQueen == null)
            {
                return false;
            }

            return compQueen.HasEvolutionFeature(EvoDef);
        }

        internal static BackstoryDef GetChildBackstory(float maturity, Map map, Faction faction)
        {
            if(maturity > 1)
            {
                return XenoStoryDefOf.StarbeastChildDeveloped10;
            }

            if(maturity < 1)
            {
                return XenoStoryDefOf.StarbeastChildPremature15;
            }

            if(XMTHiveUtility.TotalHivePopulation(map) < 1)
            {
                if(faction == null)
                {
                    return XenoStoryDefOf.StarbeastChildAlone16;
                }

                if(faction.IsPlayer)
                {
                    if(map.mapPawns.ColonistCount < 1)
                    {
                        return XenoStoryDefOf.StarbeastChildAlone16;
                    }
                    else
                    {
                        return XenoStoryDefOf.StarbeastChildHuman17;
                    }
                }
            }


            return XenoStoryDefOf.StarbeastChildHive18;
        }

        internal static CompQueen GetQueenComp()
        {
            if(Queen == null)
            {
                return null;
            }

            return Queen.GetComp<CompQueen>();
        }
        internal static Pawn GetQueen()
        {
            return Queen;
        }

        internal static bool IsAcidBlooded(Pawn pawn)
        {
            CompAcidBlood compAcidBlood = pawn.GetComp<CompAcidBlood>();
            if(compAcidBlood == null)
            {
                return false;
            }

            return true;
        }

        internal static float GetFinalBodySize(Pawn pawn)
        {
            float finalSize = pawn.BodySize;

            if (ExternalDefOf.SM_BodySizeOffset != null)
            {
                float BnSbodySizeOffset = pawn.GetStatValue(ExternalDefOf.SM_BodySizeOffset);
                float BnSbodySizeFactor = pawn.GetStatValue(ExternalDefOf.SM_BodySizeMultiplier);

                finalSize += BnSbodySizeOffset;
                finalSize *= BnSbodySizeFactor;
            }
            else
            {
                CompBuildingOrganism slumberer = pawn.GetComp<CompBuildingOrganism>();

                if (slumberer != null && slumberer.BodySize > 0)
                {
                    finalSize = slumberer.BodySize;
                }
            }
            return finalSize;
        }

        internal static bool PawnLikesTarget(Pawn pawn, Pawn target)
        {
            return pawn.relations.OpinionOf(target) > XMTSettings.MinimumOpinionForHiveFriend;
        }

        internal static bool IsPartHead(BodyPartRecord x)
        {
            return x.def == BodyPartDefOf.Head
            || x.def == ExternalDefOf.InsectHead
            || x.def == ExternalDefOf.SnakeHead
            || x.def == ExternalDefOf.HeadWithEarHoles;
        }

        internal static void SabotageThing(Thing target, Pawn pawn)
        {
            CompBreakdownable breakdownable = target.TryGetComp<CompBreakdownable>();
            FilthMaker.TryMakeFilth(target.Position, target.Map, InternalDefOf.Starbeast_Filth_Resin);
            if (breakdownable != null)
            {
                breakdownable.DoBreakdown();
            }
            else if (XMTSabotageReplacementUtility.TryGetReplacement(target.def, out XMT_SabotageReplacementPair replacement) && replacement.replacementThing != null)
            {
                GenSpawn.Spawn(replacement.replacementThing, target.Position, target.Map);
            }
            else
            {
                DamageInfo dinfo = new DamageInfo(DamageDefOf.Cut, 16, instigator: pawn);
                target.Kill(dinfo);
            }

            if (pawn.Faction != null && pawn.Faction.IsPlayer)
            {
                pawn.needs.joy.GainJoy(0.1f, ExternalDefOf.Gaming_Dexterity);
            }
        }

        internal static void TraumaResponse(Thing horror, CompPawnInfo witnessInfo)
        {
            if (witnessInfo.parent is Pawn witness)
            {
                if(!witness.Awake() || witness.Downed)
                {
                    return;
                }

                if (KnowledgeUtility.IsObsessed(witness, out float awareness))
                {
                    return;
                }

                if(XMTUtility.IsXenomorph(witness))
                {
                    return;
                }

                bool violent = Rand.Chance(witness.RaceProps.manhunterOnDamageChance);

                float moodChance = (witness.needs.mood != null)? witness.needs.mood.CurLevelPercentage : 0.5f;
                bool traumaBreak = Rand.Chance((awareness / 4) - moodChance);

                if(witness.story != null)
                {
                    if(witness.story.traits.HasTrait(TraitDefOf.Wimp) || witness.story.DisabledWorkTagsBackstoryTraitsAndGenes.HasFlag(WorkTags.Violent))
                    {
                        violent = false;
                    }
                }

                if (witness.Drafted && witnessInfo.parent.Faction == horror.Faction)
                {
                    if (traumaBreak)
                    {
                        if (violent)
                        {
                            Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, horror);
                            witness.jobs.StartJob(job, JobCondition.InterruptForced);
                        }
                        else
                        {
                            witness.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_OwnRoom, "", forced: true, forceWake: true, false);
                        }
                    }
                    
                }
                else
                {
                    if (traumaBreak)
                    {
                        if (violent)
                        {
                            Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, horror);
                            witness.jobs.StartJob(job, JobCondition.InterruptForced);
                        }
                        else
                        {
                            witness.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee, "", forced: true, forceWake: true, false);
                        }
                    }
                }
                
            }
        }

        internal static void ThreatResponse(Thing victim, CompPawnInfo aggressorInfo, float radius = 5f)
        {
            if (aggressorInfo == null)
            {
                return;
            }

            if(aggressorInfo.parent == null || aggressorInfo.parent.MapHeld == null)
            {
                return;
            }

            if (!aggressorInfo.IsXenomorphFriendly())
            {
                IntVec3 eventPosition = (victim != null) ? victim.PositionHeld : aggressorInfo.parent.PositionHeld;
                XMTHiveUtility.NotifyLocalThreatStimulus(victim, aggressorInfo.parent, eventPosition, aggressorInfo.parent.MapHeld, radius);
                IEnumerable <IntVec3> cells = GenRadial.RadialCellsAround(eventPosition, radius, true);

                foreach (IntVec3 cell in cells)
                {
                    if (!cell.InBounds(aggressorInfo.parent.MapHeld))
                        continue;
                    Pawn witness = cell.GetFirstPawn(aggressorInfo.parent.MapHeld);

                    if(witness == null)
                    {
                        continue;
                    }

                    if(witness == victim)
                    {
                        if(victim.Faction != null)
                        {
                            continue;
                        }
                    }
                    
                    if (witness.mindState.mentalStateHandler.InMentalState)
                    {
                        continue;
                    }

                    if(witness.Downed)
                    {
                        continue;
                    }

                    if (IsXenomorph(witness))
                    {
                        if (Verse.AI.Group.LordUtility.GetLord(witness)?.LordJob is LordJob_Nemesis nemesisLord
                            && !nemesisLord.AllowThreatResponse(witness, aggressorInfo.parent)) continue;
                        if (witness.def == InternalDefOf.XMT_Larva)
                        {
                            continue;
                        }

                        if(witness.jobs.curJob != null && aggressorInfo.XenomorphPheromoneValue() > -5f)
                        {
                            if(witness.jobs.curJob.def == XenoWorkDefOf.XMT_AbductHost)
                            {
                                continue;
                            }

                            if (witness.story != null && witness.story.DisabledWorkTagsBackstoryTraitsAndGenes.HasFlag(WorkTags.Violent))
                            {
                                continue;
                            }
                        }

                        if (witness.Drafted)
                        {
                            if(aggressorInfo.parent.Faction == witness.Faction && aggressorInfo.XenomorphPheromoneValue() <= -1f)
                            {
                                Messages.Message("XMT_DefendHive".Translate(witness.LabelShort), MessageTypeDefOf.PositiveEvent);
                                witness.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                            }
                        }
                        else
                        {
                            if (witness.Faction == Faction.OfPlayer)
                            {
                                Messages.Message("XMT_DefendHive".Translate(witness.LabelShort), MessageTypeDefOf.PositiveEvent);
                            }
                            witness.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                        }
                    }
                }
            }
        }

        internal static bool IsCocooned(Pawn pawn)
        {
            if(!pawn.Downed)
            {
                return false;
            }

            if(pawn.InBed())
            {
                return pawn.CurrentBed() is CocoonBase;
            }

            return pawn.health.hediffSet.HasHediff(InternalDefOf.StarbeastCocoon);
        }

        internal static bool IsHostileAndAwareOf(Thing a, Thing b)
        {
            if(a == null || b == null)
            {
                return false;
            }
            if(a is Pawn observer)
            {
                if (observer.Downed)
                {
                    return false;
                }

                CompPawnInfo info = observer.Info();
                
                if(info == null)
                {
                    return false;
                }

                if(KnowledgeUtility.IsObsessed(observer))
                {
                    return false;
                }

                if(a.Faction == b.Faction)
                {
                    return false;
                }

                return KnowledgeUtility.GetEffectiveKnowledge(observer, KnowledgeDefOf.XMT_Knowledge_Adult) > XMTSettings.MinAwarenessAutoAggression || observer.Drafted;

            }
            return false;
        }

        public static bool QueenSuppressesThreatPerception(Pawn observer, Pawn queen)
        {
            
            if (observer == null || queen == null || observer == queen)
            {
                return false;
            }

            if (!observer.Spawned || !queen.Spawned || observer.Dead || queen.Dead || observer.MapHeld != queen.MapHeld)
            {
                return false;
            }

            if (!IsQueen(queen))
            {
                return false;
            }

            if (IsXenomorph(observer))
            {
                return true;
            }

            CompQueen queenComp = queen.GetComp<CompQueen>();
            if (queenComp == null)
            {
                return false;
            }

            bool biologicalSuppression = (observer.HasBrainMutation() || observer.IsHorror())
                && queenComp.HasFunctionalEvolutionFeature(RoyalEvolutionDefOf.Evo_SubjugatorCrest);
            bool inorganicSuppression = IsInorganic(observer)
                && queen.mechanitor != null
                && queenComp.HasFunctionalEvolutionFeature(RoyalEvolutionDefOf.Evo_MechanitorArray);
            if (!biologicalSuppression && !inorganicSuppression)
            {
                return false;
            }

            float range = QueenThreatSuppressionRange(queen, queenComp);
            if (range <= 0f)
            {
                return false;
            }

            return observer.PositionHeld.DistanceToSquared(queen.PositionHeld) <= range * range;
        }

        public static bool ArraySuppressesAutomatedThreat(Thing observer, Pawn queen)
        {
            if (observer == null || queen == null || observer == queen || !observer.Spawned || !queen.Spawned
                || queen.Dead || observer.MapHeld != queen.MapHeld || !IsQueen(queen) || queen.mechanitor == null)
            {
                return false;
            }

            CompQueen queenComp = queen.GetComp<CompQueen>();
            if (queenComp?.HasFunctionalEvolutionFeature(RoyalEvolutionDefOf.Evo_MechanitorArray) != true)
            {
                return false;
            }

            float range = QueenThreatSuppressionRange(queen, queenComp);
            return range > 0f && observer.PositionHeld.DistanceToSquared(queen.PositionHeld) <= range * range;
        }

        private static float QueenThreatSuppressionRange(Pawn queen, CompQueen queenComp)
        {
            float psychicSensitivity = Mathf.Max(0f, queen.GetStatValue(StatDefOf.PsychicSensitivity));
            return EvolutionScalingUtility.Scale(queenComp.SubjugationBaseRange * 1.25f, 0f, 0f, float.MaxValue,
                EvolutionScalingCurve.Proportional, EvolutionScalingRounding.None,
                new EvolutionScalingFactor(psychicSensitivity, 1.25f));
        }

        internal static bool IsSpace(Map map)
        {
            if(map == null)
            {
                return true;
            }

            return map.Biome.inVacuum || map.Biome == ExternalDefOf.OuterSpaceBiome;
        }
    }

    public class GrappleCheckReport
    {
        public Pawn Attacker;
        public Pawn Defender;
        public string BlockedReason;
        public float AttackerStrength;
        public float DefenderStrength;
        public float ModifiedAttackerStrength;
        public float ModifiedDefenderStrength;
        public float BodySizeFactor;
        public float DefenderConsciousFactor;
        public float DefenderStunFactor;
        public float AttackerInvisibleFactor;
        public float ResistChance;
        public float SuccessChance => Mathf.Clamp01(1f - ResistChance);

        public string ToLogString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Grapple check");
            builder.AppendLine("Attacker: " + Attacker);
            builder.AppendLine("Defender: " + Defender);
            if (!BlockedReason.NullOrEmpty())
            {
                builder.AppendLine("Blocked: " + BlockedReason);
                return builder.ToString();
            }
            builder.AppendLine("Attacker strength: " + AttackerStrength.ToString("0.###") + " -> " + ModifiedAttackerStrength.ToString("0.###"));
            builder.AppendLine("Defender strength: " + DefenderStrength.ToString("0.###") + " -> " + ModifiedDefenderStrength.ToString("0.###"));
            builder.AppendLine("Body size factor: " + BodySizeFactor.ToString("0.###"));
            builder.AppendLine("Conscious factor: " + DefenderConsciousFactor.ToString("0.###"));
            builder.AppendLine("Stun factor: " + DefenderStunFactor.ToString("0.###"));
            builder.AppendLine("Invisible attacker factor: " + AttackerInvisibleFactor.ToString("0.###"));
            builder.AppendLine("Resist chance: " + ResistChance.ToStringPercent());
            return builder.ToString();
        }
    }
}
