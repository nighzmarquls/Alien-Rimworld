using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal static class XMTDoorUtility
    {
        internal const int MinimumContainmentDoorMaxHitPoints = 160;
        internal const int PoweredDoorSabotageScore = 10;

        private static readonly AccessTools.FieldRef<Building_Door, bool> HoldOpenRef =
            AccessTools.FieldRefAccess<Building_Door, bool>("holdOpenInt");

        internal static bool HasPoweredResistance(Building_Door door)
        {
            return door?.TryGetComp<CompPowerTrader>()?.PowerOn == true;
        }

        internal static bool MeetsContainmentStrength(Building_Door door)
        {
            return door != null && door.MaxHitPoints >= MinimumContainmentDoorMaxHitPoints;
        }

        internal static bool IsSecureContainmentDoor(Building_Door door)
        {
            return HasPoweredResistance(door) && MeetsContainmentStrength(door);
        }

        internal static bool CanForceOpenConventionally(Building_Door door)
        {
            return door != null && !HasPoweredResistance(door);
        }

        internal static float PoweredWeakness(Building_Door door)
        {
            if (!HasPoweredResistance(door) || MeetsContainmentStrength(door))
            {
                return 0f;
            }

            return UnityEngine.Mathf.Clamp01(
                (MinimumContainmentDoorMaxHitPoints - door.MaxHitPoints) /
                (float)MinimumContainmentDoorMaxHitPoints);
        }

        internal static void ForceHoldOpenAndOpen(Building_Door door, Pawn opener)
        {
            if (door == null)
            {
                return;
            }

            HoldOpenRef(door) = true;
            door.StartManualOpenBy(opener);
        }
    }
}
