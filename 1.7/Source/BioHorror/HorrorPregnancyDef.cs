using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class HorrorPregnancyTemplate
    {
        public HediffDef pregnancy;
        public List<HorrorChild> babies = new List<HorrorChild>();
    }

    public class HorrorPregnancyDef : Def
    {
        public SimpleCurve essenceCurve;
        public SimpleCurve conceptionChance;
        public SimpleCurve corruptionChance;
        public string fetusTexPath = "XMT/Menstruation/Default/Fetus/Horror/";
        public List<HorrorPregnancyTemplate> templates = new List<HorrorPregnancyTemplate>();

        public float Tune(float essence) => Mathf.Max(0f, essenceCurve.Evaluate(Mathf.Max(0f, essence)));
        public float Chance(float essence, bool converting) => Mathf.Clamp01(
            (converting ? corruptionChance : conceptionChance).Evaluate(Tune(essence)));

        public static bool GuaranteesLifecycle(float maternalEssence, bool matureMorph) => matureMorph ||
            maternalEssence >= CryptimorphicInfluenceDef.Current.maternalLifecycleThreshold;

        public HorrorChild Select(HediffDef pregnancy, float essence, Pawn mother = null)
        {
            bool developedMother = mother != null && mother.RaceProps.Humanlike && GuaranteesLifecycle(BioUtility.GetXenomorphInfluence(mother), mother.GetMorphComp() != null);
            if (mother != null && (mother.GetMorphComp() != null ||
                (pregnancy != XenoGeneDefOf.XMT_HorrorPregnant && developedMother)))
                return new HorrorChild { childKind = DefDatabase<PawnKindDef>.GetNamed("XMT_Starbeast_Pod"), gestationFactor = 0.1f, probability = 1f };
            List<HorrorChild> choices = templates.FirstOrDefault(t => t.pregnancy == pregnancy)?.babies
                ?? pregnancy?.comps?.OfType<HediffCompProperties_Comp_HorrorPregnant>().FirstOrDefault()?.babies;
            if (choices == null) return null;
            float tuned = Tune(essence);
            return choices.FirstOrDefault(c => c.childKind != null && (!c.requiresDevelopedMother || developedMother) && tuned >= c.essenceMinimum &&
                tuned <= c.essenceMaximum && Rand.Chance(c.probability));
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (essenceCurve == null || essenceCurve.PointsCount == 0) yield return "Missing essenceCurve";
            if (conceptionChance == null || conceptionChance.PointsCount == 0) yield return "Missing conceptionChance";
            if (corruptionChance == null || corruptionChance.PointsCount == 0) yield return "Missing corruptionChance";
            foreach (HorrorPregnancyTemplate template in templates)
                if (template.pregnancy == null || template.babies.NullOrEmpty() ||
                    template.babies.Any(c => c.childKind == null || c.gestationFactor <= 0f))
                    yield return "Invalid horror pregnancy template";
        }
    }
}
