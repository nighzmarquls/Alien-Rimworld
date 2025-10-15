
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class BackstoryHorror
    {
        public BackstoryDef    backstory;
        public float           obsession = 0;
    }
    public class XMT_BackstorySet : Def
    {
        public List<BackstoryHorror> backstories;
    }
}
