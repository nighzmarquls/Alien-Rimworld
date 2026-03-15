
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    public class CompCloningEgg : CompHatchingPod
    {
        int initializationAttemptsLeft = 1000;
        protected Pawn Parent => parent as Pawn;

        GeneSet pawnGenes = null;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (Parent != null)
            {
                Hediff helplessness = Parent.health.GetOrAddHediff(XenoGeneDefOf.XMT_Helpless);
            }
            if (initialized)
            {
                int timeToHatch = tickToHatch - Find.TickManager.TicksGame;
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message(parent + " has spawned with CompCloningEgg with pawndef " + pawnDef + " will hatch in " + timeToHatch);
                }
                if(timeToHatch < 0)
                {
                    hatched = false;
                    hatchingPawn = true;
                    Hatch();
                }
            }
        }
        public void SetupKindtoHatch(PawnKindDef target, GeneSet genes, int ticksToHatch = 2000)
        {
            if (XMTSettings.LogBiohorror)
            {
                Log.Message(parent + " has had pawnkind setup assigned with " + target + " hatching in " + ticksToHatch + " ticks");
            }
            hatchingPawn = true;
            initialized = true;
            pawnDef = target;
            pawnGenes = genes;
            tickToHatch = Find.TickManager.TicksGame + ticksToHatch;
        }

        protected override void Initialize()
        {
            if (initialized)
                return;

            initializationAttemptsLeft--;
            if(initializationAttemptsLeft <= 0)
            {
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message(parent + " failed to initialize before attempts ran out ");
                }
                parent.Destroy();
            }
            return;
        }

        protected override void Hatch()
        {

            if (hatched)
            {
                return;
            }
            if (XMTSettings.LogBiohorror)
            {
                Log.Message(parent + " attempting to hatch ");
            }
            hatched = true;
            Pawn target = parent as Pawn;

            if (parent != null)
            {
                if (hatchingPawn)
                {
                    if (XMTUtility.TransformPawnIntoPawn(target, pawnDef, out Pawn result))
                    {
                        if(pawnGenes != null)
                        {
                            BioUtility.InsertGenesetToPawn(pawnGenes, ref result);
                        }    
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
            FilthMaker.TryMakeFilth(parent.PositionHeld, parent.MapHeld, InternalDefOf.Starbeast_Filth_Resin, count: 8);
        }
    }
}
