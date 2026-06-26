using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class ScenPart_ForcedStartingHediffCount : ScenPart
    {
        private int count = 1;
        private HediffDef hediff;
        private BodyPartDef bodyPart;
        private FloatRange severityRange = new FloatRange(-1f, -1f);
        private string countBuffer;
        private bool applied;

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            Rect scenPartRect = listing.GetScenPartRect(this, ScenPart.RowHeight * 5f + 62f);
            float rowY = scenPartRect.y + ScenPart.RowHeight;
            float labelWidth = scenPartRect.width * 0.45f;
            float valueWidth = scenPartRect.width - labelWidth;

            Rect countLabelRect = new Rect(scenPartRect.x, rowY, labelWidth, ScenPart.RowHeight);
            Rect countEntryRect = new Rect(countLabelRect.xMax, rowY, 90f, ScenPart.RowHeight);
            Widgets.Label(countLabelRect, "XMT_ForcedStartingHediffCountCount".Translate());
            Widgets.TextFieldNumeric(countEntryRect, ref count, ref countBuffer, 0, 9999);
            count = Mathf.Max(0, count);
            rowY += ScenPart.RowHeight;

            Rect hediffLabelRect = new Rect(scenPartRect.x, rowY, labelWidth, ScenPart.RowHeight);
            Rect hediffButtonRect = new Rect(hediffLabelRect.xMax, rowY, valueWidth, ScenPart.RowHeight);
            Widgets.Label(hediffLabelRect, "XMT_ForcedStartingHediffCountHediff".Translate());
            if (Widgets.ButtonText(hediffButtonRect, hediff?.LabelCap ?? "None".Translate()))
            {
                Find.WindowStack.Add(new FloatMenu(PossibleHediffs()
                    .Select(hd => new FloatMenuOption(hd.LabelCap, delegate
                    {
                        hediff = hd;
                        if (severityRange.min >= 0f)
                        {
                            severityRange = new FloatRange(hd.initialSeverity, hd.initialSeverity);
                        }
                    }))
                    .ToList()));
            }
            rowY += ScenPart.RowHeight;

            Rect bodyPartLabelRect = new Rect(scenPartRect.x, rowY, labelWidth, ScenPart.RowHeight);
            Rect bodyPartButtonRect = new Rect(bodyPartLabelRect.xMax, rowY, valueWidth, ScenPart.RowHeight);
            Widgets.Label(bodyPartLabelRect, "XMT_ForcedStartingHediffCountBodyPart".Translate());
            if (Widgets.ButtonText(bodyPartButtonRect, bodyPart?.LabelCap ?? "None".Translate()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("None".Translate(), delegate
                    {
                        bodyPart = null;
                    })
                };
                options.AddRange(DefDatabase<BodyPartDef>.AllDefs
                    .OrderBy(part => part.label)
                    .Select(part => new FloatMenuOption(part.LabelCap, delegate
                    {
                        bodyPart = part;
                    })));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            rowY += ScenPart.RowHeight;

            Rect defaultSeverityRect = new Rect(scenPartRect.x, rowY, scenPartRect.width, ScenPart.RowHeight);
            bool useDefaultSeverity = severityRange.min < 0f;
            Widgets.CheckboxLabeled(defaultSeverityRect, "XMT_ForcedStartingHediffCountUseDefaultSeverity".Translate(), ref useDefaultSeverity);
            if (useDefaultSeverity)
            {
                severityRange = new FloatRange(-1f, -1f);
            }
            else if (severityRange.min < 0f)
            {
                float severity = hediff != null ? hediff.initialSeverity : 0f;
                severityRange = new FloatRange(severity, severity);
            }
            rowY += ScenPart.RowHeight;

            if (severityRange.min >= 0f)
            {
                float maxSeverity = Mathf.Max(1f, hediff?.maxSeverity ?? 1f);
                Widgets.FloatRange(new Rect(scenPartRect.x, rowY, scenPartRect.width, 31f), GetHashCode(), ref severityRange, 0f, maxSeverity, "XMT_ForcedStartingHediffCountSeverity");
            }
        }

        public override void PostGameStart()
        {
            base.PostGameStart();

            if (applied || count <= 0 || hediff == null)
            {
                return;
            }

            List<Pawn> pawns = StartingPawns();
            int alreadyAffected = pawns.Count(pawn => pawn.health?.hediffSet?.HasHediff(hediff) == true);
            int toApply = count - alreadyAffected;
            if (toApply > 0)
            {
                foreach (Pawn pawn in pawns.Where(CanApplyTo).InRandomOrder().Take(toApply))
                {
                    AddHediff(pawn);
                }
            }

            applied = true;
        }

        private List<Pawn> StartingPawns()
        {
            GameInitData initData = Current.Game?.InitData;
            if (initData?.startingAndOptionalPawns != null && initData.startingAndOptionalPawns.Count > 0)
            {
                int pawnCount = initData.startingPawnCount > 0 ? initData.startingPawnCount : count;
                return initData.startingAndOptionalPawns
                    .Take(pawnCount)
                    .Where(IsEligibleStartingPawn)
                    .ToList();
            }

            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                .Where(IsEligibleStartingPawn)
                .ToList();
        }

        private bool IsEligibleStartingPawn(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && pawn.health != null && pawn.Faction == Faction.OfPlayer;
        }

        private bool CanApplyTo(Pawn pawn)
        {
            return IsEligibleStartingPawn(pawn) && pawn.health.hediffSet.HasHediff(hediff) == false;
        }

        private void AddHediff(Pawn pawn)
        {
            BodyPartRecord part = TargetPart(pawn);
            Hediff newHediff = HediffMaker.MakeHediff(hediff, pawn, part);
            if (severityRange.min >= 0f)
            {
                newHediff.Severity = severityRange.RandomInRange;
            }
            pawn.health.AddHediff(newHediff, part);
        }

        private BodyPartRecord TargetPart(Pawn pawn)
        {
            if (bodyPart == null)
            {
                return null;
            }

            return pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(part => part.def == bodyPart);
        }

        private IEnumerable<HediffDef> PossibleHediffs()
        {
            return DefDatabase<HediffDef>.AllDefs.OrderBy(def => def.label);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (hediff == null)
            {
                yield return "hediff is null";
            }

            if (count < 0)
            {
                yield return "count must be zero or greater";
            }
        }

        public override bool HasNullDefs()
        {
            return base.HasNullDefs() || hediff == null;
        }

        public override string Summary(Scenario scen)
        {
            return "XMT_ForcedStartingHediffCountSummary".Translate(count, hediff?.LabelCap ?? "null");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref count, "count", 1);
            Scribe_Defs.Look(ref hediff, "hediff");
            Scribe_Defs.Look(ref bodyPart, "bodyPart");
            Scribe_Values.Look(ref severityRange, "severityRange", new FloatRange(-1f, -1f));
            Scribe_Values.Look(ref applied, "applied", false);
        }
    }
}
