using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class ChoiceLetter_XenoGameEnded : ChoiceLetter
    {
        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (base.ArchivedOnly)
                {
                    yield return base.Option_Close;
                    yield break;
                }

                if (!Find.GameEnder.CanSpawnNewWanderers())
                {
                    DiaOption diaOption = new DiaOption("GameOverKeepWatching".Translate());
                    diaOption.resolveTree = true;
                    yield return diaOption;
                }
                else
                {
                    DiaOption diaOption2 = new DiaOption("GameOverKeepWatchingForNow".Translate());
                    diaOption2.resolveTree = true;
                    yield return diaOption2;
                    float num = (float)(20000 - (GenTicks.TicksGame - arrivalTick)) / 2500f;
                    DiaOption diaOption3 = new DiaOption((num > 0f) ? "GameOverCreateNewWanderersWait".Translate(Math.Ceiling(num)) : "GameOverCreateNewWanderers".Translate());
                    diaOption3.action = delegate
                    {
                        Find.WindowStack.Add(new Dialog_ChooseNewWanderers());
                    };
                    diaOption3.resolveTree = true;
                    diaOption3.disabled = num > 0f || Find.AnyPlayerHomeMap == null;
                    diaOption3.disabledReason = ((Find.AnyPlayerHomeMap == null) ? "NoColony".Translate() : ((TaggedString)null));
                    yield return diaOption3;
                }

                DiaOption diaOption4 = new DiaOption("GameOverMainMenu".Translate());
                diaOption4.action = delegate
                {
                    GenScene.GoToMainMenu();
                };
                diaOption4.resolveTree = true;
                yield return diaOption4;
            }
        }
    }
}
