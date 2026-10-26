
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public class XMT_HediffByPart
    {
        public HediffDef hediffDef;
        public BodyPartDef bodyPartDef = null;
        public bool removePromoter = true;
        public bool samePart = true;
        public float chance = 1.0f;
        public float increaseSeverity = 0f;
        public bool onlyTargetSolid = false;
    }
    public class HediffComp_PerPartPromoter : HediffComp
    {
        HediffCompProperties_PerPartPromoter Props => props as HediffCompProperties_PerPartPromoter;

        bool stabilized = false;
        float promotedAtSeverity = -1f;

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref stabilized, "stabilized", false);
            Scribe_Values.Look(ref promotedAtSeverity, "promotedAtSeverity", -1f);
            // Older saves did not record the severity of the last successful promotion.
            if (Scribe.mode == LoadSaveMode.PostLoadInit && stabilized && promotedAtSeverity < 0f)
                promotedAtSeverity = parent.Severity;
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            HediffComp_GetsPermanent permanent = parent.GetComp<HediffComp_GetsPermanent>();
            if (permanent != null)
            {
                permanent.IsPermanent = true;
            }
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (!ReadyForPromotion(parent.Severity, Props.promotionSeverity, stabilized, promotedAtSeverity))
            {
                return;
            }

            if (Pawn == null || Pawn.health == null)
            {
                return;
            }

            if (parent.Severity >= Props.promotionSeverity)
            {
                BodyPartRecord part = parent.Part;
                bool promoted = false;


                foreach (XMT_HediffByPart promotion in Props.HediffPromotionsByPart)
                {
                    if (part != null && promotion.bodyPartDef == part.def && promotion.samePart)
                    {
                        if (!part.def.IsSolidInDefinition_Debug && promotion.onlyTargetSolid)
                        {
                            continue;
                        }

                        if (Rand.Chance(promotion.chance))
                        {
                            promoted = PromoteIntoHediff(promotion.hediffDef, severityIncrease: promotion.increaseSeverity, removePromoter: promotion.removePromoter);
                            if (promoted)
                            {
                                break;
                            }
                        }
                    }

                    if (promotion.bodyPartDef == null || !promotion.samePart)
                    {
                        if (Rand.Chance(promotion.chance))
                        {
                            promoted = PromoteIntoHediff(promotion.hediffDef, promotePart: promotion.bodyPartDef, useParentPart: false, severityIncrease: promotion.increaseSeverity, removePromoter: promotion.removePromoter, onlySolid: promotion.onlyTargetSolid);
                            if (promoted)
                            {
                                break;
                            }
                        }
                    }
                }
                
                stabilized = promoted;
                if (promoted) promotedAtSeverity = parent.Severity;
            }


        }

        private bool PromoteIntoHediff(HediffDef hediffDef, BodyPartDef promotePart = null, bool useParentPart = true, float severityIncrease = 0f, bool removePromoter = true, bool onlySolid = false)
        {
            if (hediffDef == null) return false;
            BodyPartRecord part;
            if (useParentPart) part = parent.Part;
            else if (promotePart != null)
                part = Pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(p => p.def == promotePart);
            else
            {
                var candidates = Pawn.health.hediffSet.GetNotMissingParts()
                    .Where(p => onlySolid ? p.def.IsSolidInDefinition_Debug : p.IsCorePart).ToList();
                if (candidates.Count == 0) return false;
                part = onlySolid ? candidates.RandomElement() : candidates[0];
            }
            if (part == null) return false;
            // Match the exact anatomical site; a completed change is not a successful promotion.
            Hediff existing = Pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.def == hediffDef && h.Part == part);
            if (existing != null)
            {
                float severity = NextSeverity(hediffDef, existing.Severity, severityIncrease);
                if (severity <= existing.Severity) return false;
                existing.Severity = severity;
            }
            else
            {
                Hediff added = Pawn.health.AddHediff(hediffDef, part);
                if (added == null || !Pawn.health.hediffSet.hediffs.Contains(added)) return false;
                added.Severity = Mathf.Min(hediffDef.maxSeverity, added.Severity + severityIncrease);
            }
            if (removePromoter) Pawn.health.RemoveHediff(parent);
            return true;
        }
        public static float NextSeverity(HediffDef def, float severity, float increase) =>
            Mathf.Min(def.maxSeverity, severity + (increase > 0f ? increase : def.initialSeverity));

        public static bool ReadyForPromotion(float severity, float threshold, bool stabilized, float previousSeverity) =>
            severity >= threshold && (!stabilized || severity > previousSeverity + 0.00001f);

    }
    public class HediffCompProperties_PerPartPromoter : HediffCompProperties
    {
        public List<XMT_HediffByPart> HediffPromotionsByPart;
        public float promotionSeverity = 0.75f;
        public HediffCompProperties_PerPartPromoter()
        {
            compClass = typeof(HediffComp_PerPartPromoter);
        }
    }
}
