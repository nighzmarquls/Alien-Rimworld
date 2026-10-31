using System;
using System.Collections.Generic;
using System.Linq;
using AlienRace;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Xenomorphtype
{
    internal static class DebugActions_XMTWorldActivities
    {
        private const string Category = "Alien | Rimworld";
        private enum SpawnMode { Original, Colonist, Prisoner, Slave }
        private static GameComponent_XMTWorldState State => Current.Game.GetComponent<GameComponent_XMTWorldState>();
        private static Pawn Selected => Find.Selector.SingleSelectedThing as Pawn ?? (Find.Selector.SingleSelectedThing as Corpse)?.InnerPawn;
        private static void Report(string text) => Log.Message(text);
        private static DebugActionNode Command(string label, Action action, DebugActionType type = DebugActionType.Action) =>
            new DebugActionNode(label, type, () =>
            {
                Log.Message("[XMT][WorldActivities][Debug] " + label + " tick=" + Find.TickManager.TicksGame);
                action();
            });
        private static DebugActionNode Group(string label, params DebugActionNode[] children) =>
            new DebugActionNode(label, DebugActionType.Action, null) { childGetter = () => children.ToList() };
        private static DebugActionNode Picker(string label, Func<List<DebugActionNode>> children) =>
            new DebugActionNode(label, DebugActionType.Action, null) { childGetter = children };
        private static DebugActionNode Root(string label, params DebugActionNode[] children)
        {
            DebugActionNode node = Group(label, children);
            node.category = Category;
            node.visibilityGetter = () => Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null;
            return node;
        }

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> WorldActivityNodes()
        {
            yield return Root("Companion",
                Group("Pawn fixtures",
                    Picker("Retrieve world pawn...", WorldPawnNodes),
                    Picker("Retrieve existing or create from bio...", BioNodes)),
                Group("Inspect",
                    Command("Selected owner: decisions and mission state", () => WithOwner(p => Report(State.DebugCompanions(p)))),
                    Command("All companion relationships and tracked pawns", () => Report(State.DebugCompanions())),
                    Command("Definitions, responses and backstory associations", () => Report(State.DebugCompanionDefinitions()))),
                Group("Exercise",
                    Command("Resolve selected owner's companion off-map", ResolveSelected),
                    Command("Make selected owner's response due", () => WithOwner(p => { State.DebugMakeCompanionsDue(p); Report(State.DebugCompanions(p)); })),
                    Command("Evaluate companion watcher now (honors timers)", () => Evaluate(typeof(XMT_Watcher_Companions))),
                    Command("Make selected owner due and evaluate", () => WithOwner(p =>
                    { State.DebugMakeCompanionsDue(p); Evaluate(typeof(XMT_Watcher_Companions)); Report(State.DebugCompanions(p)); }))));
            yield return Root("Reprisal",
                Command("Inspect budget, eligibility and queued incidents", () => Report(State.DebugReprisals())),
                Picker("Add two threat budgets from faction...", () => FactionNodes(true, faction =>
                {
                    float points = StorytellerUtility.DefaultThreatPointsNow(Find.CurrentMap) * 2f;
                    State.AccumulateReprisal(faction, points, Find.CurrentMap);
                    Report(State.DebugReprisals());
                })),
                Command("Make next reprisal check due", () => { State.DebugReprisalDue(); Report(State.DebugReprisals()); }),
                Command("Evaluate watcher now (honors cooldown)", () => { Evaluate(typeof(XMT_Watcher_Reprisal)); Report(State.DebugReprisals()); }),
                Command("Make check due and evaluate", () => { State.DebugReprisalDue(); Evaluate(typeof(XMT_Watcher_Reprisal)); Report(State.DebugReprisals()); }));
            yield return Root("Player action",
                Command("Inspect arrivals, queen defense and watcher clocks", () => Report(State.DebugPlayerActions())),
                Picker("Preview Siren settlement candidates...", () => SirenNodes(definition => WithPlayerPawn(p => Report(State.DebugPreviewSiren(p, definition))))),
                Picker("Trigger Siren off-map response (changes diplomacy)...", () => SirenNodes(TriggerSiren)),
                Picker("Queue test visitor/trader...", SirenNodesForVisitor),
                Command("Make queued arrivals due (refresh expiry)", () => { State.DebugArrivalsDue(); Report(State.DebugPlayerActions()); }),
                Command("Evaluate player-action watcher now", () => { Evaluate(typeof(XMT_Watcher_PlayerAction)); Report(State.DebugPlayerActions()); }),
                Command("Selected player queen: request aid against clicked pawn", QueenAid, DebugActionType.ToolMap));
        }

        private static void WithOwner(Action<Pawn> action)
        {
            Pawn owner = Selected;
            if (owner == null) { Report("[XMT][Companions] Select an owner pawn or its corpse first."); return; }
            if (!State.Companions.Any(l => l.owner == owner))
            { Report("[XMT][Companions] No registered companion relationship for " + owner + ". Inspect definitions/associations or spawn a configured bio."); return; }
            action(owner);
        }
        private static void WithPlayerPawn(Action<Pawn> action)
        {
            Pawn pawn = Selected;
            if (pawn?.Spawned != true || pawn.Faction != Faction.OfPlayer)
            { Report("[XMT][PlayerAction] Select a spawned player pawn first."); return; }
            action(pawn);
        }
        private static void Evaluate(Type worker)
        {
            foreach (XMT_WatcherDef definition in DefDatabase<XMT_WatcherDef>.AllDefsListForReading.Where(d => d.workerClass == worker))
            {
                Report("[XMT][WorldActivities] Evaluating " + definition.defName + " through its real worker; policy timers and eligibility remain active.");
                definition.Worker.Tick(State, Find.TickManager.TicksGame);
            }
        }
        private static void ResolveSelected() => WithOwner(owner =>
        {
            foreach (XMT_CompanionLink link in State.Companions.Where(l => l.owner == owner && l.Active).ToList())
                Report("[XMT][Companions] Resolved off-map companion: " + State.ResolveCompanion(link));
            Report(State.DebugCompanions(owner));
        });

        private static List<DebugActionNode> WorldPawnNodes() => Find.WorldPawns.AllPawnsAlive
            .Where(XMT_PawnStateUtility.StructurallyValid).OrderBy(p => p.LabelShort).Select(pawn =>
                Command(pawn.LabelShort + " / " + pawn.ThingID + " / " + pawn.Faction, () => SpawnWorldPawn(pawn, SpawnMode.Original), DebugActionType.ToolMap)).ToList();

        private static List<DebugActionNode> BioNodes() => DefDatabase<PawnBioDef>.AllDefsListForReading.OrderBy(b => b.defName)
            .Select(bio => Group(bio.defName,
                Command("As player colonist", () => SpawnBio(bio, SpawnMode.Colonist), DebugActionType.ToolMap),
                Command("As player prisoner", () => SpawnBio(bio, SpawnMode.Prisoner), DebugActionType.ToolMap),
                Command("As player slave", () => SpawnBio(bio, SpawnMode.Slave), DebugActionType.ToolMap))).ToList();

        private static string RetrievalBlock(Pawn pawn)
        {
            string invalid = XMT_PawnStateUtility.StructuralInvalidity(pawn);
            if (invalid != null) return invalid;
            if (pawn.Dead) return "dead";
            if (pawn.Spawned) return "already spawned on map " + pawn.Map.uniqueID;
            if (pawn.ParentHolder != null || CaravanUtility.IsCaravanMember(pawn)) return "held or traveling in a caravan";
            if (State.Claimed(pawn)) return "claimed by a live deployment";
            return null;
        }
        private static void SpawnWorldPawn(Pawn pawn, SpawnMode mode)
        {
            string blocked = RetrievalBlock(pawn);
            if (blocked != null) { Report("[XMT][Companions] Retrieval blocked: " + blocked + ". No replacement was created."); return; }
            if (!Find.WorldPawns.Contains(pawn)) { Report("[XMT][Companions] Pawn is no longer in WorldPawns."); return; }
            Place(pawn, mode, "retrieved original world pawn");
        }
        private static void SpawnBio(PawnBioDef bio, SpawnMode mode)
        {
            var source = new XMT_CompanionSource_Bio { bio = bio };
            Pawn existing = Find.WorldPawns.AllPawnsAlive.Concat(Find.Maps.SelectMany(m => m.mapPawns.AllPawns)).FirstOrDefault(source.Matches);
            if (existing != null) { SpawnWorldPawn(existing, mode); return; }
            source.pawnKind = bio.validRaces.NullOrEmpty() || bio.validRaces.Contains(ThingDefOf.Human) ? PawnKindDefOf.Colonist
                : DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => bio.validRaces.Contains(k.race));
            if (source.pawnKind == null) { Report("[XMT][Companions] No pawnkind for bio races: " + bio.defName); return; }
            var stages = source.pawnKind.race.race.lifeStageAges;
            float adultAge = stages.NullOrEmpty() ? 1f : stages.Last().minAge + 1f;
            FloatRange age = source.pawnKind.race == ThingDefOf.Human ? new FloatRange(25f, 45f) : new FloatRange(adultAge, adultAge + 1f);
            if (bio.defName == "XMT_BoyKavalier07") age = new FloatRange(16f, 18f);
            if (bio.defName == "XMT_Atom08") age = new FloatRange(40f, 50f);
            if (bio.defName == "XMT_RomulusGirl00") age = new FloatRange(20f, 25f);
            Place(source.Create(null, new XMT_CompanionDef { biologicalAge = age }), mode, "created exact bio " + bio.defName);
        }
        private static void Place(Pawn pawn, SpawnMode mode, string origin)
        {
            if (pawn == null) { Report("[XMT][Companions] Generation returned no pawn."); return; }
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map) || !cell.Standable(map))
            {
                if (!Find.WorldPawns.Contains(pawn)) Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                Report("[XMT][Companions] Choose a standable cell. Pawn retained in WorldPawns: " + pawn.ThingID); return;
            }
            if (mode != SpawnMode.Original)
            {
                pawn.guest?.SetGuestStatus(null);
                Faction faction = mode == SpawnMode.Colonist ? Faction.OfPlayer
                    : Find.FactionManager.AllFactionsVisibleInViewOrder.FirstOrDefault(f => !f.IsPlayer && !f.defeated && f.def.humanlikeFaction) ?? Faction.OfAncients;
                pawn.SetFaction(faction);
            }
            GenSpawn.Spawn(pawn, cell, map);
            if (mode == SpawnMode.Prisoner || mode == SpawnMode.Slave)
                pawn.guest?.SetGuestStatus(Faction.OfPlayer, mode == SpawnMode.Prisoner ? GuestStatus.Prisoner : GuestStatus.Slave);
            Find.Selector.ClearSelection(); Find.Selector.Select(pawn);
            Report("[XMT][Companions] " + origin + ": " + pawn + " id=" + pawn.ThingID + " mode=" + mode);
            Report(State.DebugCompanions(pawn));
        }

        private static List<DebugActionNode> FactionNodes(bool hostile, Action<Faction> action) => Find.FactionManager.AllFactionsVisibleInViewOrder
            .Where(f => !f.IsPlayer && !f.defeated && !f.temporary && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) == hostile
                && (!hostile || f.def.canStageAttacks))
            .Select(f => Command(f.Name + " / " + f.def.defName, () => action(f))).ToList();
        private static List<DebugActionNode> SirenNodes(Action<XMT_SirenResponseDef> action) =>
            DefDatabase<XMT_SirenResponseDef>.AllDefsListForReading.Select(d => Command(d.defName, () => action(d))).ToList();
        private static List<DebugActionNode> SirenNodesForVisitor() => DefDatabase<XMT_SirenResponseDef>.AllDefsListForReading
            .Select(d => Picker(d.defName, () => FactionNodes(false, faction =>
            { State.DebugQueueArrival(faction, Find.CurrentMap, d); Report(State.DebugPlayerActions()); }))).ToList();

        private static void TriggerSiren(XMT_SirenResponseDef response) => WithPlayerPawn(caster =>
        {
            var vanilla = DefDatabase<RimWorld.AbilityDef>.GetNamedSilentFail("XMT_SirenBroadcast")?.comps?.OfType<CompProperties_SirenBroadcast>().FirstOrDefault();
            var expanded = DefDatabase<VEF.Abilities.AbilityDef>.GetNamedSilentFail("XMT_SirenBroadcast")?.GetModExtension<AbilityExtension_SirenBroadcast>();
            if (vanilla == null && expanded == null)
            { Report("[XMT][PlayerAction] No Siren ability definition is loaded. Use Queue test visitor/trader to exercise the arrival watcher independently."); return; }
            ((XMT_SirenResponseWorker)response.ResponseWorker).Notify(State, response, caster, vanilla?.friendlyPrecepts ?? expanded?.friendlyPrecepts,
                vanilla?.hostilePrecepts ?? expanded?.hostilePrecepts, vanilla?.goodwillImpactForFriendly ?? expanded.goodwillImpactForFriendly,
                vanilla?.goodwillImpactForHostile ?? expanded.goodwillImpactForHostile);
            Report(State.DebugPlayerActions());
            Report(State.DebugReprisals());
        });
        private static void QueenAid() => WithPlayerPawn(queen =>
        {
            Pawn aggressor = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
            if (!XMTUtility.IsQueen(queen) || aggressor == null)
            { Report("[XMT][PlayerAction] Select a player queen and click an aggressor pawn."); return; }
            bool accepted = Current.Game.GetComponent<GameComponent_Xenomorph>().TryQueenCallForAid(queen, aggressor);
            Report("[XMT][PlayerAction] Queen aid request accepted=" + accepted + " xenoforming=" + XenoformingUtility.GetXenoforming());
            Report(State.DebugPlayerActions());
        });
    }
}
