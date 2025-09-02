using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static UnityEngine.GraphicsBuffer;

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
        ThingDef    thingDef;
        PawnKindDef pawnDef;
        bool        hatchingPawn;
        float       tickToHatch;
        bool        initialized;
        bool        hatched;
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

        protected void Initialize()
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
                tickToHatch = Find.TickManager.TicksGame + (hatchTime);
                initialized = true;
            }
        }
        protected void Hatch()
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

                    }
                }
                else
                {
                    if (XMTUtility.TransformPawnIntoThing(target, thingDef, out Thing result))
                    {
                        
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
