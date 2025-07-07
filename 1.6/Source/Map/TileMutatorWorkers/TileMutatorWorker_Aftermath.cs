
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Xenomorphtype
{
    internal class TileMutatorWorker_Aftermath : TileMutatorWorker
    {
        Map lastMap = null;
        public TileMutatorWorker_Aftermath(TileMutatorDef def) : base(def)
        {
        }
        public override void Init(Map map)
        {
            if(lastMap == map)
            {
                return;
            }
            if (XenoformingUtility.XenoformingMeets(1))
            {
                float chance = XenoformingUtility.ChanceByXenoforming(XMTSettings.SiteAttackChance);
                if (Rand.Chance(chance))
                {
                    if (!def.extraGenSteps.Contains(XenoMapDefOf.XMT_AttackAftermath))
                    {
                        def.extraGenSteps.Add(XenoMapDefOf.XMT_AttackAftermath);
                    }
                    lastMap = map;
                }
                else
                {
                    
                    if (def.extraGenSteps.Contains(XenoMapDefOf.XMT_AttackAftermath))
                    {
                        def.extraGenSteps.Remove(XenoMapDefOf.XMT_AttackAftermath);
                    }
                    
                }
            }
        }

        public override void GeneratePostFog(Map map)
        {
            if (lastMap == map)
            {
                foreach (Pawn pawn in map.mapPawns.AllHumanlike)
                {
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

                foreach(Corpse corpse in map.listerThings.GetThingsOfType<Corpse>())
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
                lastMap = null;
            }
        }

        public override string GetLabel(PlanetTile tile)
        {
            return "";
        }

        public override string GetDescription(PlanetTile tile)
        {
            return "";
        }
    }
}
