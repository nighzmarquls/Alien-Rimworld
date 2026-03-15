using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class PodProduct
    {
        public ThingDef     thingDef;
        public PawnKindDef  pawnDef;
        public int        hatchTime;
        public float        probability;

    }
    public class CompHatchingPod : ThingComp
    {
        protected ThingDef      thingDef;
        protected PawnKindDef   pawnDef;
        protected bool          hatchingPawn;
        protected int           tickToHatch;
        protected bool          initialized;
        protected bool          hatched;
        CompHatchingPodProperties Props => props as CompHatchingPodProperties;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref thingDef, "productDef");
            Scribe_Defs.Look(ref pawnDef, "pawnDef");
            Scribe_Values.Look(ref tickToHatch, "tickToHatch", -1);
            Scribe_Values.Look(ref initialized, "initialized", false);
            Scribe_Values.Look(ref hatched, "hatched", false);
            Scribe_Values.Look(ref hatchingPawn, "hatchingPawn", false);
        }

        public override void CompTick()
        {
            base.CompTick();
            if(!parent.Spawned)
            {
                return;
            }

            if (initialized)
            {
               
                if (Find.TickManager.TicksGame > tickToHatch)
                {
                    Hatch();
                }
            }
            else
            {
                Initialize();
            }
        }

        protected virtual void Initialize()
        {
            ThingDef hatchProduct = null;
            float hatchTime = 0f;
            foreach(PodProduct pod in Props.possibleProducts)
            {
                if(Rand.Chance(pod.probability))
                {
                    hatchTime = pod.hatchTime;
                    hatchProduct = pod.thingDef;
                    if (pod.pawnDef != null)
                    {
                        hatchingPawn = true;
                        pawnDef = pod.pawnDef;
                       
                    }
                    break;
                }
            }
            if (hatchProduct != null)
            {
                thingDef = hatchProduct;
                tickToHatch = Find.TickManager.TicksGame + Mathf.RoundToInt(hatchTime);
                initialized = true;
            }
        }
        protected virtual void Hatch()
        {
            if (hatched)
            {
                return;
            }
            hatched = true;
            Pawn target = parent as Pawn;

            if (parent != null)
            {
                if (hatchingPawn)
                {
                    if (XMTUtility.TransformPawnIntoPawn(target, pawnDef, out Pawn result))
                    {
                        return;
                    }
                }
                else
                {
                    if (XMTUtility.TransformPawnIntoThing(target, thingDef, out Thing result))
                    {
                        return;
                    }
                }
            }
        }
    }
    public class CompHatchingPodProperties : CompProperties
    {
        public List<PodProduct> possibleProducts;

        public CompHatchingPodProperties()
        {
            this.compClass = typeof(CompHatchingPod);
        }
        public CompHatchingPodProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
