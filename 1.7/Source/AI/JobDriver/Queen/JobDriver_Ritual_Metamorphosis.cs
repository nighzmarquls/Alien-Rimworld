using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    internal class JobDriver_Ritual_Metamorphosis : JobDriver
    {
        private int ticksTillFacingUpdate;

        private IntVec3 facingCellCached = IntVec3.Invalid;

        public static readonly Texture2D moteIcon = ContentFinder<Texture2D>.Get("Things/Mote/SpeechSymbols/Speech");

        public Vector3 DynamicBodyOffset = Vector3.zero;
        public override Vector3 ForcedBodyOffset => DynamicBodyOffset;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            
            Toil toil = ToilMaker.MakeToil("MakeNewToils");

            toil.tickAction = delegate
            {
                pawn.GainComfortFromCellIfPossible(1);
                //pawn.skills.Learn(SkillDefOf.Social, 0.3f);

                pawn.Rotation = Rot4.South;
            };
            if (ModsConfig.IdeologyActive)
            {
                toil.PlaySustainerOrSound(() => (pawn.gender != Gender.Female) ? job.speechSoundMale : job.speechSoundFemale, pawn.story.VoicePitchFactor);
            }

            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.handlingFacing = true;
            yield return toil;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksTillFacingUpdate, "ticksTillFacingUpdate", 0);
            Scribe_Values.Look(ref facingCellCached, "facingCellCached");
        }
    }
}
