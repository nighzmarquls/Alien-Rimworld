

using System.Collections.Generic;
using Verse.AI;
using Verse;
using static UnityEngine.GraphicsBuffer;
using RimWorld;

namespace Xenomorphtype
{
    internal class XMTMentalStateUtility
    {


        public static Thing FindEggToDestroy(Pawn pawn)
        {
            if (!pawn.Spawned)
            {
                return null;
            }

            return HiveUtility.GetOvamorph(pawn.Map, false);
        }
        public static Pawn FindXenoToKill(Pawn pawn)
        {
            if (!pawn.Spawned)
            {
                return null;
            }

            List<Pawn> tmpTargets = new List<Pawn>();
            IReadOnlyList<Pawn> allPawnsSpawned = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                Pawn candidate = allPawnsSpawned[i];
                if (!XMTUtility.IsXenomorph(candidate))
                {
                    continue;
                }

                if (pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly) && (candidate.CurJob == null || !candidate.CurJob.exitMapOnArrival))
                {
                    tmpTargets.Add(candidate);
                }
            }

            if (!tmpTargets.Any())
            {
                return null;
            }

            Pawn result = tmpTargets.RandomElement();

            tmpTargets.Clear();
            return result;
        }
        public static Pawn FindXenoEnemyToKill(Pawn pawn)
        {
            if (!pawn.Spawned)
            {
                return null;
            }

            bool KillerIsXenomorph = XMTUtility.IsXenomorph(pawn);

            List<Pawn> tmpTargets = new List<Pawn>();
            IReadOnlyList<Pawn> allPawnsSpawned = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                Pawn candidate = allPawnsSpawned[i];
                if (KillerIsXenomorph && ( XMTUtility.IsXenomorphFriendly(candidate) || XMTUtility.IsMorphing(candidate) || XMTUtility.HasEmbryo(candidate) || XMTUtility.IsXenomorph(candidate)))
                {
                    continue;
                }

                if ( pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly) && (candidate.CurJob == null || !candidate.CurJob.exitMapOnArrival))
                {
                    tmpTargets.Add(candidate);
                }
            }

            if (!tmpTargets.Any())
            {
                return null;
            }

            Pawn result = tmpTargets[0];
            int closest = int.MaxValue;
            float worstTarget = 0;

            foreach (Pawn target in tmpTargets)
            {
                if (target.Downed)
                {
                    continue;
                }

                CompPawnInfo info = target.Info();
                if (info != null)
                {
                    float pheromone = info.XenomorphPheromoneValue();

                    
                    if(target.HostileTo(pawn))
                    {
                        pheromone -= 0.5f;
                    }

                    int distance = target.Position.DistanceToSquared(pawn.Position);

                    if (pheromone < -5 && pheromone < worstTarget)
                    {
                        result = target;
                        worstTarget = pheromone;
                        closest = distance;
                    }
                    else if( worstTarget > -5 && distance < closest)
                    {
                        result = target;
                        worstTarget = pheromone;
                        closest = distance;
                    }
                    
                }
            }
            
            tmpTargets.Clear();
            return result;
        }
    }
}
