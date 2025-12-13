

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class AmbushTrap : HibernationCocoon
    {
        CompMatureMorph containedMorph = null;

        int attempts = 2;

        bool empty = true;
        int CheckInterval = 120;

        public override bool CanOpen => false;
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if(respawningAfterLoad)
            {
                if(ContainedThing == null)
                {
                    Destroy();
                }
                else
                {
                    containedMorph = ContainedThing.TryGetComp<CompMatureMorph>();
                }
            }
        }
        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (ContainedThing != null)
            {
                return false;
            }

            if (base.TryAcceptThing(thing, allowSpecialEffects))
            {
                containedMorph = thing.TryGetComp<CompMatureMorph>();
                return true;
            }
            return false;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if(Faction == null || !Faction.IsPlayer)
            {
                yield break;
            }

            Command_Action command_Action = new Command_Action();
            command_Action.action = Open;
            command_Action.defaultLabel = "CommandPodEject".Translate();
            command_Action.defaultDesc = "CommandPodEjectDesc".Translate();

            command_Action.hotKey = KeyBindingDefOf.Misc8;
            command_Action.icon = ContentFinder<Texture2D>.Get("UI/Abilities/Starbeast_Leap");
            yield return command_Action;
        }

        public void Initialize()
        {
            IEnumerable<Thing> possibleMaker = GenRadial.RadialDistinctThingsAround(Position, Map, 1.5f, true);
            foreach (Thing thing in possibleMaker)
            {
                if(!empty)
                {
                    return;
                }
                if (thing is Pawn pawn)
                {

                    if (XMTUtility.IsXenomorph(pawn))
                    {
                        if (XMTUtility.IsQueen(pawn))
                        {
                            continue;
                        }
                        empty = false;
                        bool flag = pawn.DeSpawnOrDeselect();
                        if (TryAcceptThing(pawn, allowSpecialEffects: false) && flag)
                        {

                            containedMorph = ContainedThing.TryGetComp<CompMatureMorph>();
                            if (pawn.jobs.curJob != null)
                            {
                                pawn.jobs.curJob.Clear();
                            }
                            Find.Selector.Select(this, playSound: false, forceDesignatorDeselect: false);
                            return;
                        }
                    }

                }
            }

            if(attempts > 0)
            {
                attempts--;
            }
            else
            {
                Pawn gaurdian = XenoformingUtility.GenerateFeralXenomorph();
                if (gaurdian.Faction != Faction)
                {
                    gaurdian.SetFaction(Faction);
                }
                GenSpawn.Spawn(gaurdian, Position, Map);
                bool flag = gaurdian.DeSpawnOrDeselect();
                if (TryAcceptThing(gaurdian, allowSpecialEffects: false) && flag)
                {
                    if (gaurdian.jobs.curJob != null)
                    {
                        gaurdian.jobs.curJob.Clear();
                    }
                    return;
                }
            }
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if(ContainedThing == null && empty)
            {
                Initialize();
                return;
            }

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
                        if (XMTUtility.IsHost(pawn))
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
