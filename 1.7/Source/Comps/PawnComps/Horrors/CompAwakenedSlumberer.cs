using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class CompAwakenedSlumberer : ThingComp
    {
        CompAwakenedSlumbererProperties Props => props as CompAwakenedSlumbererProperties;
        int nextPlantingTick = Find.TickManager.TicksGame;
        float bodySize = 0f;

        public float BodySize => bodySize;
        
        Pawn Parent => parent as Pawn;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextPlantingTick, "nextPlantingTick", Find.TickManager.TicksGame);
            Scribe_Values.Look(ref bodySize, "bodySize", Find.TickManager.TicksGame);
        }

        public void InitializeSleeper(float initbodySize)
        {
            bodySize = initbodySize;
            if (ExternalDefOf.SM_BodySizeOffset != null)
            {
                ExternalDefOf.SM_BodySizeOffset.Worker.ClearCacheForThing(parent);
            }
        }

        public override float GetStatOffset(StatDef stat)
        {
            if (stat == ExternalDefOf.SM_BodySizeOffset)
            {
                if (bodySize > 0f)
                {
                    return (bodySize - Parent.RaceProps.baseBodySize);
                }
            }
            return base.GetStatOffset(stat);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            nextPlantingTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.plantingIntervalHour * 2500);
        }
        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
            {
                return;
            }
            if (Parent != null)
            {
                if (Parent.Awake() && !Parent.Downed )
                {
                    int currentTick = Find.TickManager.TicksGame;
                    if (Find.TickManager.TicksGame > nextPlantingTick)
                    {
                        nextPlantingTick = currentTick + Mathf.CeilToInt(Props.plantingIntervalHour * 2500);
                        if (currentTick > Parent.LastAttackTargetTick + 2500)
                        {
                            IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(Parent.Position, Props.plantingRadius, true);

                            foreach (IntVec3 cell in cells)
                            {
                                if (cell.GetFertility(parent.Map) >= Props.minimumFertility)
                                {
                                    if (XMTUtility.TransformPawnIntoThing(Parent, Props.plantingResult, out Thing result))
                                    {
                                        return;
                                    }

                                }
                            }
                        }
                    }
                }
                else
                {
                    int currentTick = Find.TickManager.TicksGame;
                    if (Find.TickManager.TicksGame > nextPlantingTick)
                    {
                        nextPlantingTick = currentTick + Mathf.CeilToInt(Props.plantingIntervalHour * 2500);
                        
                        IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(Parent.Position, Props.plantingRadius, true);

                        foreach (IntVec3 cell in cells)
                        {
                            if (cell.GetFertility(parent.Map) >= Props.minimumFertility)
                            {
                                if (XMTUtility.TransformPawnIntoThing(Parent, Props.plantingResult, out Thing FertileResult))
                                {
                          
                                    return;
                                }

                            }
                        }
                        
                        if(XMTUtility.TransformPawnIntoThing(Parent, Props.plantingResult, out Thing result))
                        {
                            return;
                        }
                    }
                }
            }
        }
    }

    public class CompAwakenedSlumbererProperties : CompProperties
    {
        public float plantingIntervalHour = 1;
        public float plantingRadius = 3;
        public float minimumFertility = 0.1f;
        public ThingDef plantingResult;
        public CompAwakenedSlumbererProperties()
        {
            this.compClass = typeof(CompAwakenedSlumberer);
        }
        public CompAwakenedSlumbererProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
