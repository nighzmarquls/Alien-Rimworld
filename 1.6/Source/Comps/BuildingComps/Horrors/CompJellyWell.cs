using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class CompJellyWell : ThingComp
    {
        Pawn progenitorPawn = null;
        float breathingSize = 0;
        public int RealMaxHP => parent.MaxHitPoints;
        public Vector2 DisplaySize => Vector2.one * displaySize;
        float displaySize => 2 + breathingSize;

        bool NoJellyPresent = false;
        public bool processedJelly => !NoJellyPresent;

        int tickCountUp = 0;

        PipeSystem.CompResource _network;

        PipeSystem.CompResource network
        {
            get
            {
                if(_network== null)
                {
                    _network = parent.GetComp<PipeSystem.CompResource>();
                }

                return _network;
            }
        }

        CompJellywellProperties Props => props as CompJellywellProperties;
        CompJellyMaker _compJellyMaker;
        CompJellyMaker compJellyMaker
        {
            get
            {
                if(_compJellyMaker == null)
                {
                    _compJellyMaker = parent.GetComp<CompJellyMaker>();
                    if (_compJellyMaker == null)
                    {
                        Log.Error("No CompJellyMaker on " + parent);
                    }
                }
                return _compJellyMaker;
            }
        }
        float tickBreath => (Props.maxSizeChange) / (Props.breathticks / 2);
        bool inhaling;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref progenitorPawn, "progenitorPawn");

            Scribe_Values.Look(ref tickCountUp, "tickCount", 0);
        }

        public void SetProgenitor(Pawn pawn)
        {
            parent.HitPoints = parent.MaxHitPoints;
        }

        public void SetProgenitor(Thing thing)
        {
            parent.HitPoints = parent.MaxHitPoints;

            if(thing is MeatballLarder meatball)
            {
                CompMeatBall progenitorBall =  meatball.GetComp<CompMeatBall>();

                compJellyMaker.MakeJellyByDef(meatball.HitPoints, progenitorBall.GetMeat(), meatball.Position,meatball.Map, compJellyMaker.Efficiency);
                meatball.HitPoints = 0;
            }
        }


        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
            {
                return;
            }

            tickCountUp++;

            if (inhaling)
            {
                breathingSize += tickBreath;

                if (breathingSize > Props.maxSizeChange)
                {
                    inhaling = false;
                }
            }
            else
            {
                breathingSize -= tickBreath;

                if (breathingSize < -Props.maxSizeChange)
                {
                    inhaling = true;
                }

            }

            if (parent.HitPoints > parent.MaxHitPoints)
            {
                parent.HitPoints = parent.MaxHitPoints;
            }

            if (tickCountUp >= 500)
            {
                tickCountUp = 0;

                NoJellyPresent = true;
              
                IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(parent.Position, parent.def.specialDisplayRadius, true);
                if (cells.Any())
                {
                    ConvertJellyCell(cells);
                }

            }

        }

        protected void ConvertJellyCell(IEnumerable<IntVec3> Cells)
        {

            IntVec3 bestCell = Cells.First();

            if(compJellyMaker == null)
            {
                return;
            }

            float jellyStored = network.PipeNet.Stored;
            bool hasCapacityInNetwork = network.PipeNet.AvailableCapacity > 0;

            if (jellyStored > 10)
            {
                NoJellyPresent = false;
                if (parent.HitPoints < parent.MaxHitPoints)
                {
                    parent.HitPoints += 50;
                    network.PipeNet.DrawAmongStorage(10, network.PipeNet.storages);
                }
            }

            foreach (IntVec3 cell in Cells)
            {
               List<Thing> thingList = cell.GetThingList(parent.Map).ListFullCopy();
               bool madeJelly = false;
               foreach (Thing thing in thingList)
               {
                    if (compJellyMaker.CanMakeIntoJelly(thing))
                    {
                        madeJelly = true;
                        NoJellyPresent = false;
                        int total = compJellyMaker.ConvertToJelly(thing,compJellyMaker.Efficiency);
                        if (parent.HitPoints < parent.MaxHitPoints)
                        {
                            parent.HitPoints += total;
                        }
                        break;
                    }
                    else if(compJellyMaker.GetJellyProduct() == thing.def)
                    {
                        NoJellyPresent = false;
                        if (parent.HitPoints < parent.MaxHitPoints)
                        {
                            parent.HitPoints += (thing.stackCount*5);
                            thing.Destroy();
                            break;
                        }
                    }
                }

                if (madeJelly)
                {
                    break;
                }

            }


        }
    }


    public class CompJellywellProperties : CompProperties
    {
        public int breathticks = 120;
        public float maxSizeChange = 0.005f;
        public CompJellywellProperties()
        {
            this.compClass = typeof(CompJellyWell);
        }

        public CompJellywellProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
