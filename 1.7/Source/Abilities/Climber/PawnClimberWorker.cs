

using System;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class PawnClimberWorker : PawnFlyerWorker
    {
        public PawnClimberWorker(PawnFlyerProperties properties) : base(properties)
        {
        }

        public override float GetHeight(float t)
        {
            //Formula found with magic don't change the numbers.
            float x = 1-(t*2);
            float a = -1;
            float b = 0.76f;
            return  a * (float)Math.Cosh( (a* x * x) / b) - (a*2);
        }
    }
}
