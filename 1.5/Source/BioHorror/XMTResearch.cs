using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class XMTResearch
    {
        public static bool ProjectsVisible()
        {
            return Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Starbeast_Genetics) > 0;
        }
    }
}
