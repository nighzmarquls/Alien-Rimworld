using PipeSystem;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CocoonBase : Building_Bed
    {
        public override Color DrawColor
        {
            get
            {
                return Color.white;
            }
        }
        public Pawn CurOccupant
        {
            get
            {
                List<Thing> list = Map.thingGrid.ThingsListAt(this.Position);
                for (int i = 0; i < list.Count; i++)
                {
                    Pawn pawn = list[i] as Pawn;
                    if (pawn != null && pawn.pather.MovingNow is false)
                    {
                        return pawn;
                    }
                }
                return null;
            }
        }
        public Pawn LastOccupant;

        private CompResource _jellyResource;
        public CompResource JellyResource
        {
            get
            {
                if (_jellyResource == null)
                {
                    _jellyResource = GetComp<CompResource>();
                }

                return _jellyResource;
            }
        }

        int feedingTick = -1;

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
    
            Pawn occupant = CurOccupant;
            if (occupant != null)
            {
                if (LastOccupant == null)
                {
                    LastOccupant = occupant;
                    XMTHiveUtility.AddCocooned(occupant, occupant.MapHeld);
                }
                else
                {
                    if(LastOccupant.Dead || occupant != LastOccupant)
                    {
                        Kill();
                        return;
                    }
                }

                occupant.jobs.posture = PawnPosture.LayingInBedFaceUp;
                if (occupant.IsPrisoner && !ForPrisoners)
                {
                    ForOwnerType = BedOwnerType.Prisoner;
                    occupant.jobs.Notify_TuckedIntoBed(this);
                }

                if (occupant.IsSlave && !ForSlaves)
                {
                    ForOwnerType = BedOwnerType.Slave;
                    occupant.jobs.Notify_TuckedIntoBed(this);
                }

                int tick = Find.TickManager.TicksGame;
                if (tick > feedingTick)
                {
                    feedingTick = tick + 2500;
                    if (occupant.needs.food != null)
                    {
                        float nutritionToFull = occupant.needs.food.NutritionWanted;
                        float JellyWanted = nutritionToFull * InternalDefOf.Starbeast_Jelly.statBases.GetStatValueFromList(StatDefOf.Nutrition, 1);
                        
                        if(JellyResource == null)
                        {
                            Log.Error("Cocoon has Null Jelly Resource");
                            return;
                        }
                        
                        float stored = JellyResource.PipeNet.CurrentStored();
                        if (JellyWanted > 0 && stored > JellyWanted)
                        {
                            JellyResource.PipeNet.DrawAmongStorage(JellyWanted, JellyResource.PipeNet.storages);
                            occupant.needs.food.CurLevel += nutritionToFull;
                            ThingDef jellyDef = InternalDefOf.Starbeast_Jelly;
                            if (jellyDef.ingestible.outcomeDoers != null)
                            {
                                occupant.mindState.lastIngestTick = Find.TickManager.TicksGame;
                                Thing jellyThing = ThingMaker.MakeThing(jellyDef);
                                occupant.needs.drugsDesire?.Notify_IngestedDrug(jellyThing);

                                List<Hediff> hediffs = occupant.health.hediffSet.hediffs;
                                for (int k = 0; k < hediffs.Count; k++)
                                {
                                    hediffs[k].Notify_IngestedThing(jellyThing, Mathf.CeilToInt(JellyWanted));
                                }

                                for (int l = 0; l < jellyDef.ingestible.outcomeDoers.Count; l++)
                                {
                                    jellyDef.ingestible.outcomeDoers[l].DoIngestionOutcome(occupant, jellyThing, Mathf.CeilToInt(JellyWanted));
                                }
                                
                            }

                        }
                    }
                }
            }
            else
            {
                if (LastOccupant != null)
                {
                    XMTHiveUtility.RemoveCocooned(LastOccupant,LastOccupant.MapHeld);
                    LastOccupant = null;
                }

                CompAssignableToPawn assignable = GetComp<CompAssignableToPawn>();

                if (assignable != null)
                {
                    List<Pawn> ownership = assignable.AssignedPawnsForReading.ListFullCopy();
                    foreach (Pawn pawn in ownership)
                    {
                        pawn.ownership.UnclaimBed();
                    }
                }
                Kill();
            }    
        }
    }
}
