using RimWorld;


namespace Xenomorphtype
{
  public class FloatMenuOptionProvider_Arrest : FloatMenuOptionProvider
  {
      protected override bool Drafted => true;

      protected override bool Undrafted => true;

      protected override bool Multiselect => false;

      protected override bool RequiresManipulation => true;
        /*
      protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
      {
          Pawn pawn = context.FirstSelectedPawn;
          if (!XMTUtility.IsXenomorph(pawn))
          {
              return null;
          }

          if(pawn.genes != null)
          {
              if(pawn.genes.HasActiveGene(ExternalDefOf.XMT_NaturalBiotic))
              {
                  string text =  (string)"ConsumeThing".Translate(clickedThing.LabelShort, clickedThing);
                  FloatMenuOption floatMenuOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, delegate
                  {
                      int maxAmountToPickup2 = FoodUtility.GetMaxAmountToPickup(clickedThing, context.FirstSelectedPawn, FoodUtility.WillIngestStackCountOf(context.FirstSelectedPawn, clickedThing.def, FoodUtility.NutritionForEater(context.FirstSelectedPawn, clickedThing)));
                      if (maxAmountToPickup2 != 0)
                      {
                          clickedThing.SetForbidden(value: false);
                          Job job = JobMaker.MakeJob(JobDefOf.Ingest, clickedThing);
                          job.count = maxAmountToPickup2;
                          context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                      }
                  }, (clickedThing is Corpse) ? MenuOptionPriority.Low : MenuOptionPriority.Default), context.FirstSelectedPawn, clickedThing);

                  return floatMenuOption;
              }
          }
          return null;

      }
        */
  }
}
