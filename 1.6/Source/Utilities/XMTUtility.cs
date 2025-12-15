using AlienRace;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;



namespace Xenomorphtype
{
    public class XMTUtility
    {
        private static Pawn Queen
        {
            get
            {

                GameComponent_Xenomorph gameComp = Current.Game.GetComponent<GameComponent_Xenomorph>();

                if(gameComp == null)
                {
                    Log.Warning("No GameComponent_Xenomorph");
                    return null;
                }
                if (gameComp.Queen != null)
                {
                    if (gameComp.QueenInWorld)
                    {
                        if (gameComp.Queen.Faction != null && !gameComp.Queen.Faction.IsPlayer)
                        {
                            gameComp.Queen.Destroy();
                            gameComp.Queen = null;
                        }
                    }

                    if (gameComp.Queen.Dead)
                    {
                        gameComp.Queen = null;
                    }
                }
                return gameComp.Queen;
            }
            set
            {

                GameComponent_Xenomorph gameComp = Current.Game.GetComponent<GameComponent_Xenomorph>();
       
                if (gameComp == null)
                {
                    Log.Warning("No GameComponent_Xenomorph");
                    return;
                }

                if(WorldPawnsUtility.IsWorldPawn(value))
                {
                    return;
                }

                if (value.Dead)
                {
                    return;
                }

                gameComp.Queen = value;
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
                (map.glowGrid.GroundGlowAt(x.parent.Position) < 0.45f) &&
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
                Log.Message(pawn + " is trying to spawn from " + target);
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
                Find.WorldPawns.PassToWorld(pawn);
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

        public static bool IsXenomorph(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            CompPerfectOrganism perfect = pawn.GetComp<CompPerfectOrganism>();

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
                if (pawn.genes.HasActiveGene(ExternalDefOf.VREA_SyntheticBody))
                {
                    return true;
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


        public static float GetDefendGrappleChance(Pawn attacker, Pawn defender)
        {
            if (defender.WorkTagIsDisabled(WorkTags.Violent))
            {
                return 0f;
            }

            float defenderChance = defender.GetStatValue(StatDefOf.MeleeHitChance);
            if (ModsConfig.IdeologyActive)
            {
                if (DarknessCombatUtility.IsOutdoorsAndLit(defender))
                {
                    defenderChance += defender.GetStatValue(StatDefOf.MeleeDodgeChanceOutdoorsLitOffset);
                }
                else if (DarknessCombatUtility.IsOutdoorsAndDark(defender))
                {
                    defenderChance += defender.GetStatValue(StatDefOf.MeleeDodgeChanceOutdoorsDarkOffset);
                }
                else if (DarknessCombatUtility.IsIndoorsAndDark(defender))
                {
                    defenderChance += defender.GetStatValue(StatDefOf.MeleeDodgeChanceIndoorsDarkOffset);
                }
                else if (DarknessCombatUtility.IsIndoorsAndLit(defender))
                {
                    defenderChance += defender.GetStatValue(StatDefOf.MeleeDodgeChanceIndoorsLitOffset);
                }
            }

            if (!defender.Awake())
            {
                defenderChance *= 0.25f;
            }

            if(defender.stances.stunner.Stunned)
            {
                defenderChance *= 0.5f;
            }

            float attackerChance = attacker.GetStatValue(StatDefOf.MeleeHitChance);
            if (ModsConfig.IdeologyActive)
            {
                if (DarknessCombatUtility.IsOutdoorsAndLit(attacker))
                {
                    attackerChance += attacker.GetStatValue(StatDefOf.MeleeDodgeChanceOutdoorsLitOffset);
                }
                else if (DarknessCombatUtility.IsOutdoorsAndDark(attacker))
                {
                    attackerChance += attacker.GetStatValue(StatDefOf.MeleeDodgeChanceOutdoorsDarkOffset);
                }
                else if (DarknessCombatUtility.IsIndoorsAndDark(attacker))
                {
                    attackerChance += attacker.GetStatValue(StatDefOf.MeleeDodgeChanceIndoorsDarkOffset);
                }
                else if (DarknessCombatUtility.IsIndoorsAndLit(attacker))
                {
                    attackerChance += attacker.GetStatValue(StatDefOf.MeleeDodgeChanceIndoorsLitOffset);
                }
            }

            if (attacker.IsPsychologicallyInvisible())
            {
                attackerChance *= 2;
            }

            defenderChance *= (defender.BodySize / attacker.BodySize);

            defenderChance /= attackerChance;

            //Log.Message(" Grapple attempt between " + attacker + " and " + defender + " defense chance: " + defenderChance);
            return defenderChance;

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
        public static Thing SearchRegionsForJellyMakable(Region region, Pawn pawn, CompJellyMaker jellyMaker)
        {
            if(pawn == null)
            {
                return null;
            }
            TraverseParms traverseParams = TraverseParms.For(pawn);

            traverseParams.maxDanger = Danger.Deadly;
            traverseParams.mode = TraverseMode.PassDoors;
            RegionEntryPredicate entryCondition = (Region from, Region r) => r.Allows(traverseParams, isDestination: false);
            Thing found = null;
            RegionProcessor regionProcessor = delegate (Region r)
            {
                List<Thing> list = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));

                for (int i = 0; i < list.Count; i++)
                {
                    Thing thing = list[i];

                    if (jellyMaker.CanMakeIntoJelly(thing) && FeralJobUtility.IsThingAvailableForJobBy(pawn, thing))
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
            IEnumerable<Hediff> source = from x in pawn.health.hediffSet.hediffs
                                         where
                                         x.TryGetComp<HediffComp_BuildingMorphing>() != null
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

            //If you are friendly you're not a host.
            if (IsXenomorphFriendly(pawn))
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

            return (IsMorphing(target) || HasEmbryo(target) || IsXenomorph(target) || IsXenomorphFriendly(target) || !target.RaceProps.canBePredatorPrey);
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


        public static bool WitnessAcid(IntVec3 positionHeld, Map mapHeld, float strength, out float bonus, float maxAwareness = 1.0f, float radius = 1.5f)
        {
            IEnumerable<Pawn> witnesses = GenRadial.RadialDistinctThingsAround(positionHeld, mapHeld, radius, true).OfType<Pawn>();
            bool XenomorphWitness = false;
            bonus = 0;
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
                            float thisAwareness = info.AcidAwareness;
                            if (thisAwareness > bonus)
                            {
                                bonus = thisAwareness;
                            }
                            
                            info.WitnessAcidHorror(strength, maxAwareness);
                        }
                    }
                }
            }
            return XenomorphWitness;
        }
        public static bool WitnessAcid(IntVec3 positionHeld, Map mapHeld, float strength, float maxAwareness = 1.0f, float radius = 1.5f)
        {
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
                            info.WitnessAcidHorror(strength, maxAwareness);
                            ResearchUtility.ProgressCryptobioTech(1, witness);
                        }
                    }
                }
            }
            return XenomorphWitness;
        }
        public static bool WitnessHorror(IntVec3 positionHeld, Map mapHeld, float strength, float maxAwareness = 1.0f, float radius = 1.5f)
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
                            info.WitnessHorror(strength, maxAwareness);
                            ResearchUtility.ProgressCryptobioTech(1, witness);
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
        public static bool WitnessOvomorph(IntVec3 positionHeld, Map mapHeld, float strength, float maxAwareness = 1.0f, float radius = 1.5f)
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
                            info.WitnessOvomorphHorror(strength, maxAwareness);
                            ResearchUtility.ProgressCryptobioTech(1, witness);
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

        public static bool WitnessLarva(IntVec3 positionHeld, Map mapHeld, float strength, float maxAwareness = 1.0f, float radius = 1.5f)
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
                    GiveMemory(witness, ThoughtDefOf.BabyGiggledSocial);
                }
                else
                {
                    if (witness?.health?.capacities.GetLevel(PawnCapacityDefOf.Sight) > 0)
                    {
                        CompPawnInfo info = witness.Info();
                        ResearchUtility.ProgressCryptobioTech(1, witness);
                        if (info != null)
                        {
                            info.WitnessLarvaHorror(strength, maxAwareness);
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
        public static bool WitnessLarva(IntVec3 positionHeld, Map mapHeld, float strength, out float bonus, float maxAwareness = 1.0f, float radius = 1.5f)
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
                }
                else
                {
                    if (witness?.health?.capacities.GetLevel(PawnCapacityDefOf.Sight) > 0)
                    {
                        CompPawnInfo info = witness.Info();
                        ResearchUtility.ProgressCryptobioTech(1, witness);
                        if (info != null)
                        {
                            float thisAwareness = info.LarvaAwareness;
                            if (thisAwareness > bonus)
                            {
                                bonus = thisAwareness;
                            }
                            info.WitnessLarvaHorror(strength, maxAwareness);
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

        public static bool TransformPawnIntoPawn(Pawn target, PawnKindDef targetDef, out Pawn result, Pawn instigator = null)
        {
            result = null;
            if (targetDef == null)
            {
                return false;
            }

            if (target == null)
            {
                return false;
            }
            PawnGenerationRequest request = new PawnGenerationRequest(
                targetDef, faction: target.Faction, PawnGenerationContext.PlayerStarter, -1, true, false, true, false, false, 0, false, true, false, false, false, false, false, false, true, 0, 0, null, 0, null, null, null, null, 0, target.ageTracker.AgeBiologicalYears, target.ageTracker.AgeChronologicalYears, target.gender);

            if (targetDef.race.race.hasGenders)
            {
                ThingDef_AlienRace NewRaceDef = targetDef.race as ThingDef_AlienRace;
                if (NewRaceDef != null)
                {
                    if (NewRaceDef.alienRace.generalSettings.maleGenderProbability <= 0)
                    {
                        request.FixedGender = Gender.Female;
                    }
                }
            }
            else
            {
                request.FixedGender = Gender.None;
            }

            Pawn NewPawn = PawnGenerator.GeneratePawn(request);

            if (NewPawn != null)
            {
                NewPawn.Name = target.Name;

                if(NewPawn.BodySize < target.BodySize)
                {
                    if (target.Spawned)
                    {
                        ThingDef meat = NewPawn.RaceProps.meatDef;
                        if(meat == null)
                        {
                            meat = ThingDefOf.Meat_Human;
                        }

                        int ExcessAmount = Mathf.FloorToInt((target.GetStatValue(StatDefOf.Mass) - NewPawn.GetStatValue(StatDefOf.Mass))/NewPawn.RaceProps.meatDef.BaseMass);

                        DropAmountThing(meat, ExcessAmount, target.PositionHeld, target.MapHeld, InternalDefOf.Starbeast_Filth_Resin);
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

                if (target.relations != null)
                {
                    if (NewPawn.relations != null)
                    {
                        NewPawn.relations.ClearAllRelations();
    
                        foreach (DirectPawnRelation relation in target.relations.DirectRelations)
                        {
                            NewPawn.relations.AddDirectRelation(relation.def, relation.otherPawn);
                        }

                        foreach(Pawn relatedPawn in target.relations.RelatedPawns)
                        {
                            List<DirectPawnRelation> pawnRelations = relatedPawn.relations.DirectRelations.ToList();
                            foreach (DirectPawnRelation relation in pawnRelations)
                            {
                                if(relation.otherPawn == target)
                                {
                                    relatedPawn.relations.AddDirectRelation(relation.def,NewPawn);
                                }
                            }
                        }

                        target.relations.ClearAllRelations();


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

                        BodyPartDef partDef = hediff.Part?.def;
                        string label = hediff.Part?.Label;
                        if (partDef == null || label == null)
                        {
                            if (XMTSettings.LogBiohorror)
                            {
                                Log.Message("part or label is null in TransformPawnIntoPawn on " + target);
                            }
                            continue;
                        }

                        if(partDef == ExternalDefOf.Brain)
                        {
                            if (IsXenomorph(NewPawn))
                            {
                                partDef = InternalDefOf.StarbeastBrain;
                                label = partDef.label;
                            }
                        }

                        BodyPartRecord newPart = NewPawn.def.race.body.AllParts.FirstOrDefault(part => part.def == partDef && part.Label == label);
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

                            
                
                            Hediff newHediff = HediffMaker.MakeHediff(hediff.def, NewPawn, newPart);
                            NewPawn.health.AddHediff(newHediff, newHediff.Part);
                            
                            continue;
                        }
                        ThingDef thingToSpawn = hediff.def.spawnThingOnRemoved;
                        if (thingToSpawn != null)
                        {
                            GenSpawn.Spawn(thingToSpawn, NewPawn.Position, NewPawn.Map);
                            
                            continue;
                        }                        
                    }
                }

                if (target?.needs?.mood?.thoughts?.memories != null)
                {
                    if (NewPawn?.needs?.mood?.thoughts?.memories != null)
                    {
                        foreach (Thought_Memory thought in target.needs.mood.thoughts.memories.Memories)
                        {
                            NewPawn.needs.mood.thoughts.memories.Memories.Add(thought);
                        }
                    }
                }

                if(target.genes != null)
                {
                    if(NewPawn.genes != null)
                    {
                        GeneSet geneset = new GeneSet();
                        BioUtility.ExtractCryptimorphGenesToGeneset(ref geneset, target.genes.GenesListForReading);
                        BioUtility.InsertGenesetToPawn(geneset, ref NewPawn);
                    }
                }
                else
                {
                    if (NewPawn.genes != null)
                    {
                        GeneSet geneset = new GeneSet();
                        List<GeneDef> genes = BioUtility.GetExtraHostGenes(target);
                        BioUtility.ExtractGenesToGeneset(ref geneset, genes);
                        BioUtility.InsertGenesetToPawn(geneset, ref NewPawn);
                    }
                }

                if (target.ideo != null)
                {
                    if (NewPawn.ideo != null)
                    {
                        NewPawn.ideo.SetIdeo(target.ideo.Ideo);
                    }
                }

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

                if(target.apparel != null)
                {
                    target.apparel.DropAll(target.Position);
                }

                if(target.inventory != null)
                {
                    target.inventory.DropAllNearPawn(target.Position);
                }

                if(target.carryTracker != null)
                {
                    if (target.carryTracker.CarriedThing != null)
                    {
                        target.carryTracker.TryDropCarriedThing(target.Position, ThingPlaceMode.Near, out Thing resultingThing);
                    }
                }

                /*
                 * Find.PlayLog.AllEntries.Where((LogEntry e) => e.Concerns(pawn)).ToArray()
                 */
            }

            TrySpawnPawnFromTarget(NewPawn, target);


            target.Destroy();

            result = NewPawn;
            return true;
        }

        internal static bool TransformThingIntoThing(Thing target, ThingDef targetDef, out Thing result, Pawn instigator = null)
        {
            result = null;
            if (targetDef == null)
            {
                return false;
            }

            if (target == null)
            {
                return false;
            }

            Thing NewThing = ThingMaker.MakeThing(targetDef);
           
            XMTBase_Building building = NewThing as XMTBase_Building;
            if (building != null)
            {
                building.TransformedFrom(target, instigator);
            }

            result = GenSpawn.Spawn(NewThing, target.PositionHeld, target.MapHeld, WipeMode.Vanish);
            return true;
        }

        internal static bool TransformPawnIntoThing(Pawn target, ThingDef targetDef, out Thing result, Pawn instigator = null)
        {
            result = null;
            if (targetDef == null)
            {
                return false;
            }

            if (target == null)
            {
                return false;
            }

            Thing NewThing = GenSpawn.Spawn(targetDef, target.Dead ? target.Corpse.Position : target.Position, target.Dead ? target.Corpse.Map : target.MapHeld, WipeMode.Vanish);

            if (target?.health.hediffSet != null)
            {
                List<Hediff> hediffList = target.health.hediffSet.hediffs.ToList();
                foreach (Hediff hediff in hediffList)
                {
                    BodyPartDef partDef = hediff.Part?.def;
                    string label = hediff.Part?.Label;
                    if (partDef == null || label == null)
                    {
                        if (XMTSettings.LogBiohorror)
                        {
                            Log.Message("part or label is null in TransformPawnIntoThing on " + target);
                        }
                        continue;
                    }
                    switch (label)
                    {
                        case "genitals":
                        case "chest":
                        case "anus":
                            continue;
                    }

                    ThingDef thingToSpawn = hediff.def.spawnThingOnRemoved;
                    if (thingToSpawn != null)
                    {
                        GenSpawn.Spawn(thingToSpawn, target.Dead ? target.Corpse.Position : target.PositionHeld, target.Dead ? target.Corpse.Map : target.MapHeld,WipeMode.VanishOrMoveAside);

                        continue;
                    }
                }
            }
            XMTBase_Building building = NewThing as XMTBase_Building;
            if (building != null)
            {
                building.TransformedFrom(target, instigator);
            }

            target.Destroy();
            result = NewThing;
            return true;
        }
        public static bool PlayerXenosOnMap(Map localMap)
        {
            return XMTHiveUtility.PlayerXenosOnMap(localMap);
        }

        public static void DropAmountThing(ThingDef thingDef, int stackTotal, IntVec3 position, Map targetMap, ThingDef Filth = null)
        {

            int stackLimit = thingDef.stackLimit;
            int totalstacks = stackTotal / stackLimit;

            List<Thing> DroppedMeat = new List<Thing>();

            for (int i = 0; i < totalstacks; i++)
            {
                if (stackTotal <= 0)
                {
                    break;
                }
                Thing meat = ThingMaker.MakeThing(thingDef);
                meat.stackCount = stackTotal > stackLimit ? stackLimit : totalstacks;
                stackTotal -= meat.stackCount;
                DroppedMeat.Add(meat);
            }

            if (stackTotal > 0)
            {
                Thing meat = ThingMaker.MakeThing(thingDef);
                meat.stackCount = stackTotal;
                DroppedMeat.Add(meat);
            }

            List<IntVec3> dropRadial = GenRadial.RadialCellsAround(position, GenRadial.RadiusOfNumCells(DroppedMeat.Count + 1), false).ToList();
            for (int i = 0; i < DroppedMeat.Count; i++)
            {
                if (Filth != null)
                {
                    FilthMaker.TryMakeFilth(dropRadial[i], targetMap, Filth);
                }
                GenSpawn.Spawn(DroppedMeat[i], dropRadial[i], targetMap,WipeMode.VanishOrMoveAside);
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
                    Log.Message("Queen reported as dead: " + p);
                }
                Queen = null;
            }
        }

        public static void DeclareQueen(Pawn p)
        {
            
            if (Queen == null)
            {
                if (IsQueen(p))
                {
                    Queen = p;
                }
            }
        }

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

            return compQueen.ChosenEvolutions.Contains(EvoDef);
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
                CompAwakenedSlumberer slumberer = pawn.GetComp<CompAwakenedSlumberer>();

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
            else if(target.def == ExternalDefOf.Ship_Beam)
            {
                GenSpawn.Spawn(ExternalDefOf.Ship_Beam_Unpowered, target.Position, target.Map);
            }
            else if (target.def == ExternalDefOf.Ship_BeamMech)
            {
                GenSpawn.Spawn(ExternalDefOf.Ship_BeamMech_Unpowered, target.Position, target.Map);
            }
            else if (target.def == ExternalDefOf.Ship_BeamArchotech)
            {
                GenSpawn.Spawn(ExternalDefOf.Ship_BeamArchotech_Unpowered, target.Position, target.Map);
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

                if (witnessInfo.IsObsessed(out float awareness))
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
            if (!aggressorInfo.IsXenomorphFriendly())
            {
                IntVec3 eventPosition = (victim != null) ? victim.PositionHeld : aggressorInfo.parent.PositionHeld;
                IEnumerable <IntVec3> cells = GenRadial.RadialCellsAround(eventPosition, radius, true);

                foreach (IntVec3 cell in cells)
                {
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
                                witness.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                            }
                        }
                        else
                        {
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

            return pawn.health.hediffSet.HasHediff(InternalDefOf.StarbeastCocoon);
        }

        internal static Job ClimberFleeJob(Pawn pawn)
        {
            RCellFinder.TryFindRandomExitSpot(pawn, out IntVec3 cell, TraverseMode.PassAllDestroyableThings);
            Job job = JobMaker.MakeJob(JobDefOf.Flee, cell);
            job.exitMapOnArrival = true;
            return job;
        }

        internal static bool IsHostileAndAwareOf(Thing a, Thing b)
        {
            if(a is Pawn observer)
            {
                CompPawnInfo info = observer.Info();
                if(info.IsObsessed())
                {
                    return false;
                }

                if(a.Faction == b.Faction)
                {
                    return info.TotalHorrorAwareness() > 0.5f;
                }

                return info.TotalHorrorAwareness() > 0.25f;

            }
            return false;
        }
    }
}
