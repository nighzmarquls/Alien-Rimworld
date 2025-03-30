using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class Designator_Hive_Cancel : Designator_Cancel
    {
        public override bool Disabled {
            get
            {
                return !XMTUtility.PlayerXenosOnMap(Find.CurrentMap);
            }
        }
        public override bool Visible
        {
            get
            {
                return XMTUtility.PlayerXenosOnMap(Find.CurrentMap);
            }
        }
    }
}
