using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    internal static class MutationRegistration
    {
        static MutationRegistration()
        {
            var recipes = DefDatabase<RecipeDef>.AllDefsListForReading.Where(r => r.GetModExtension<MutationSurgeryExtension>() != null || r.workerClass == typeof(Recipe_ClearCryptimorphicExposure)).ToList();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.race != null))
            {
                foreach (RecipeDef recipe in recipes)
                {
                    recipe.recipeUsers ??= new List<ThingDef>();
                    if (!recipe.recipeUsers.Contains(def)) recipe.recipeUsers.Add(def);
                    def.recipes ??= new List<RecipeDef>();
                    if (!def.recipes.Contains(recipe)) def.recipes.Add(recipe);
                }
                def.inspectorTabsResolved ??= new List<InspectTabBase>();
                if (!def.inspectorTabsResolved.Any(t => t is ITab_CryptimorphicAssessment))
                    def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(typeof(ITab_CryptimorphicAssessment)));
            }
        }
    }
}
