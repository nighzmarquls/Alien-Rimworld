using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class XMTContainmentUtility
    {
        private const string HoldingPlatformTypeName = "RimWorld.Building_HoldingPlatform";
        private const string EntityHolderCompTypeName = "RimWorld.CompEntityHolder";

        private static readonly Dictionary<Type, PropertyInfo> heldPawnProperties = new Dictionary<Type, PropertyInfo>();
        private static readonly Dictionary<Type, PropertyInfo> containmentStrengthProperties = new Dictionary<Type, PropertyInfo>();

        public static Pawn HeldPawn(Thing holder)
        {
            if (holder is Building_ContainmentHarness harness)
            {
                return harness.ContainedThing as Pawn;
            }

            if (!IsAnomalyHoldingPlatform(holder))
            {
                return null;
            }

            Type type = holder.GetType();
            if (!heldPawnProperties.TryGetValue(type, out PropertyInfo property))
            {
                property = type.GetProperty("HeldPawn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                heldPawnProperties[type] = property;
            }
            return property?.GetValue(holder) as Pawn;
        }

        public static Thing Holder(Pawn pawn)
        {
            IThingHolder parentHolder = pawn?.ParentHolder;
            Thing holder = parentHolder as Thing;
            if (holder == null && parentHolder is ThingComp holderComp)
            {
                holder = holderComp.parent;
            }
            return HeldPawn(holder) == pawn ? holder : null;
        }

        public static bool IsHeld(Pawn pawn)
        {
            return Holder(pawn) != null;
        }

        public static bool Eject(Pawn pawn)
        {
            Thing holder = Holder(pawn);
            if (holder is Building_ContainmentHarness harness)
            {
                harness.EjectContents();
                return true;
            }

            ThingComp entityHolder = FindEntityHolderComp(holder);
            MethodInfo eject = entityHolder?.GetType().GetMethod("EjectContents",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (eject != null)
            {
                eject.Invoke(entityHolder, null);
                return true;
            }

            return false;
        }

        public static bool TryGetContainmentQuality(Thing holder, out float quality)
        {
            if (holder is Building_ContainmentHarness harness && harness.ContainmentComp != null)
            {
                quality = harness.ContainmentComp.ContainmentQuality;
                return true;
            }

            ThingComp entityHolder = FindEntityHolderComp(holder);
            if (entityHolder != null)
            {
                Type type = entityHolder.GetType();
                if (!containmentStrengthProperties.TryGetValue(type, out PropertyInfo property))
                {
                    property = type.GetProperty("ContainmentStrength",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    containmentStrengthProperties[type] = property;
                }
                if (property?.GetValue(entityHolder) is float strength)
                {
                    quality = strength;
                    return true;
                }
            }

            quality = 0f;
            return false;
        }

        public static bool IsAnomalyHoldingPlatform(Thing thing)
        {
            return IsTypeOrSubclassNamed(thing?.GetType(), HoldingPlatformTypeName);
        }

        public static bool IsAnomalyHoldingPlatformDef(ThingDef def)
        {
            return IsTypeOrSubclassNamed(def?.thingClass, HoldingPlatformTypeName);
        }

        public static bool CanAcceptPawn(Thing destination, Pawn pawn)
        {
            if (destination is Building_ContainmentHarness harness)
            {
                return harness.ContainedThing == null &&
                    harness.ContainmentComp?.CanContain(pawn, requireDowned: false) == true;
            }

            ThingComp entityHolder = FindEntityHolderComp(destination);
            if (entityHolder == null || HeldPawn(destination) != null)
            {
                return false;
            }

            PropertyInfo available = entityHolder.GetType().GetProperty("Available",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return available?.GetValue(entityHolder) is bool isAvailable && isAvailable;
        }

        public static bool TryAcceptPawn(Thing destination, Pawn pawn)
        {
            if (!CanAcceptPawn(destination, pawn))
            {
                return false;
            }

            if (destination is Building_ContainmentHarness harness)
            {
                return harness.TryAcceptPawn(pawn);
            }

            ThingComp entityHolder = FindEntityHolderComp(destination);
            PropertyInfo containerProperty = entityHolder?.GetType().GetProperty("Container",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (containerProperty?.GetValue(entityHolder) is not ThingOwner container ||
                pawn.holdingOwner == null || !pawn.holdingOwner.TryTransferToContainer(pawn, container, false))
            {
                return false;
            }

            NotifyHeldOnAnomalyPlatform(pawn, container);
            pawn.GetComp<CompStealth>()?.ForceVisible();
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            return true;
        }

        public static bool IsTransferDestination(Thing destination, Pawn pawn)
        {
            if (CanAcceptPawn(destination, pawn))
            {
                return true;
            }

            return destination is Building_Bed bed && pawn?.IsPrisonerOfColony == true &&
                bed.ForPrisoners && bed.AnyUnoccupiedSleepingSlot && RestUtility.CanUseBedEver(pawn, bed.def);
        }

        private static ThingComp FindEntityHolderComp(Thing holder)
        {
            if (holder is not ThingWithComps thingWithComps || !IsAnomalyHoldingPlatform(holder))
            {
                return null;
            }

            foreach (ThingComp comp in thingWithComps.AllComps)
            {
                if (IsTypeOrSubclassNamed(comp?.GetType(), EntityHolderCompTypeName))
                {
                    return comp;
                }
            }
            return null;
        }

        private static void NotifyHeldOnAnomalyPlatform(Pawn pawn, ThingOwner container)
        {
            if (pawn?.AllComps == null)
            {
                return;
            }

            foreach (ThingComp comp in pawn.AllComps)
            {
                MethodInfo notify = comp?.GetType().GetMethod("Notify_HeldOnPlatform",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(ThingOwner) }, null);
                if (notify != null)
                {
                    notify.Invoke(comp, new object[] { container });
                    return;
                }
            }
        }

        private static bool IsTypeOrSubclassNamed(Type type, string fullName)
        {
            while (type != null)
            {
                if (type.FullName == fullName)
                {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }
    }

    public abstract class WorkGiver_XMTContainedPawn : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                yield break;
            }

            foreach (Building building in pawn.Map.listerBuildings.allBuildingsColonist)
            {
                if (XMTContainmentUtility.HeldPawn(building) != null)
                {
                    yield return building;
                }
            }

        }

        protected abstract Pawn GetEntity(Thing potentialHolder);
    }
}
