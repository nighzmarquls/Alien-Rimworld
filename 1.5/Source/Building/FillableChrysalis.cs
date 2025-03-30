using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class FillableChrysalis : Building
    {

        float FillStages = 6;
        bool lastOpen = true;
        bool Open = true;
        AltitudeLayer OpenAltitude => def.altitudeLayer;

        AltitudeLayer ClosedAltitude => AltitudeLayer.MoteOverheadLow;

        float CurrentAltitude => Open? Altitudes.AltitudeFor(OpenAltitude) : Altitudes.AltitudeFor(ClosedAltitude);
        public bool Filled => Refuelable.IsFull;

        Graphic OpenEmptyGraphic = null;
        Graphic ClosedGraphic = null;
        List<Graphic> FillGraphics = null;
        private CompRefuelable refuelable = null;
        CompRefuelable Refuelable
        {
            get
            {
               
                if (refuelable == null)
                {
                    refuelable = GetComp<CompRefuelable>();
                }
                return refuelable; 
            }
        }

        Graphic cacheGraphic = null;
        float lastFuel = 0;
        public override Graphic Graphic
        {
            get
            {

                if(lastFuel == Refuelable.Fuel && lastOpen == Open && cacheGraphic != null)
                {
                    return cacheGraphic;
                }
                lastFuel = Refuelable.Fuel;
                lastOpen = Open;
                float stageAmount = Refuelable.TargetFuelLevel / FillStages;

                if(ClosedGraphic == null)
                {
                    var data = new GraphicData();
                    data.CopyFrom(def.graphicData);
                    data.texPath += "_Closed";
                    ClosedGraphic = data.GraphicColoredFor(this);
                }

                if (OpenEmptyGraphic == null)
                {
                    var data = new GraphicData();
                    data.CopyFrom(def.graphicData);
                    data.texPath += "_Open";
                    OpenEmptyGraphic = data.GraphicColoredFor(this);
                }

                if (FillGraphics == null)
                {
                    FillGraphics = new List<Graphic>();
                    for(int i = 1; i <= Mathf.CeilToInt(FillStages); i++)
                    {
                        var data = new GraphicData();
                        data.CopyFrom(def.graphicData);
                        data.texPath += "_Open_" + i;

                        FillGraphics.Add(data.GraphicColoredFor(this));
                    }
                }

                if (Open)
                {
                    if (Refuelable.Fuel < stageAmount)
                    {
                        cacheGraphic = OpenEmptyGraphic;

                    }
                    else
                    {
                        int index = Mathf.FloorToInt(Refuelable.Fuel / stageAmount) - 1;
                        Log.Message(FillGraphics[index].path);
                        cacheGraphic = FillGraphics[index];
                    }
                }
                else
                {
                    cacheGraphic = ClosedGraphic;
                }
                return cacheGraphic;
            }
        }

        public void CloseChrysalis()
        {
            Open = false;
        }

        public void OpenChrysalis()
        {
            Open = true;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Vector3 modifiedLoc = drawLoc;
            modifiedLoc.y = CurrentAltitude;
            base.DrawAt(modifiedLoc, flip);
        }
    }
}
