
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Xenomorphtype
{
    internal class TileMutatorWorker_Aftermath : TileMutatorWorker
    {
  
        public TileMutatorWorker_Aftermath(TileMutatorDef def) : base(def)
        {

        }
        public override void Init(Map map)
        {

            if (XenoformingUtility.XenoformingMeets(1))
            {
                float chance = XenoformingUtility.ChanceByXenoforming(XMTSettings.SiteAttackChance);
                if (Rand.Chance(chance))
                {
                    if (!def.extraGenSteps.Contains(XenoMapDefOf.XMT_AttackAftermath))
                    {
                        def.extraGenSteps.Add(XenoMapDefOf.XMT_AttackAftermath);
                        def.extraGenSteps.Add(XenoMapDefOf.XMT_AbductPopulation);
                    }

                }
                else
                {
                    
                    if (def.extraGenSteps.Contains(XenoMapDefOf.XMT_AttackAftermath))
                    {
                        def.extraGenSteps.Remove(XenoMapDefOf.XMT_AttackAftermath);
                        def.extraGenSteps.Remove(XenoMapDefOf.XMT_AbductPopulation);
                    }
                    
                }
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
