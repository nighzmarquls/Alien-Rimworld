using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class XenoformingUtility
    {
        private static GameComponent_Xenomorph gameComponent => Current.Game.GetComponent<GameComponent_Xenomorph>();

        public static bool XenoformingMeets(float minXenoforming)
        {
            return gameComponent.Xenoforming >= minXenoforming;
        }
        public static void HandleXenoformingImpact(Pawn pawn)
        {
            if (XMTSettings.LogWorld)
            {
                Log.Message(pawn + " is being evaluated for xenoforming impact");
            }
            if (pawn != null)
            {
                if(CaravanUtility.IsCaravanMember(pawn))
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(pawn + " is in a caravan. Skipping.");
                    }
                    return;
                }
                if (XMTUtility.IsXenomorph(pawn))
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(pawn + " is a xenomorph");
                    }
                    gameComponent.ReleaseXenomorphOnWorld(pawn);
                }
                else
                {
                    if(XMTUtility.HasEmbryo(pawn))
                    {
                        if (XMTSettings.LogWorld)
                        {
                            Log.Message(pawn + " has an embryo");
                        }
                        gameComponent.ReleaseEmbryoOnWorld(pawn);
                    }
                    float essence = BioUtility.GetXenomorphInfluence(pawn);
                    if (essence > 0)
                    {
                        if (XMTSettings.LogWorld)
                        {
                            Log.Message(pawn + " has mutations");
                        }
                        gameComponent.ReleaseMutagenOnWorld(essence);
                    }
                }

                ThingOwner carriedThings = pawn.inventory.GetDirectlyHeldThings();

                if (XMTSettings.LogWorld)
                {
                    Log.Message("checking inventory for " + pawn);
                }
                if (carriedThings != null)
                {
                    foreach (Thing carriedThing in carriedThings)
                    {
                        HandleXenoformingImpact(carriedThing);
                    }
                }

            }
        }

        public static void HandleXenoformingImpact(HibernationCocoon hibernationCocoon)
        {
            if (hibernationCocoon != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message(hibernationCocoon + " is a hibernation cocoon");
                }
                HandleXenoformingImpact(hibernationCocoon.ContainedThing as Pawn);
            }


        }
        public static void HandleXenoformingImpact(Ovamorph ovamorph)
        {

            if (ovamorph != null)
            {
                if (ovamorph.CanFire)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(ovamorph + " is viable ovamorph");
                    }
                    gameComponent.ReleaseOvamorphOnWorld(ovamorph);
                    return;
                }
            }
        }
        public static void HandleXenoformingImpact(Thing thing)
        {
            if(thing == null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("thing is null");
                }
                return;
            }

            if (XMTSettings.LogWorld)
            {
                Log.Message(thing + " is being evaluated for xenoforming impact");
            }
            MinifiedThing minifiedThing = thing as MinifiedThing;

            if (minifiedThing != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message(thing + " is a minified thing");
                }
                HandleXenoformingImpact(minifiedThing.InnerThing);
                return;
            }

            Ovamorph ovamorph = thing as Ovamorph;

            if (ovamorph != null)
            {
                if (ovamorph.CanFire)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(thing + " is viable ovamorph");
                    }
                    gameComponent.ReleaseOvamorphOnWorld(ovamorph);
                    return;
                }
            }

            HibernationCocoon hibernationCocoon = thing as HibernationCocoon;

            if(hibernationCocoon != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message(thing + " is a hibernation cocoon");
                }
                HandleXenoformingImpact(hibernationCocoon.ContainedThing as Pawn);
            }
            
            XMTGenePack genePack = thing as XMTGenePack;

            if (genePack != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message(thing + " is a genepack");
                }
                gameComponent.ReleaseMutagenOnWorld(genePack.Potency*genePack.stackCount);
                return;
            }

            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                HandleXenoformingImpact(pawn);
            }
        }

        internal static void HandleMatureMorphDeath(Pawn deadMorph)
        {
            gameComponent.HandleMatureMorphDeath(deadMorph);
        }

        internal static float ChanceByXenoforming(float chance)
        {
            return chance + (gameComponent.Xenoforming / 100);
        }
    }
}
