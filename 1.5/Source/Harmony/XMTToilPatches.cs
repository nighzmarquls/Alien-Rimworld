using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class XMTToilPatches
    {
        [HarmonyPatch(typeof(JobDriver_Deconstruct), "FinishedRemoving")]
        public static class Patch_JobDriver_Deconstruct_FinishedRemoving
        {


            [HarmonyPrefix]
            public static void Prefix(JobDriver_Deconstruct __instance)
            {
                Pawn actor = __instance.pawn;
                
                if (XMTUtility.IsXenomorph(actor))
                {
                    if (__instance.job.GetTarget(TargetIndex.A).Thing is Building building)
                    {
                        if (!XMTUtility.IsHiveBuilding(building.def))
                        {
                           
                            int progress = building.HitPoints / 100;
                            XMTResearch.ProgressMimicTech(progress, actor);
                            
                        }
                    }
                }
            }
        }
        [HarmonyPatch(typeof(JobDriver_ConstructFinishFrame), "MakeNewToils")]
        public static class Patch_JobDriver_ConstructFinishFrame_MakeNewToils
        {
            private static bool IsBuildingAttachment(Frame frame)
            {
                
                ThingDef thingDef = GenConstruct.BuiltDefOf(frame.def) as ThingDef;
                if (thingDef?.building != null)
                {
                    return thingDef.building.isAttachment;
                }

                return false;
                
            }

            [HarmonyPostfix]
            public static void Postfix(ref IEnumerable<Toil> __result, JobDriver_ConstructFinishFrame __instance)
            {
                Pawn actor = __instance.pawn;
                Frame frame = (Frame)__instance.job.GetTarget(TargetIndex.A).Thing;
                
                if (XMTUtility.IsXenomorph(actor))
                {
                    if (XMTUtility.IsHiveBuilding(frame.BuildDef))
                    {
                        List<Toil> output = new List<Toil>();
                        
                        foreach (Toil build in __result)
                        {
                            if (build.debugName == "MakeNewToils")
                            {
                                build.tickAction = delegate
                                {
                                    if (actor.skills != null)
                                    {
                                        actor.skills.Learn(SkillDefOf.Construction, 0.25f);
                                        int progress = 1;
                                        XMTResearch.ProgressResinTech(progress, actor);
                                    }

                                    if (actor?.needs?.food != null)
                                    {
                                        actor.needs.food.CurLevel = actor.needs.food.CurLevel - HiveUtility.HiveHungerCostPerTick;

                                        if(actor.needs.food.Starving)
                                        {
                                            Hediff Malnutrition = actor.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);

                                            if (Malnutrition != null)
                                            {
                                                Malnutrition.Severity += 0.001f;

                                                
                                                //actor.workSettings.Disable(WorkTypeDefOf.Construction);
                                            }
                                            __instance.ReadyForNextToil();
                                            return;
                                        }
                                    }

                                    if (IsBuildingAttachment(frame))
                                    {
                                        actor.rotationTracker.FaceTarget(GenConstruct.GetWallAttachedTo(frame));
                                    }
                                    else
                                    {
                                        actor.rotationTracker.FaceTarget(frame);
                                    }

                                    float num = actor.GetStatValue(StatDefOf.ConstructionSpeed) * 1.7f;
                                    if (frame.Stuff != null)
                                    {
                                        num *= frame.Stuff.GetStatValueAbstract(StatDefOf.ConstructionSpeedFactor);
                                    }

                                    float workToBuild = frame.WorkToBuild;
                                    if (actor.Faction == Faction.OfPlayer)
                                    {
                                        float statValue = actor.GetStatValue(StatDefOf.ConstructSuccessChance);
                                        if (!TutorSystem.TutorialMode && Rand.Value < 1f - Mathf.Pow(statValue, num / workToBuild))
                                        {
                                            frame.FailConstruction(actor);
                                            __instance.ReadyForNextToil();
                                            return;
                                        }
                                    }

                                    if (frame.def.entityDefToBuild is TerrainDef)
                                    {
                                        actor.Map.snowGrid.SetDepth(frame.Position, 0f);
                                    }

                                    frame.workDone += num;
                                    if (frame.workDone >= workToBuild)
                                    {
                                        frame.CompleteConstruction(actor);
                                        __instance.ReadyForNextToil();
                                    }
                                };
                            }
                            output.Add(build);
                        }
                        __result = output;

                    }
                }
            }
        }
    }
}

/* TODO: Implement this instead.

Bradson — Today at 11:10 AM
The argument in my method there is a predicate, so you'd pass in field => field.Name == "tickAction" or field == staticFieldInfoFetchedThroughAccessTools for example
The first FindIndex there looks for the field the delegate is being assigned to. The FindLastIndex after that then looks for the closest preceding Ldftn instruction, which itself holds the delegate's methodinfo as its operand
Predicate can be modified as needed. Ldftn should be more or less always the same, outside of rare goto constructs
Right, and because of MakeNewToils itself being a method implemented with yield return its own instructions only contain the new Enumerable constructor. AccessTools.EnumeratorMoveNext(containingMethodInfo) would be another method to put in between there

 *  public static MethodBase FindDelegateMethod(this MethodBase containingMethod, Predicate<FieldInfo> predicate,
        string fieldName)
    {
        var body = PatchProcessor.ReadMethodBody(containingMethod).ToList();

        var fieldIndex = body.FindIndex(code
            => (code.Key == OpCodes.Stfld || code.Key == OpCodes.Stsfld)
            && code.Value is FieldInfo fieldInfo
            && predicate(fieldInfo));

        if (fieldIndex < 0)
        {
            ThrowHelper.ThrowInvalidOperationException($"No assignment to field '{
                fieldName}' found in method '{containingMethod.Name}'");
        }

        var methodIndex = body.FindLastIndex(fieldIndex, static code => code.Key == OpCodes.Ldftn);

        if (methodIndex < 0)
        {
            ThrowHelper.ThrowInvalidOperationException($"No function found with assignment to field '{
                fieldName}' in method '{containingMethod.Name}'");
        }

        return (MethodBase)body[methodIndex].Value;
    }*/