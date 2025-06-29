using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class JellyWell : XMTBase_Building
    {
        CompJellyWell _compJellyWell = null;
        CompJellyWell compJellyWell
        {
            get
            {
                if (_compJellyWell == null)
                {
                    _compJellyWell = GetComp<CompJellyWell>();
                }
                return _compJellyWell;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic.drawSize = compJellyWell.DisplaySize;
            base.DrawAt(drawLoc, flip);
            Graphic.drawSize = def.graphic.drawSize;
            Graphic.color = def.graphic.color;
        }

        private Graphic _fullGraphic;
        public override Graphic Graphic
        {
            get
            {
                if (compJellyWell.processedJelly)
                {
                    if (_fullGraphic is null)
                    {
                        var data = new GraphicData();
                        data.CopyFrom(def.graphicData);
                        data.texPath += "_full";
                        _fullGraphic = data.GraphicColoredFor(this);
                    }
                    return _fullGraphic;
                   
                }
                else
                {
                    return base.Graphic;
                }
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (compJellyWell != null && Spawned)
            {
                int stackTotal = HitPoints;

            }

            base.Destroy(mode);
        }

        public override void TransformedFrom(Pawn pawn, Pawn instigator)
        {
            compJellyWell.SetProgenitor(pawn);
        }

        public override void TransformedFrom(Thing thing, Pawn instigator)
        {
            compJellyWell.SetProgenitor(thing);
        }
    }
}
