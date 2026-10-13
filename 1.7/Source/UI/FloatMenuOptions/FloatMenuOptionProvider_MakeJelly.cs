using RimWorld;
using VEF.Graphics;
using Verse;
using Verse.AI;


namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_MakeJelly : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;
        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            CompJellyMaker jellyMaker = context.FirstSelectedPawn.GetComp<CompJellyMaker>();

            if (jellyMaker != null)
            {
                if (jellyMaker.CanMakeIntoJelly(clickedThing))
                {
                    if (FeralJobUtility.IsThingAvailableForJobBy(context.FirstSelectedPawn, clickedThing))
                    {
                        string JellyName = jellyMaker.GetJellyProduct()?.label;
                        FloatMenuOption JellyOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_Make".Translate() + JellyName, delegate
                        {
                            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ProduceJelly, clickedThing);
                            context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.MiscWork);
                        }, priority: MenuOptionPriority.High), context.FirstSelectedPawn, clickedThing);

                        return JellyOption;
                    }
                }
            }

            return null;
        }
    }
}
