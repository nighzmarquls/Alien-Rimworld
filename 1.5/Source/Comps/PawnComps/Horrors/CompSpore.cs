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
    internal class CompSpore : ThingComp
    {
        CompSporeProperties Props => props as CompSporeProperties;
        int nextPlantingTick = Find.TickManager.TicksGame;

        Pawn Parent => parent as Pawn;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextPlantingTick, "nextPlantingTick", Find.TickManager.TicksGame);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            nextPlantingTick = Find.TickManager.TicksGame + Mathf.CeilToInt(Props.plantingIntervalHour * 2500);
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (dinfo.Def == DamageDefOf.Burn || dinfo.Def == DamageDefOf.Bomb)
            {
                dinfo.SetAmount(0);
                absorbed = true;
                return;
            }
            base.PostPreApplyDamage(ref dinfo, out absorbed);
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
                if (Parent.Awake())
                {
                    int currentTick = Find.TickManager.TicksGame;
                    if (Find.TickManager.TicksGame > nextPlantingTick)
                    {
                        nextPlantingTick = currentTick + Mathf.CeilToInt(Props.plantingIntervalHour * 2500);

                        IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(Parent.Position,Props.plantingRadius,true);

                        foreach (IntVec3 cell in cells)
                        {
                            if(cell.GetFertility(parent.Map)>= Props.minimumFertility)
                            {
                                if(XMTUtility.TransformPawnIntoThing(Parent,Props.plantingResult, out Thing result))
                                {
                                    break;
                                }
                                
                            }
                        }
                    }
                }
            }
        }
    }

    public class CompSporeProperties : CompProperties
    {
        public float plantingIntervalHour = 2;
        public float plantingRadius = 3;
        public float minimumFertility = 0.1f;
        public ThingDef plantingResult;
        public CompSporeProperties()
        {
            this.compClass = typeof(CompSpore);
        }
        public CompSporeProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
