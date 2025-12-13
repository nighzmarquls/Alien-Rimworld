

using AlienRace;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    public class XMTMentalStateUtility
    {


        public static Thing FindEggToDestroy(Pawn pawn)
        {
            if (!pawn.Spawned)
            {
                return null;
            }

            return XMTHiveUtility.GetOvomorph(pawn.Map, false);
        }
        public static Thing FindXenoToKill(Pawn pawn)
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
        public static Thing FindXenoEnemyToKill(Pawn pawn)
        {
            if (!pawn.Spawned)
            {
                return null;
            }

            bool KillerIsXenomorph = XMTUtility.IsXenomorph(pawn);

            List<Thing> tmpTargets = new List<Thing>();
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

            List<Building_TurretGun> turrets = pawn.Map.listerThings.GetThingsOfType<Building_TurretGun>().ToList();

            for (int i = 0; i < turrets.Count;i++)
            {
                Building_TurretGun candidate = turrets[i];

                if(!candidate.Active || candidate.IsMannable)
                {
                    continue;
                }
   
                if (pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly))
                {
                    tmpTargets.Add(candidate);
                }
            }

            if (!tmpTargets.Any())
            {
                return null;
            }

            Thing result = tmpTargets[0];
            int closest = int.MaxValue;
            float worstTarget = 0;

            foreach (Thing target in tmpTargets)
            {
                float pheromone = -1;
                if (target is Building_TurretGun turret)
                {
                    if(turret.CurrentTarget == pawn)
                    {
                        return target;
                    }
                }

                if (target is Pawn targetPawn)
                {
                    if (targetPawn.Downed)
                    {
                        continue;
                    }

                    CompPawnInfo info = targetPawn.Info();
                    if (info != null)
                    {


                        pheromone = info.XenomorphPheromoneValue();
                        if (target.HostileTo(pawn))
                        {
                            pheromone -= 0.5f;
                        }
                    }
                }
                int distance = target.Position.DistanceToSquared(pawn.Position);

                if (pheromone < -5 && pheromone < worstTarget)
                {
                    result = target;
                    worstTarget = pheromone;
                    closest = distance;
                }
                else if (worstTarget > -5 && distance < closest)
                {
                    result = target;
                    worstTarget = pheromone;
                    closest = distance;
                }
            }

            tmpTargets.Clear();
            return result;
        }
    }
}
