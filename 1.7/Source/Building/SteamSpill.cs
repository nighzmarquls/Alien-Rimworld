using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PipeSystem;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class SteamSpillProperties : DefModExtension
    {
        public float targetTemperature = 100f;
        public float heatPerUnit = 100f;
        public float damage = 3f;
        public DamageDef damageDef;
        public EffecterDef spillEffect;
    }

    public class SteamSpill : Filth
    {
        private sealed class ScaldStamp
        {
            public int tick = int.MinValue;
        }

        private static readonly ConditionalWeakTable<Pawn, ScaldStamp> scalds = new ConditionalWeakTable<Pawn, ScaldStamp>();
        private sealed class CloudStamp
        {
            public int tick = -1;
            public readonly HashSet<IntVec3> cells = new HashSet<IntVec3>();
        }

        private static readonly ConditionalWeakTable<Map, CloudStamp> clouds = new ConditionalWeakTable<Map, CloudStamp>();
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad)
            {
                Destroy();
                return;
            }

            float amount = Math.Max(1, stackCount);
            SteamSpillProperties settings = def.GetModExtension<SteamSpillProperties>();
            if (settings == null)
            {
                Destroy();
                return;
            }
            if (settings.spillEffect != null)
            {
                CloudStamp cloud = clouds.GetValue(map, _ => new CloudStamp());
                int now = Find.TickManager.TicksGame;
                if (cloud.tick != now)
                {
                    cloud.tick = now;
                    cloud.cells.Clear();
                }
                if (cloud.cells.Add(Position))
                {
                    Effecter effect = settings.spillEffect.Spawn();
                    TargetInfo location = new TargetInfo(Position, map);
                    effect.Trigger(location, location);
                    effect.Cleanup();
                }
            }
            Room room = Position.GetRoom(map);
            if (room != null && !room.UsesOutdoorTemperature && room.Temperature < settings.targetTemperature)
            {
                room.Temperature += GenTemperature.ControlTemperatureTempChange(Position, map,
                    amount * settings.heatPerUnit, settings.targetTemperature);
            }
            var targets = new HashSet<Pawn>();
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8WayAndInside(this))
            {
                if (!cell.InBounds(map)) continue;
                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (thing is Pawn pawn) targets.Add(pawn);
                }
            }
            foreach (Pawn pawn in targets)
            {
                // A destroyed tank emits many filth instances in one tick. They form one scalding event.
                ScaldStamp stamp = scalds.GetValue(pawn, _ => new ScaldStamp());
                int tick = Find.TickManager.TicksGame;
                if (stamp.tick == tick)
                    continue;
                stamp.tick = tick;
                if (pawn.Spawned && pawn.Map == map && settings.damageDef != null)
                {
                    pawn.TakeDamage(new DamageInfo(settings.damageDef, settings.damage, 0f, -1f, this));
                }
            }

            Destroy();
        }
    }
}
