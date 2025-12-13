using RimWorld;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using PipeSystem;

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

        public CompResource JellyResource;
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
