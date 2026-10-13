
using RimWorld;
using Verse;

namespace Xenomorphtype { 
    public class CompStudyXenomorph : CompStudyUnlocks
    {
        protected new CompProperties_StudyUnlocks Props => (CompStudyXenomorphProperties)props;

        
    }

    public class CompStudyXenomorphProperties : CompProperties_StudyUnlocks
    {
        public ResearchProjectDef researchProject;
        public CompStudyXenomorphProperties()
        {
            compClass = typeof(CompStudyXenomorph);
        }

    }
}
