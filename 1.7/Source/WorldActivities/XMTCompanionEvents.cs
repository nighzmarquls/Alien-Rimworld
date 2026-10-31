using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    [HarmonyPatch(typeof(PawnGenerator), "GetValidCandidatesToRedress")]
    internal static class XMT_WorldReuseProtectionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref System.Collections.Generic.IEnumerable<Pawn> __result)
        {
            var state = Current.Game?.GetComponent<GameComponent_XMTWorldState>();
            if (state != null) __result = __result.Where(p => !state.BlockWorldReuse(p));
        }
    }
    [HarmonyPatch(typeof(LordMaker), nameof(LordMaker.MakeNewLord))]
    internal static class XMT_DeploymentRegistrationPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Lord __result)
        {
            if (__result?.LordJob is LordJob_XMT operation && operation.Response != null)
                __result.Map.GetComponent<MapComponent_XMTWorldOperations>().Register(__result);
        }
    }
    [HarmonyPatch(typeof(LordManager), nameof(LordManager.RemoveLord))]
    internal static class XMT_DeploymentRemovalPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Lord __0)
        {
            if (__0?.LordJob is not LordJob_XMT) return;
            (__0.LordJob as LordJob_XMTRecovery)?.Cancel();
            __0.Map.GetComponent<MapComponent_XMTWorldOperations>().Remove(__0);
        }
    }
    internal static class XMT_CompanionCaravanUtility
    {
        internal static void TryEncounter(GameComponent_XMTWorldState state, XMT_CompanionLink link, Caravan caravan, int tick)
        {
            Pawn companion = state.ResolveCompanion(link);
            if (!XMT_PawnStateUtility.Available(companion, state)) return;
            if (!link.caravanRefused)
            {
                link.dueTick = tick + GenDate.TicksPerDay;
                Find.WindowStack.Add(new Dialog_MessageBox("XMT_CompanionDemand".Translate(companion.Named("COMPANION"), link.owner.Named("OWNER")),
                    buttonAText: "XMT_CompanionSurrender".Translate(), buttonAAction: () =>
                    {
                        Thing target = link.Target;
                        if (target?.GetCaravan() != caravan) return;
                        if (target is Pawn pawn)
                        {
                            caravan.RemovePawn(pawn);
                            Faction original = pawn.SlaveFaction ?? pawn.Faction;
                            pawn.guest?.SetGuestStatus(null);
                            pawn.SetFaction(original);
                            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                        }
                        else if (!state.RetainThing(target)) return;
                        if (target is Corpse corpse)
                            ((XMT_CorpseOutcomeWorker)System.Activator.CreateInstance(link.definition.corpseOutcomeWorkerClass)).Recovered(state, link, corpse);
                        state.Reunited(link);
                    }, buttonBText: "XMT_CompanionRefuse".Translate(), buttonBAction: () =>
                    { link.caravanRefused = true; link.dueTick = tick + 250; }, title: "XMT_CompanionDemandLabel".Translate()));
                return;
            }
            int hour = GenLocalDate.HourOfDay(caravan.Tile);
            if (hour >= 5 && hour < 20 || !CaravanIncidentUtility.CanFireIncidentWhichWantsToGenerateMapAt(caravan.Tile)) return;
            LordJob_XMTRecovery operation = new LordJob_XMTRecovery(link, true);
            companion.SetFaction(Faction.OfAncientsHostile);
            Map map = CaravanIncidentUtility.SetupCaravanAttackMap(caravan, new System.Collections.Generic.List<Pawn> { companion }, false);
            if (map == null) { link.dueTick = tick + GenDate.TicksPerDay; return; }
            companion.GetLord()?.Notify_PawnLost(companion, PawnLostCondition.ForcedToJoinOtherLord);
            if (state.BeginOperation(link, map) == null) return;
            LordMaker.MakeNewLord(companion.Faction, operation, map, new[] { companion });
            link.attempts++;
            Find.LetterStack.ReceiveLetter("XMT_CompanionAssaultLabel".Translate(),
                "XMT_CompanionAssault".Translate(companion.Named("COMPANION"), link.owner.Named("OWNER")), LetterDefOf.ThreatBig, companion);
        }
    }

    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.AddDirectRelation))]
    internal static class XMT_TrackedBirthPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn ___pawn, PawnRelationDef def, Pawn otherPawn)
        {
            GameComponent_XMTWorldState state = Current.Game?.GetComponent<GameComponent_XMTWorldState>();
            if (state != null && def == PawnRelationDefOf.Parent && ___pawn.ageTracker.AgeBiologicalYearsFloat < 1f && state.Protects(otherPawn))
                state.Track(___pawn, "descendant:" + otherPawn.ThingID);
        }
    }
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
    internal static class XMT_TrackedDeparturePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Pawn __instance, out (bool player, LordJob_XMTRecovery recovery) __state) =>
            __state = (__instance.Faction == Faction.OfPlayer, __instance.GetLord()?.LordJob as LordJob_XMTRecovery);
        [HarmonyPostfix]
        private static void Postfix(Pawn __instance, (bool player, LordJob_XMTRecovery recovery) __state)
        {
            if (__state.player && !__instance.Destroyed) Current.Game.GetComponent<GameComponent_XMTWorldState>().Track(__instance, "player-offmap");
            __state.recovery?.NotifyMapDeparture(__instance);
        }
    }

    internal static class XMT_CompanionEvents
    {
        internal static void PlayerAggression(Thing target, Faction aggressor)
        {
            if (Current.Game == null || target == null || aggressor != Faction.OfPlayer) return;
            Pawn subject = target as Pawn ?? (target as Corpse)?.InnerPawn;
            if (subject == null) return;
            if (subject.GetLord()?.LordJob is LordJob_XMTRecovery own) own.ExposeHostility();
            foreach (XMT_CompanionLink link in Current.Game.GetComponent<GameComponent_XMTWorldState>().Companions)
                if (link.operationId != null && link.owner == subject && link.companion?.GetLord()?.LordJob is LordJob_XMTRecovery operation)
                    operation.ExposeHostility();
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class XMT_RecoveryDamagePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Thing __instance, DamageInfo dinfo) => XMT_CompanionEvents.PlayerAggression(__instance, dinfo.Instigator?.Faction);
        [HarmonyPostfix]
        private static void Postfix(Thing __instance, DamageInfo dinfo)
        {
            if (__instance.Destroyed && dinfo.Instigator is Pawn attacker && attacker.CurJob?.def.defName == "XMT_RecoveryBreach"
                && attacker.CurJob.targetA.Thing == __instance)
                (attacker.GetLord()?.LordJob as LordJob_XMTRecovery)?.Announce(true);
        }
    }
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class XMT_RecoveryArrestPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Pawn ___pawn, Job newJob)
        {
            if (newJob != null && (newJob.def == JobDefOf.Arrest || newJob.def == JobDefOf.AttackMelee || newJob.def == JobDefOf.AttackStatic))
                XMT_CompanionEvents.PlayerAggression(newJob.targetA.Thing, ___pawn.Faction);
        }
    }
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo),
        typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    internal static class XMT_RecoveryAttackAttemptPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Verb __instance, LocalTargetInfo __0)
        {
            if (__instance.verbProps?.violent == true)
                XMT_CompanionEvents.PlayerAggression(__0.Thing, __instance.Caster?.Faction);
        }
    }
    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.StartManualOpenBy))]
    internal static class XMT_RecoveryDoorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Building_Door __instance, Pawn __0)
        { (__0?.GetLord()?.LordJob as LordJob_XMTRecovery)?.DoorOpened(__instance); }
    }
    [HarmonyPatch(typeof(KidnappedPawnsTracker), nameof(KidnappedPawnsTracker.Kidnap), new[] { typeof(Pawn), typeof(Pawn) })]
    internal static class XMT_WorldCaptivePatch
    {
        [HarmonyPostfix]
        private static void Postfix(KidnappedPawnsTracker __instance, Pawn pawn, Pawn kidnapper)
        {
            if (__instance.KidnappedPawnsListForReading.Contains(pawn))
                Current.Game.GetComponent<GameComponent_XMTWorldState>().Track(pawn, "captured", kidnapper?.Faction);
        }
    }
    // A normal generated group can bring a previously separated companion without creating a rescue operation.
    [HarmonyPatch(typeof(PawnGroupMakerUtility), nameof(PawnGroupMakerUtility.GeneratePawns))]
    internal static class XMT_CompanionGroupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref System.Collections.Generic.IEnumerable<Pawn> __result, PawnGroupMakerParms parms)
        {
            GameComponent_XMTWorldState state = Current.Game?.GetComponent<GameComponent_XMTWorldState>();
            if (state == null) return;
            System.Collections.Generic.List<Pawn> group = __result.ToList();
            foreach (XMT_CompanionLink link in state.Companions.ToList())
            {
                if (!link.Active || link.operationId != null || link.owner.Dead || link.owner.IsPrisoner || link.owner.IsSlave || !group.Contains(link.owner)) continue;
                Pawn companion = state.ResolveCompanion(link, group);
                if (group.Contains(companion)) { state.Reunited(link); continue; }
                if (!XMT_PawnStateUtility.Available(companion, state)) continue;
                companion.SetFaction(parms.faction); group.Add(companion); state.Reunited(link);
            }
            __result = group;
        }
    }
}
