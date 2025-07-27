

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace Xenomorphtype
{
    public class AmbushTrap : HibernationCocoon
    {
        CompMatureMorph containedMorph = null;

        int attempts = 2;

        int CheckInterval = 480;
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
            if(base.TryAcceptThing(thing, allowSpecialEffects))
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
                if (thing is Pawn pawn)
                {
                    if (XMTUtility.IsXenomorph(pawn))
                    {
                        if (XMTUtility.IsQueen(pawn))
                        {
                            continue;
                        }

                        bool flag = pawn.DeSpawnOrDeselect();
                        if (TryAcceptThing(pawn) && flag)
                        {
                            if (pawn.jobs.curJob != null)
                            {
                                pawn.jobs.curJob.Clear();
                            }
                            Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
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
                PawnGenerationRequest request = new PawnGenerationRequest(
                               InternalDefOf.XMT_FeralStarbeastKind, faction: null, PawnGenerationContext.PlayerStarter, -1, true, false, true, false, false, 0, false, true, false, false, false, false, false, false, true, 0, 0, null, 0, null, null, null, null, 0, fixedGender: Gender.Female);

                request.ForceNoIdeo = true;
                request.ForceNoBackstory = true;
                request.ForceNoGear = true;
                request.ForceBaselinerChance = 100;
                request.ForcedXenotype = XenotypeDefOf.Baseliner;

                Pawn gaurdian = PawnGenerator.GeneratePawn(request);
                GenSpawn.Spawn(gaurdian, Position, Map);
                bool flag = gaurdian.DeSpawnOrDeselect();
                if (TryAcceptThing(gaurdian) && flag)
                {
                    if (gaurdian.jobs.curJob != null)
                    {
                        gaurdian.jobs.curJob.Clear();
                    }
                    Find.Selector.Select(gaurdian, playSound: false, forceDesignatorDeselect: false);
                    return;
                }
            }
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if(ContainedThing == null)
            {
                Initialize();
                return;
            }

            if (!this.IsHashIntervalTick(CheckInterval))
            {
                return;
            }
            IEnumerable<Pawn> targets = GenRadial.RadialDistinctThingsAround(Position, Map, def.specialDisplayRadius, true).OfType<Pawn>();
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

        protected virtual float SpringChance(Pawn p)
        {
            float num = 1f;
            if (p.kindDef.immuneToTraps)
            {
                return 0f;
            }

            num *= (this.GetStatValue(StatDefOf.TrapSpringChance) * p.GetStatValue(StatDefOf.PawnTrapSpringChance));
            return Mathf.Clamp01(num);
        }
    }
}
