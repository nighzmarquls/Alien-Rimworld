using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class Ability_PsychicDefense : Ability
    {
        private bool active = true;
        private int ambientProtectedUntilTick;
        private int nextAmbientPaymentTick;

        public bool Active => active;

        public Ability_PsychicDefense()
        {
        }

        public Ability_PsychicDefense(Pawn pawn)
            : base(pawn)
        {
        }

        public Ability_PsychicDefense(Pawn pawn, AbilityDef def)
            : base(pawn, def)
        {
        }

        public override IEnumerable<Command> GetGizmos()
        {
            yield return new Command_Toggle
            {
                defaultLabel = "XMT_PsychicDefenseToggle".Translate(),
                defaultDesc = "XMT_PsychicDefenseToggleDesc".Translate(),
                icon = def.uiIcon,
                isActive = () => active,
                toggleAction = delegate
                {
                    active = !active;
                    ResetAmbientProtection();
                }
            };
        }

        public override string Tooltip
        {
            get
            {
                PsychicDefenseSettingsDef settings = PsychicDefenseUtility.Settings;
                string status = active
                    ? "XMT_PsychicDefenseStatusActive".Translate()
                    : "XMT_PsychicDefenseStatusInactive".Translate();

                return "XMT_PsychicDefenseTooltip".Translate(
                    status,
                    settings.targetedHeatCost.ToString("0.##"),
                    settings.ambientHeatCost.ToString("0.##"),
                    settings.ambientIntervalTicks.ToStringTicksToPeriod());
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref active, "psychicDefenseActive", true);
            Scribe_Values.Look(ref ambientProtectedUntilTick, "psychicDefenseAmbientProtectedUntilTick", 0);
            Scribe_Values.Look(ref nextAmbientPaymentTick, "psychicDefenseNextAmbientPaymentTick", 0);
        }

        internal bool TryMaintainAmbientProtection()
        {
            if (!active || !PsychicDefenseUtility.QueenCanProtect(pawn, requireActiveToggle: false))
            {
                return false;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick < ambientProtectedUntilTick)
            {
                return true;
            }

            if (currentTick < nextAmbientPaymentTick)
            {
                return false;
            }

            PsychicDefenseSettingsDef settings = PsychicDefenseUtility.Settings;
            int interval = Mathf.Max(1, settings.ambientIntervalTicks);
            nextAmbientPaymentTick = currentTick + interval;

            if (!PsychicDefenseUtility.TryPayHeat(pawn, settings.ambientHeatCost))
            {
                ambientProtectedUntilTick = 0;
                return false;
            }

            ambientProtectedUntilTick = nextAmbientPaymentTick;
            return true;
        }

        private void ResetAmbientProtection()
        {
            ambientProtectedUntilTick = 0;
            nextAmbientPaymentTick = 0;
        }
    }
}
