
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using VEF;
using Verse;

namespace Xenomorphtype
{
    internal class JellyPod : Building_Bed
    {
        CompRefuelable _refuelable;
        CompRefuelable refuelable
        {
            get
            {
                if (_refuelable == null)
                {
                    _refuelable = GetComp<CompRefuelable>();
                }
                return _refuelable;
            }
        }

        private Graphic _fullGraphic;
        public override Graphic Graphic
        {
            get
            {
                if (refuelable.HasFuel)
                {
                    if (_fullGraphic is null)
                    {
                        var data = new GraphicData();
                        data.CopyFrom(def.graphicData);
                        data.texPath += "_full";
                        _fullGraphic = data.GraphicColoredFor(this);
                    }
                    return _fullGraphic;

                }
                else
                {
                    return base.Graphic;
                }
            }
        }

        public bool SoakInJelly(Pawn pawn)
        {
            if(refuelable.Fuel < 25)
            {
                return false;
            }

            bool healed = false;

            if (XMTUtility.IsXenomorph(pawn))
            {
                IEnumerable<Hediff> undeveloped = pawn.health.hediffSet.hediffs.Where(x => x.def == InternalDefOf.Undeveloped);

                if (undeveloped != null && undeveloped.Any())
                {
                    foreach (Hediff part in undeveloped)
                    {
                        part.Severity += 0.5f;
                    }
                    refuelable.ConsumeFuel(25);
                    return true;
                }
                var injuredHediffs = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList();
                if (injuredHediffs.Any())
                {
                    injuredHediffs.RandomElement().Heal(1f);
                    healed = true;
                }
                else
                {
                    var nonMissingParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
                    var missingParts = pawn.def.race.body.AllParts.Where(x => pawn.health.hediffSet.PartIsMissing(x)
                        && nonMissingParts.Contains(x.parent) && !pawn.health.hediffSet.AncestorHasDirectlyAddedParts(x)).ToList();
                    if (missingParts.Any())
                    {
                        var missingPart = missingParts.RandomElement();
                        var MissingHediffsBefore = pawn.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().ToList();
                        pawn.health.RestorePart(missingPart);
                        var MissingHediffsAfter = pawn.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().ToList();
                        var removedMissingPartHediff = MissingHediffsBefore.Where(x => !MissingHediffsAfter.Contains(x));
                        foreach (var missingPartHediff in removedMissingPartHediff)
                        {
                            var undevelopedHediff = HediffMaker.MakeHediff(InternalDefOf.Undeveloped, pawn, missingPartHediff.Part);

                            pawn.health.AddHediff(undevelopedHediff);
                        }
                        healed = true;
                    }

                }
            }
            else
            {
                //TODO: Make the parts that are regrowing always Xenomorph Mutation limbs!
                IEnumerable<Hediff> undeveloped = pawn.health.hediffSet.hediffs.Where(x => x.def == InternalDefOf.Undeveloped);

                if (undeveloped != null && undeveloped.Any())
                {
                    foreach (Hediff part in undeveloped)
                    {
                        part.Severity += 0.1f;
                    }
                    refuelable.ConsumeFuel(25);
                    return true;
                }
                var injuredHediffs = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList();
                if (injuredHediffs.Any())
                {
                    injuredHediffs.RandomElement().Heal(0.5f);
                    healed = true;
                }
                else
                {
                    var nonMissingParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
                    var missingParts = pawn.def.race.body.AllParts.Where(x => pawn.health.hediffSet.PartIsMissing(x)
                        && nonMissingParts.Contains(x.parent) && !pawn.health.hediffSet.AncestorHasDirectlyAddedParts(x)).ToList();
                    if (missingParts.Any())
                    {
                        var missingPart = missingParts.RandomElement();
                        var MissingHediffsBefore = pawn.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().ToList();
                        pawn.health.RestorePart(missingPart);
                        var MissingHediffsAfter = pawn.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().ToList();
                        var removedMissingPartHediff = MissingHediffsBefore.Where(x => !MissingHediffsAfter.Contains(x));
                        foreach (var missingPartHediff in removedMissingPartHediff)
                        {
                            var undevelopedHediff = HediffMaker.MakeHediff(InternalDefOf.Undeveloped, pawn, missingPartHediff.Part);

                            pawn.health.AddHediff(undevelopedHediff);
                        }
                        healed = true;
                    }

                }

                BioUtility.TryMutatingPawn(ref pawn);
            }

            if (healed)
            {
                refuelable.ConsumeFuel(25);
                return true;
            }
            return false;
        }

    }
}
