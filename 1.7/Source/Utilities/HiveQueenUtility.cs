using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    // Identity is independent of map presence. The owner (map, caravan, throne, etc.) saves the pawn.
    public static class HiveQueenUtility
    {
        private static Game lastGame;
        private static int nextRecoveryTick;
        public static bool IsValid(Pawn pawn) => pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded && pawn.GetComp<CompQueen>() != null;
        public static bool IsProtectedPlayerQueen(Pawn pawn) => IsValid(pawn) && pawn.Faction == Faction.OfPlayerSilentFail;
        public static Pawn Resolve()
        {
            Game game = Current.Game;
            GameComponent_Xenomorph component = game?.GetComponent<GameComponent_Xenomorph>();
            if (component == null)
                return null;
            if (IsProtectedPlayerQueen(component.Queen))
                return component.Queen;
            Pawn fallback = IsValid(component.Queen) ? component.Queen : null;
            component.Queen = fallback;
            if (Find.World == null)
                return fallback;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (lastGame == game && now < nextRecoveryTick)
                return fallback;
            lastGame = game;
            nextRecoveryTick = now + 250;
            var candidates = new HashSet<Pawn>();
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
                if (IsProtectedPlayerQueen(pawn))
                    candidates.Add(pawn);
            foreach (Pawn pawn in PawnsFinder.AllCaravansAndTravellingTransporters_Alive)
                if (IsProtectedPlayerQueen(pawn))
                    candidates.Add(pawn);
            foreach (IThingHolder holder in PawnsFinder.AllMapsWorldAndTemporary_Alive.Cast<IThingHolder>().Concat(Find.WorldObjects.AllWorldObjects.OfType<IThingHolder>()))
                foreach (Thing thing in ThingOwnerUtility.GetAllThingsRecursively(holder, true))
                    if (thing is Pawn pawn && IsProtectedPlayerQueen(pawn))
                        candidates.Add(pawn);
            // Map pawn lists need not contain unspawned occupants of nested building holders.
            foreach (Map map in Find.Maps)
                foreach (IThingHolder holder in map.listerThings.AllThings.OfType<IThingHolder>())
                    foreach (Thing thing in ThingOwnerUtility.GetAllThingsRecursively(holder, true))
                        if (thing is Pawn pawn && IsProtectedPlayerQueen(pawn))
                            candidates.Add(pawn);
            component.Queen = candidates.OrderBy(p => p.thingIDNumber).FirstOrDefault() ?? fallback;
            return component.Queen;
        }

        public static void Register(Pawn pawn)
        {
            if (!IsValid(pawn))
                return;
            GameComponent_Xenomorph component = Current.Game?.GetComponent<GameComponent_Xenomorph>();
            if (component == null)
                return;
            if (!IsValid(component.Queen) || (!IsProtectedPlayerQueen(component.Queen) && IsProtectedPlayerQueen(pawn)))
                component.Queen = pawn;
            lastGame = null;
        }
    }
}
