using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class Recipe_DismantleMutation : RecipeWorker
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!base.AvailableOnNow(thing, part) || thing?.Spawned != true) return false;

            // Each breakdown consumes one harvested part. Menu availability needs only
            // the per-def map index; normal bill work still checks reservations and paths.
            Map map = thing.Map;
            foreach (ThingDef ingredient in recipe.fixedIngredientFilter.AllowedThingDefs)
            {
                foreach (Thing candidate in map.listerThings.ThingsOfDef(ingredient))
                {
                    if (candidate.stackCount > 0 && !candidate.IsForbidden(Faction.OfPlayer) && !candidate.Position.Fogged(map))
                        return true;
                }
            }
            return false;
        }
    }
}
