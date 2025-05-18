

using System.Collections.Generic;
using Verse.AI;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    internal class XMTMentalStateUtility
    {
        private static List<Pawn> tmpTargets = new List<Pawn>();

        public static Pawn FindPawnToKill(Pawn pawn)
        {
            if (!pawn.Spawned)
            {
                return null;
            }

            bool KillerIsXenomorph = XMTUtility.IsXenomorph(pawn);


            tmpTargets.Clear();
            IReadOnlyList<Pawn> allPawnsSpawned = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                Pawn candidate = allPawnsSpawned[i];
                if (KillerIsXenomorph && XMTUtility.IsMorphing(candidate) || XMTUtility.HasEmbryo(candidate) || XMTUtility.IsXenomorph(candidate))
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
            float worstTarget = 0;

            foreach (Pawn target in tmpTargets)
            {
                CompPawnInfo info = target.GetComp<CompPawnInfo>();
                if (info != null)
                {
                    float pheromone = info.XenomorphPheromoneValue();

                    if (pheromone < worstTarget)
                    {
                        result = target;
                        worstTarget = pheromone;
                    }
                }
            }
            
            tmpTargets.Clear();
            return result;
        }
    }
}
