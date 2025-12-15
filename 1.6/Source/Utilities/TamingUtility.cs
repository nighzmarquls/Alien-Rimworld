

using RimWorld;
using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class TamingUtility
    {
        private static bool TryPlatformInteraction(Pawn pawn, Pawn recipient, InteractionDef intDef)
        {
            if (DebugSettings.alwaysSocialFight)
            {
                intDef = InteractionDefOf.Insult;
            }

            if (pawn == recipient)
            {
                Log.Warning(pawn?.ToString() + " tried to interact with self, interaction=" + intDef.defName);
                return false;
            }

            if (!intDef.ignoreTimeSinceLastInteraction && pawn.interactions.InteractedTooRecentlyToInteract())
            {
                return false;
            }

            List<RulePackDef> list = new List<RulePackDef>();
            if (intDef.initiatorThought != null)
            {
                XMTUtility.GiveInteractionMemory(pawn,  intDef.initiatorThought, recipient);
            }

            if (intDef.recipientThought != null && recipient.needs.mood != null)
            {
                XMTUtility.GiveInteractionMemory(recipient, intDef.recipientThought, pawn);
            }

            if (intDef.initiatorXpGainSkill != null)
            {
                pawn.skills.Learn(intDef.initiatorXpGainSkill, intDef.initiatorXpGainAmount);
            }

            if (intDef.recipientXpGainSkill != null && recipient.RaceProps.Humanlike)
            {
                recipient.skills.Learn(intDef.recipientXpGainSkill, intDef.recipientXpGainAmount);
            }

            recipient.ideo?.IncreaseIdeoExposureIfBaby(pawn.Ideo, 0.5f);

            string letterText;
            string letterLabel;
            LetterDef letterDef;
            LookTargets lookTargets;
         
            intDef.Worker.Interacted(pawn, recipient, list, out letterText, out letterLabel, out letterDef, out lookTargets);


            MoteMaker.MakeInteractionBubble(pawn, recipient, intDef.interactionMote, intDef.GetSymbol(pawn.Faction, pawn.Ideo), intDef.GetSymbolColor(pawn.Faction));

            PlayLogEntry_Interaction playLogEntry_Interaction = new PlayLogEntry_Interaction(intDef, pawn, recipient, list);
            Find.PlayLog.Add(playLogEntry_Interaction);
            if (letterDef != null)
            {
                string text = playLogEntry_Interaction.ToGameStringFromPOV(pawn);
                if (!letterText.NullOrEmpty())
                {
                    text = text + "\n\n" + letterText;
                }

                Find.LetterStack.ReceiveLetter(letterLabel, text, letterDef, lookTargets ?? ((LookTargets)pawn));
            }

            return true;
        }

        private static void ProcessTamingImpact(Pawn tamer, Pawn recipient)
        {
            CompMatureMorph morph = recipient.GetMorphComp();

            if (morph == null)
            {
                return;
            }

            if (morph.ShouldTameCondition)
            {
                if (!tamer.story.DisabledWorkTagsBackstoryTraitsAndGenes.HasFlag(WorkTags.Violent) && tamer.equipment.HasAnything())
                {
                    XMTUtility.GiveInteractionMemory(recipient, ThoughtDefOf.HarmedMe, tamer);
                    morph.tamingConditioning += Mathf.Min(tamer.skills.GetSkill(SkillDefOf.Melee).Level * 0.025f, tamer.skills.GetSkill(SkillDefOf.Shooting).Level * 0.05f);
                    if (recipient.IsOnHoldingPlatform)
                    {
                        if (recipient.ParentHolder is Building_HoldingPlatform platform)
                        {
                            if (platform.TryGetComp(out CompEntityHolder comp))
                            {
                                float containment = recipient.GetStatValue(StatDefOf.MinimumContainmentStrength);
                                if (comp.ContainmentStrength < containment && !recipient.Downed)
                                {
                                    comp.EjectContents();
                                    recipient.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                                }
                            }

                        }
                    }
                    else if (!recipient.Downed)
                    {
                        recipient.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                    }
                }
            }

            if (morph.ShouldTamePheromone)
            {
                if (tamer.Info().XenomorphPheromoneValue() > 0f)
                {
                    XMTUtility.GiveInteractionMemory(recipient, HorrorMoodDefOf.SnuggledVictim, tamer);
                    morph.tamingPheromones += tamer.Info().XenomorphPheromoneValue() * 0.01f;
                }
                else if(tamer.Info().XenomorphPheromoneValue() < 0f)
                {
                    XMTUtility.GiveInteractionMemory(recipient, ThoughtDefOf.HarmedMe, tamer);
                    if (recipient.IsOnHoldingPlatform)
                    {
                        if (recipient.ParentHolder is Building_HoldingPlatform platform)
                        {
                            if (platform.TryGetComp(out CompEntityHolder comp))
                            {
                                float containment = recipient.GetStatValue(StatDefOf.MinimumContainmentStrength);
                                if (comp.ContainmentStrength < containment && !recipient.Downed)
                                {
                                    comp.EjectContents();
                                }
                            }
                            recipient.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                        }
                    }
                    else if (!recipient.Downed)
                    {
                        recipient.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                    }
                }
            }

            if (morph.ShouldTameSocial)
            {
                if (tamer.XenoSocial() >= 1 && !recipient.ageTracker.Adult)
                {
                    XMTUtility.GiveInteractionMemory(recipient, HorrorMoodDefOf.SnuggledVictim, tamer);
                    morph.tamingSocializing += tamer.XenoSocial() * tamer.skills.GetSkill(SkillDefOf.Social).Level * 0.05f;
                }
            }

            if (morph.ShouldTameHostage)
            {
                if (!tamer.story.DisabledWorkTagsBackstoryTraitsAndGenes.HasFlag(WorkTags.Violent) && tamer.equipment.HasAnything())
                {
                    XMTUtility.GiveInteractionMemory(recipient, ThoughtDefOf.HarmedMe, tamer);
                    morph.tamingHostage += Mathf.Min(tamer.skills.GetSkill(SkillDefOf.Melee).Level * 0.025f, tamer.skills.GetSkill(SkillDefOf.Shooting).Level * 0.05f);
                    if (recipient.IsOnHoldingPlatform)
                    {
                        if (recipient.ParentHolder is Building_HoldingPlatform platform)
                        {
                            if (platform.TryGetComp(out CompEntityHolder comp))
                            {
                                float containment = recipient.GetStatValue(StatDefOf.MinimumContainmentStrength);
                                if (comp.ContainmentStrength < containment && !recipient.Downed)
                                {
                                    comp.EjectContents();
                                    recipient.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                                }
                            }

                        }
                    }
                    else if (!recipient.Downed)
                    {
                        recipient.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                    }
                }
            }
            ResearchUtility.ProgressCryptobioTech(1, tamer);
        }

        public static Toil InteractWithTargetPawn(TargetIndex tameeInd)
        {
            Toil toil = ToilMaker.MakeToil("InteractTargetPawn");
            toil.initAction = delegate
            {
                Pawn actor = toil.GetActor();
                if (actor.CurJob.GetTarget(tameeInd).Thing is Pawn recipient)
                {
                    if (recipient.Spawned && recipient.Awake())
                    {
                        ProcessTamingImpact(actor, recipient);
                        actor.interactions.TryInteractWith(recipient, InteractionDefOf.AnimalChat);
                    }
                }
                else if(actor.CurJob.GetTarget(tameeInd).Thing is Building_HoldingPlatform platform)
                {
                    TryPlatformInteraction(actor, platform.HeldPawn, InteractionDefOf.AnimalChat);
                }
            };
            
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = 270;
            toil.activeSkill = () => SkillDefOf.Animals;
            return toil;
        }

        public static Toil TryRecruitPawnOnPlatform(TargetIndex recruiteeInd)
        { 
            Toil toil = ToilMaker.MakeToil("RecruitTargetOnPlatform");
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                if (actor.CurJob.GetTarget(recruiteeInd).Thing is Building_HoldingPlatform platform)
                {
                    Pawn recipient = platform.HeldPawn;
                    if (recipient.Awake())
                    {
                        InteractionDef intDef = XenoSocialDefOf.XMT_AdvancedTameAttempt;
                        ProcessTamingImpact(actor, recipient);
                        TryPlatformInteraction(actor, recipient, intDef);
                        recipient.mindState.lastAssignedInteractTime = Find.TickManager.TicksGame;
                        recipient.mindState.interactionsToday++;
                    }
                }
            };
            toil.socialMode = RandomSocialMode.Off;
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = 350;
            toil.activeSkill = () => SkillDefOf.Animals;
            return toil;
        }

        public static Toil TryRecruitPawn(TargetIndex recruiteeInd)
        {
            Toil toil = ToilMaker.MakeToil("RecruitTarget");
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Pawn pawn = (Pawn)actor.jobs.curJob.GetTarget(recruiteeInd).Thing;
                if (pawn.Spawned && pawn.Awake())
                {
                    InteractionDef intDef =  XenoSocialDefOf.XMT_AdvancedTameAttempt;
                    actor.interactions.TryInteractWith(pawn, intDef);
                }
            };
            toil.socialMode = RandomSocialMode.Off;
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = 350;
            toil.activeSkill = () => SkillDefOf.Animals;
            return toil;
        }

        public static bool CanTameSocial(Pawn tameTarget)
        {
            if(XMTHiveUtility.PlayerXenosOnMap(tameTarget.MapHeld))
            {
                return true;
            }

            foreach (Pawn colonist in tameTarget.MapHeld.mapPawns.FreeAdultColonistsSpawned)
            {
                if (colonist.story.DisabledWorkTagsBackstoryTraitsAndGenes.HasFlag(WorkTags.Social))
                {
                    continue;
                }

                if(colonist.XenoSocial() >= 1)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CanTameConditioning(Pawn pawn)
        {
            foreach(Pawn colonist in pawn.MapHeld.mapPawns.FreeAdultColonistsSpawned)
            {
                if (colonist.story.DisabledWorkTagsBackstoryTraitsAndGenes.HasFlag(WorkTags.Violent))
                {
                    continue;
                }

                if(colonist.equipment.HasAnything())
                {
                    return true;
                }
            }
            return false;
        }
        public static bool CanTameBribery(Pawn pawn)
        {
            foreach (Pawn colonist in pawn.MapHeld.mapPawns.FreeAdultColonistsSpawned)
            {
                if (colonist.story.DisabledWorkTagsBackstoryTraitsAndGenes.HasFlag(WorkTags.Animals))
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        public static bool CanTameThreat(Pawn pawn)
        {
            foreach (Pawn prisoner in pawn.MapHeld.mapPawns.SlavesAndPrisonersOfColonySpawned)
            {
                if (prisoner.GetMorphComp() == null)
                {
                    continue;
                }
                
                if(XMTUtility.IsQueen(prisoner))
                {
                    return true;
                }
            }

            if(pawn.MapHeld != null)
            {
                if(pawn.MapHeld.resourceCounter.GetCount(InternalDefOf.XMT_Ovomorph) > 0)
                {
                    return true;
                }
            }

            return false;
        }
        public static bool CanTamePheromones(Pawn pawn)
        {
            if (XMTHiveUtility.PlayerXenosOnMap(pawn.MapHeld))
            {
                return true;
            }

            foreach (Pawn colonist in pawn.MapHeld.mapPawns.FreeAdultColonistsSpawned)
            {
                if (colonist.Info().XenomorphPheromoneValue() > 0f || colonist.Info().XenomorphPheromoneValue() < 0f)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CanTameControlHarness(Pawn pawn)
        {

            return false;
        }
        public static bool IsAdvancedTameable(this Pawn pawn)
        {
            CompMatureMorph morph = pawn.GetMorphComp();
            if (morph == null)
            {
                return false;
            }

            if (pawn.IsFullyIntegrated())
            {
                return false;
            }

            
            if (pawn.GuestStatus != GuestStatus.Prisoner && !pawn.IsOnHoldingPlatform)
            {
                return false;
            }

            if(CanTameSocial(pawn))
            {
                return true;
            }

            if(CanTameConditioning(pawn))
            {
                return true;
            }

            if(CanTameBribery(pawn))
            {
                return true;
            }

            if(CanTameThreat(pawn))
            {
                return true;
            }

            if(CanTameControlHarness(pawn))
            {
                return true;
            }

            return false;
        }

        public static bool IsFullyIntegrated(this Pawn pawn)
        {
            if (pawn.GuestStatus == RimWorld.GuestStatus.Slave || pawn.GuestStatus == RimWorld.GuestStatus.Prisoner)
            {
                return false;
            }

            if (pawn.Faction != Faction.OfPlayer)
            {
                if(pawn.GetMorphComp() is CompMatureMorph morph)
                {
                    return morph.Integrated;
                }
                return false;
            }

            return true;
        }

        public static bool HasFoodToInteractAnimal(Pawn pawn, Pawn tamee)
        {
            ThingOwner<Thing> innerContainer = pawn.inventory.innerContainer;
            int num = 0;
            float num2 = JobDriver_InteractAnimal.RequiredNutritionPerFeed(tamee);
            float num3 = 0f;
            for (int i = 0; i < innerContainer.Count; i++)
            {
                Thing thing = innerContainer[i];
                if (!tamee.WillEat(thing, pawn) || (int)thing.def.ingestible.preferability > 5 || thing.def.IsDrug)
                {
                    continue;
                }

                for (int j = 0; j < thing.stackCount; j++)
                {
                    num3 += thing.GetStatValue(StatDefOf.Nutrition);
                    if (num3 >= num2)
                    {
                        num++;
                        num3 = 0f;
                    }

                    if (num >= 2)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

    }
}
