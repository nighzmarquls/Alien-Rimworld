using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public abstract class Building_TreatmentDoorway : PassableRoomborder
    {
        protected const float FuelPerTreatment = 0.1f;

        private readonly HashSet<Pawn> processedOccupants = new HashSet<Pawn>();
        private CompRefuelable refuelable;

        protected CompRefuelable Refuelable => refuelable ??= GetComp<CompRefuelable>();
        protected bool CanSpendTreatmentFuel => powerComp?.PowerOn == true && Refuelable?.Fuel >= FuelPerTreatment;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            DoorPreDraw();
            base.DrawAt(drawLoc, flip);
            Comps_PostDraw();
        }

        protected override void Tick()
        {
            base.Tick();

            if (!Spawned)
            {
                processedOccupants.Clear();
                return;
            }

            CellRect occupiedRect = this.OccupiedRect();
            processedOccupants.RemoveWhere(pawn => pawn == null || pawn.Destroyed || pawn.Map != Map || !occupiedRect.Contains(pawn.Position));

            foreach (IntVec3 cell in occupiedRect)
            {
                List<Thing> things = cell.GetThingList(Map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn pawn && !processedOccupants.Contains(pawn) && TryTreatPawn(pawn))
                    {
                        processedOccupants.Add(pawn);
                    }
                }
            }
        }

        protected void SpendTreatmentFuel()
        {
            Refuelable.ConsumeFuel(FuelPerTreatment);
        }

        protected abstract bool TryTreatPawn(Pawn pawn);
    }

    public class Building_SterilizingDoorway : Building_TreatmentDoorway
    {
        protected override bool TryTreatPawn(Pawn pawn)
        {
            if (IsCoveredInFirefoam(pawn))
            {
                return true;
            }

            if (!CanSpendTreatmentFuel)
            {
                return false;
            }

            pawn.TakeDamage(new DamageInfo(DamageDefOf.Extinguish, 1f, instigator: this));
            if (!IsCoveredInFirefoam(pawn))
            {
                return false;
            }

            SpendTreatmentFuel();
            return true;
        }

        private static bool IsCoveredInFirefoam(Pawn pawn)
        {
            return pawn?.Drawer?.renderer?.FirefoamOverlays?.coveredInFoam == true;
        }
    }

    public enum PheromoneDoorwayMode
    {
        None = 0,
        Aggregation = 1,
        Reproductive = 2,
        Alarm = 3
    }

    public class Building_PheromoneDoorway : Building_TreatmentDoorway
    {
        private const float PheromoneDose = 0.25f;

        private PheromoneDoorwayMode selectedMode;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref selectedMode, "selectedPheromoneMode", PheromoneDoorwayMode.None);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            yield return new Command_Action
            {
                defaultLabel = "XMT_PheromoneDoorway_SelectLabel".Translate(),
                defaultDesc = "XMT_PheromoneDoorway_SelectDescription".Translate(PheromoneModeLabel(selectedMode)),
                icon = InternalDefOf.XMT_RawPheromone.uiIcon,
                action = OpenSelectionMenu
            };
        }

        protected override bool TryTreatPawn(Pawn pawn)
        {
            if (selectedMode == PheromoneDoorwayMode.None)
            {
                return true;
            }

            if (!CanSpendTreatmentFuel)
            {
                return false;
            }

            float aggregation = selectedMode == PheromoneDoorwayMode.Aggregation ? PheromoneDose : 0f;
            float reproductive = selectedMode == PheromoneDoorwayMode.Reproductive ? PheromoneDose : 0f;
            float alarm = selectedMode == PheromoneDoorwayMode.Alarm ? PheromoneDose : 0f;
            if (!PheromoneUtility.TryApplyArtificial(pawn, aggregation, reproductive, alarm))
            {
                return false;
            }

            SpendTreatmentFuel();
            return true;
        }

        private void OpenSelectionMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                MakeSelectionOption(PheromoneDoorwayMode.None),
                MakeSelectionOption(PheromoneDoorwayMode.Aggregation),
                MakeSelectionOption(PheromoneDoorwayMode.Reproductive),
                MakeSelectionOption(PheromoneDoorwayMode.Alarm)
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private FloatMenuOption MakeSelectionOption(PheromoneDoorwayMode mode)
        {
            return new FloatMenuOption(PheromoneModeLabel(mode), () => selectedMode = mode);
        }

        private static string PheromoneModeLabel(PheromoneDoorwayMode mode)
        {
            return mode switch
            {
                PheromoneDoorwayMode.Aggregation => "XMT_Pheromone_Aggregation".Translate(),
                PheromoneDoorwayMode.Reproductive => "XMT_Pheromone_Reproductive".Translate(),
                PheromoneDoorwayMode.Alarm => "XMT_Pheromone_Alarm".Translate(),
                _ => "XMT_Pheromone_None".Translate()
            };
        }
    }
}
