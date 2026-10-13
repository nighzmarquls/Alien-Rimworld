using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_Transformation : HediffComp_SeverityModifierBase
    {
        HediffCompProperties_Transformation Props => props as HediffCompProperties_Transformation;
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (parent.Severity >= 1.0f)
            {
                if (parent.pawn.IsHashIntervalTick(60))
                {
                    Transform();
                }
            }
        }

        public void Transform()
        {

            if (BioUtility.TryGetMutationForm(Pawn, out ThingDef thingForm, out PawnKindDef pawnForm))
            {
                if (pawnForm != null)
                {
                    if (XMTUtility.TransformPawnIntoPawn(Pawn, pawnForm, out Pawn result))
                    {
                        return;
                    }
                }

                if (thingForm != null)
                {
                    if (XMTUtility.TransformPawnIntoThing(Pawn, thingForm, out Thing result))
                    {
                        return;
                    }
                }

            }

            Log.Message(Pawn + " cannot find a new form!");

        }

        public override float SeverityChangePerDay()
        {
            return Props.SeverityPerDay;
        }
    }

    public class HediffCompProperties_Transformation : HediffCompProperties
    {
        public float DeathTriggerSeverity = 1;
        public float SeverityPerDay = 1;
   
        public HediffCompProperties_Transformation()
        {
            compClass = typeof(HediffComp_Transformation);
        }
    }
}
