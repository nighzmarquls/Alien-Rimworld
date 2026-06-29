using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class BattleLogEntry_QueenTrample : LogEntry
    {
        private Pawn queen;
        private Pawn target;
        private bool direct;

        public BattleLogEntry_QueenTrample()
        {
        }

        public BattleLogEntry_QueenTrample(Pawn queen, Pawn target, bool direct)
        {
            this.queen = queen;
            this.target = target;
            this.direct = direct;
        }

        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
        {
            string key = direct ? "XMT_BattleLog_QueenTrampled" : "XMT_BattleLog_QueenGrazed";
            return key.Translate(queen.Named("QUEEN"), target.Named("TARGET")).Resolve();
        }

        public override bool Concerns(Thing t)
        {
            return t == queen || t == target;
        }

        public override IEnumerable<Thing> GetConcerns()
        {
            if (queen != null)
            {
                yield return queen;
            }

            if (target != null)
            {
                yield return target;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref queen, "queen");
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref direct, "direct", defaultValue: false);
        }
    }
}
