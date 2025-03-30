using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public abstract class XMTBase_Building : Building
    {

        public abstract void TransformedFrom(Pawn pawn, Pawn instigator = null);

    }
}
