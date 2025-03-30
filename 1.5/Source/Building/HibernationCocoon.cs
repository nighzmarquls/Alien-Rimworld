using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class HibernationCocoon : Building_CryptosleepCasket
    {
        public override void Open()
        {
            base.Open();
            Destroy();
        }
        public override bool Accepts(Thing thing)
        {
            if (XMTUtility.IsXenomorph(thing))
            {
                return true;
            }
            return false;
        }
    }

}
