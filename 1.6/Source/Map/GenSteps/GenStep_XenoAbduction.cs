

using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class GenStep_XenoAbduction : GenStep
    {
        public override int SeedPart => 9872125;

        public override void Generate(Map map, GenStepParams parms)
        {
            foreach (Pawn pawn in map.mapPawns.AllHumanlike)
            {
                if(pawn == null)
                {
                    continue;
                }

                if ((bool)pawn.Faction?.IsPlayer)
                {
                    XenoformingUtility.IncreaseXenoforming(1);
                    FilthMaker.TryMakeFilth(pawn.PositionHeld, map, InternalDefOf.Starbeast_Filth_Resin);
                    continue;
                }

                if (pawn.Dead)
                {
                    pawn.Corpse.Destroy();
                }
                else
                {
                    pawn.Destroy();
                }
            }

            foreach (Corpse corpse in map.listerThings.GetThingsOfType<Corpse>())
            {
                corpse.Destroy();
            }

            foreach (Building building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (!building.Spawned)
                {
                    continue;
                }

                if (building is Building_GibbetCage cage)
                {
                    if (cage.ContainedThing == null)
                    {
                        continue;
                    }

                    cage.ContainedThing.Destroy();
                    FilthMaker.TryMakeFilth(building.PositionHeld, map, InternalDefOf.Starbeast_Filth_Resin);
                }
                else if (building is Building_Turret turret)
                {
                    CompBreakdownable breakdownable = turret.GetComp<CompBreakdownable>();
                    breakdownable.DoBreakdown();
                }
            }
        }
    }
}
