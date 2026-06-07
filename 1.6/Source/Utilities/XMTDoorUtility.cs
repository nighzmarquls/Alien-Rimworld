using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal static class XMTDoorUtility
    {
        private static readonly AccessTools.FieldRef<Building_Door, bool> HoldOpenRef =
            AccessTools.FieldRefAccess<Building_Door, bool>("holdOpenInt");

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
