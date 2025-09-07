
using System.Collections.Generic;
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

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref stabilized, "stabilized", false);
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
            if(stabilized)
            {
                return;
            }


            if(parent.Severity >= Props.promotionSeverity)
            {
                BodyPartRecord part = parent.Part;


                foreach (XMT_HediffByPart promotion in Props.HediffPromotionsByPart)
                {
                    if (promotion.bodyPartDef == part.def && promotion.samePart)
                    {
                        if (!part.def.IsSolidInDefinition_Debug && promotion.onlyTargetSolid)
                        {
                            continue;
                        }

                        if (Rand.Chance(promotion.chance))
                        {
                            PromoteIntoHediff(promotion.hediffDef, severityIncrease: promotion.increaseSeverity, removePromoter: promotion.removePromoter);
                            break;
                        }
                    }

                    if (promotion.bodyPartDef == null || !promotion.samePart)
                    {
                        if (Rand.Chance(promotion.chance))
                        {
                            PromoteIntoHediff(promotion.hediffDef, promotePart: promotion.bodyPartDef, useParentPart: false, severityIncrease: promotion.increaseSeverity, removePromoter: promotion.removePromoter, onlySolid: promotion.onlyTargetSolid);
                            break;
                        }
                    }
                }
                
                stabilized = true;
            }


        }

        private void PromoteIntoHediff(HediffDef hediffDef, BodyPartDef promotePart = null, bool useParentPart = true, float severityIncrease = 0f, bool removePromoter = true, bool onlySolid = false)
        {
            if (XMTSettings.LogBiohorror)
            {
                Log.Message(parent + " promoting hediff " + hediffDef);
            }
            bool partAdded = false;
            if (useParentPart)
            {
                Hediff promoted = parent.pawn.health.GetOrAddHediff(hediffDef, parent.Part);
                if (promoted != null)
                {
                    promoted.Severity += severityIncrease;
                    partAdded = true;
                }
            }
            else if (promotePart != null)
            {
                BodyPartRecord part = parent.pawn.health.hediffSet.GetBodyPartRecord(promotePart);
                Hediff promoted = parent.pawn.health.GetOrAddHediff(hediffDef, part);
                if (promoted != null)
                {
                    promoted.Severity += severityIncrease;
                    partAdded = true;
                }
            }
            else
            {
                BodyPartRecord part = null;
                List<BodyPartRecord> bodyparts = parent.pawn.health.hediffSet.GetNotMissingParts().ToList();
                if (onlySolid)
                {
                    bodyparts.Shuffle();
                    foreach (BodyPartRecord bodypart in bodyparts)
                    {
                        if (bodypart.def.IsSolidInDefinition_Debug)
                        {
                            part = bodypart;
                            break;
                        }
                    }
                }
                else
                {
                    foreach (BodyPartRecord bodypart in bodyparts)
                    {
                        if (bodypart.IsCorePart)
                        {
                            part = bodypart;
                            break;
                        }
                    }
                }
                Hediff promoted = parent.pawn.health.GetOrAddHediff(hediffDef, part);
                if (promoted != null)
                {
                    promoted.Severity += severityIncrease;
                    partAdded = true;
                }
            }

            if (removePromoter && partAdded)
            {
                parent.pawn.health.RemoveHediff(parent);
            }
            
        }
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
