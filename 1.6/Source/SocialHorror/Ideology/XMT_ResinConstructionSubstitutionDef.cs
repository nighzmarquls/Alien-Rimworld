using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public class XMT_ResinConstructionSubstitutionDef : Def
    {
        public ThingFilter structuralIngredientFilter;

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            structuralIngredientFilter?.ResolveReferences();
        }

        public bool Substitutes(ThingDef thingDef)
        {
            if (thingDef == null || structuralIngredientFilter == null)
            {
                return false;
            }

            return structuralIngredientFilter.Allows(thingDef);
        }

        public static XMT_ResinConstructionSubstitutionDef ActiveDef
        {
            get
            {
                return DefDatabase<XMT_ResinConstructionSubstitutionDef>.AllDefsListForReading.FirstOrDefault();
            }
        }

        public static List<ThingDefCountClass> RequiredMaterialCost(ThingDef buildDef)
        {
            List<ThingDefCountClass> result = new List<ThingDefCountClass>();
            if (buildDef?.costList == null)
            {
                return result;
            }

            XMT_ResinConstructionSubstitutionDef substitutionDef = ActiveDef;
            foreach (ThingDefCountClass cost in buildDef.costList)
            {
                if (cost?.thingDef == null)
                {
                    continue;
                }

                if (substitutionDef != null && substitutionDef.Substitutes(cost.thingDef))
                {
                    continue;
                }

                result.Add(new ThingDefCountClass(cost.thingDef, cost.count));
            }

            return result;
        }
    }
}
