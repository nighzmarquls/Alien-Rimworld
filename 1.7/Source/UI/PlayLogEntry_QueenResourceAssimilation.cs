using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class PlayLogEntry_QueenResourceAssimilation : PlayLogEntry_InteractionSinglePawn
    {
        private string text;

        public PlayLogEntry_QueenResourceAssimilation()
        {
        }

        public PlayLogEntry_QueenResourceAssimilation(Pawn queen, string text) : base(InternalDefOf.XMT_QueenResourceAssimilation, queen, null)
        {
            this.text = text;
        }

        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
        {
            return text;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref text, "text");
        }
    }
}
