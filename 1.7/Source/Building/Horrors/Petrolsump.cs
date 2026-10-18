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
    public class Petrolsump : XMTAnimatedBuilding
    {
        CompPetrolsump petrolSump => GetComp<CompPetrolsump>();

        public float bodySize => petrolSump.TotalBodySize();
   

        
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!SuppressTransformationEffects && petrolSump != null && Spawned)
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
