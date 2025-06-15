using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class TransformationHorror
    {
        public ThingDef        thingTransformationTarget;
        public PawnKindDef     pawnTransformationTarget;
        public float    probability = 0;
        public float    essenceMinimum = 0;
        public float    essenceMaximum = 1;
    }
    public class MutationHealth
    {
        public HediffDef    horror;
        public bool         randomBodypart = true;
        public BodyPartDef  specificBodyPart = null;
        public float        probability = 0;
        public float        essenceMinimum = 0;
        public float        essenceMaximum = 1;
    }

    public class InfluenceHealth
    {
        public HediffDef   hediff;
        public float    influence;
    }

    public class GooHorror
    {
        public PawnKindDef childKind;
        public float probability;
        public int minimumPotency;
    }

    public class XMT_GooHorrorSet : Def
    {
        public List<GooHorror> horrors;
    }
    public class XMT_InfluenceHealthSet : Def
    {
        public List<InfluenceHealth> influences;
    }

    public class XMT_TransformationSet : Def
    {
        public List<TransformationHorror> transformations;
    }
    public class XMT_MutationsHealthSet : Def
    {
        public List<MutationHealth> mutations;
    }

}
