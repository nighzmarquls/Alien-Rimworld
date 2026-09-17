using PipeSystem;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class HiveMapComponent : MapComponent
    {
        bool Uninitialized = true;
        private int nextZoneMaintenanceTick;
        private List<Thing> generatedCryptimorphStructures = new List<Thing>();
        private List<Thing> cryptimorphSubvertedTurrets = new List<Thing>();
        private bool motherWarningSent;

        public HiveMapComponent(Map map) : base(map)
        {
            
        }

        public override void MapRemoved()
        {
            base.MapRemoved();

            XMTHiveUtility.DeregisterNestMap(map);
        }
        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if(Uninitialized)
            {
                Initialize();
            }

            if (Find.TickManager.TicksGame >= nextZoneMaintenanceTick)
            {
                nextZoneMaintenanceTick = Find.TickManager.TicksGame + 250;
                XMTZoneUtility.ReconcileDoorwayCells(map);
            }
        }


        protected void Initialize()
        {
            foreach (Room room in map.regionGrid.AllRooms)
            {
                XMTHiveUtility.NotifyHiveRoomCompleted(room);
            }
            Uninitialized = false;
        }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref generatedCryptimorphStructures, "generatedCryptimorphStructures", LookMode.Reference);
            Scribe_Collections.Look(ref cryptimorphSubvertedTurrets, "cryptimorphSubvertedTurrets", LookMode.Reference);
            Scribe_Values.Look(ref motherWarningSent, "motherWarningSent", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                generatedCryptimorphStructures ??= new List<Thing>();
                generatedCryptimorphStructures.RemoveAll(thing => thing == null || thing.Destroyed);
                cryptimorphSubvertedTurrets ??= new List<Thing>();
                cryptimorphSubvertedTurrets.RemoveAll(thing => thing == null || thing.Destroyed);
            }
        }

        internal void CaptureGeneratedCryptimorphStructures()
        {
            generatedCryptimorphStructures ??= new List<Thing>();
            foreach (Thing thing in map.spawnedThings)
            {
                if (IsNestGeneratedCryptimorphStructure(thing))
                {
                    generatedCryptimorphStructures.AddDistinct(thing);
                }
            }
        }

        internal IEnumerable<Thing> GeneratedCryptimorphStructures
        {
            get
            {
                generatedCryptimorphStructures ??= new List<Thing>();
                generatedCryptimorphStructures.RemoveAll(thing => thing == null || thing.Destroyed);
                return generatedCryptimorphStructures;
            }
        }

        internal bool TryMarkMotherWarningSent()
        {
            if (motherWarningSent)
            {
                return false;
            }
            motherWarningSent = true;
            return true;
        }

        internal bool IsCryptimorphSubvertedTurret(Thing turret)
        {
            cryptimorphSubvertedTurrets ??= new List<Thing>();
            cryptimorphSubvertedTurrets.RemoveAll(thing => thing == null || thing.Destroyed);
            return turret != null && cryptimorphSubvertedTurrets.Contains(turret);
        }

        internal void MarkCryptimorphSubvertedTurret(Thing turret)
        {
            if (turret == null || turret.Destroyed || turret.MapHeld != map)
            {
                return;
            }

            cryptimorphSubvertedTurrets ??= new List<Thing>();
            cryptimorphSubvertedTurrets.AddDistinct(turret);
        }

        private static bool IsNestGeneratedCryptimorphStructure(Thing thing)
        {
            ThingDef def = thing?.def;
            return def == XenoBuildingDefOf.Hivemass
                || def == XenoBuildingDefOf.HiveWebbing
                || def == XenoBuildingDefOf.XMT_Ovomorph
                || def == XenoBuildingDefOf.XMT_CocoonBase
                || def == XenoBuildingDefOf.XMT_CocoonBaseAnimal
                || def == XenoBuildingDefOf.XMT_AmbushSpot
                || def == XenoBuildingDefOf.XMT_Ovothrone;
        }
    }
}
