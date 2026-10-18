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

        public override void TransformedFrom(Pawn pawn, Pawn instigator)
        {
            compJellyWell.SetProgenitor(pawn);
        }

        public override void TransformedFrom(Thing thing, Pawn instigator)
        {
            compJellyWell.SetProgenitor(thing);

            HorrorGenePayload actorGenes = BioUtility.CaptureHorrorGenePayload(instigator);
            CompHiveGeneHolder holder = GetComp<CompHiveGeneHolder>();
            if (actorGenes != null && holder != null)
            {
                holder.ReplaceGenes(actorGenes.Genes, actorGenes.TemplateName);
            }
        }
    }
}
