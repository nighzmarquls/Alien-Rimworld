using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static UnityEngine.Scripting.GarbageCollector;
using Verse.Noise;

namespace Xenomorphtype
{
    public class Petrolsump : XMTBase_Building
    {
        CompPetrolsump petrolSump;

        public float bodySize => petrolSump.TotalBodySize();
   
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic.drawSize = petrolSump.DisplaySize;
            base.DrawAt(drawLoc, flip);
            Graphic.drawSize = def.graphic.drawSize;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (petrolSump == null)
            {
                petrolSump = this.GetComp<CompPetrolsump>();
            }
      
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {

            base.DeSpawn(mode);
        }
        
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (petrolSump != null && Spawned)
            {
                int stackTotal = HitPoints;
                petrolSump.DropFuel(stackTotal);

                float explosionRadius = bodySize * 2;
                GenExplosion.DoExplosion(radius: explosionRadius, center: PositionHeld, map: MapHeld, damType: DamageDefOf.Flame, instigator: this);

                CompAcidBlood compAcidBlood = GetComp<CompAcidBlood>();

                if (compAcidBlood != null)
                {
                    compAcidBlood.CreateAcidExplosion(explosionRadius);
                }
                List<IntVec3> circle = GenRadial.RadialCellsAround(PositionHeld, explosionRadius, explosionRadius + 1).ToList();

                circle.Shuffle();
                int hp = 0;
                foreach (IntVec3 cell in circle)
                {
                    if(hp >= MaxHitPoints)
                    {
                        break;
                    }
                    petrolSump.ReleaseSpore(cell, Map);
                    hp += petrolSump.SporeMinHP;
                }
                
            }

            base.Destroy(mode);
        }

        public override void TransformedFrom(Pawn pawn, Pawn instigator)
        {
            petrolSump.SetProgenitor(pawn); 
        }
    }
}
