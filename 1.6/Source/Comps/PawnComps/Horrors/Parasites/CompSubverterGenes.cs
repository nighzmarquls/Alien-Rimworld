using Verse;

namespace Xenomorphtype
{
    public class CompSubverterGenes : CompHiveGeneHolder
    {
        public Pawn mother;
        public Pawn father;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref mother, "mother", saveDestroyedThings: false);
            Scribe_References.Look(ref father, "father", saveDestroyedThings: false);
        }
    }
}
