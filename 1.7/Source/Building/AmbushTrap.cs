

using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class AmbushTrap : SelfOccupyingBuilding
    {
        CompMatureMorph containedMorph = null;
        int CheckInterval = 120;

        public override bool CanOpen => false;

        protected override bool DestroyWhenEmptyAfterLoad => true;
        protected override bool HasFallbackOccupant => true;

        protected override bool CanOccupy(Pawn pawn)
        {
            return base.CanOccupy(pawn) && !XMTUtility.IsQueen(pawn);
        }

        protected override Pawn GenerateFallbackOccupant()
        {
            return XenoformingUtility.GenerateFeralXenomorph();
        }

        protected override void Notify_OccupantRegistered(Pawn pawn)
        {
            containedMorph = pawn.TryGetComp<CompMatureMorph>();
        }

        protected override void TickSelfOccupyingBuilding(int delta)
        {
            if (this.IsHashIntervalTick(CheckInterval))
            {
                IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(Position, def.specialDisplayRadius, true);

                List<Pawn> targets = new List<Pawn>();
                foreach (IntVec3 cell in cells)
                {
                    
                    foreach (Thing thing in cell.GetThingList(Map))
                    {

                        if(thing is Pawn pawn)
                        {
                            targets.Add(pawn);
                        }
                    }
                }

                foreach (Pawn pawn in targets)
                {
                    if (ContainedThing.Faction != null)
                    {
                        if (!pawn.HostileTo(ContainedThing))
                        {
                            continue;
                        }
                    }

                    if (XMTUtility.IsXenomorph(pawn))
                    {
                        continue;
                    }

                    if (Rand.Chance(SpringChance(pawn)))
                    {
                        if (XMTUtility.IsAcceptableHost(pawn))
                        {
                            EjectContents();
                            containedMorph.TryAmbushAbduct(pawn);
                            Destroy();
                            break;

                        }
                        else
                        {
                            EjectContents();
                            containedMorph.TryAmbushAttack(pawn);
                            Destroy();
                            break;
                        }
                    }
                }
            }
        }

        protected virtual float SpringChance(Pawn p)
        {
            float num = 1f;
            if (p.kindDef.immuneToTraps)
            {
                return 0f;
            }

            num *= (this.GetStatValue(StatDefOf.TrapSpringChance) * p.GetStatValue(StatDefOf.PawnTrapSpringChance));
            Log.Message("spring chance of " + p + " is " + num);
            return Mathf.Clamp01(num);
        }
    }
}
