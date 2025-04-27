
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class XMTSettings : ModSettings
    {
        static private XMTSettings instance;

        public static bool LogJobGiver => instance != null ? instance._logJobGiver : false;
        public static bool LogBiohorror => instance != null ? instance._logBiohorror : false;
        public static bool LogRituals => instance != null ? instance._logRituals : false;
        public static bool LogWorld => instance != null ? instance._logWorld : false;

        private bool _logJobGiver = false;
        private bool _logBiohorror = false;
        private bool _logRituals = false;
        private bool _logWorld = false;

        public static bool PlayerSabotage => instance != null ? instance._playerSabotage : true;
        private bool _playerSabotage = true;

        public static bool HorrorPregnancy => instance != null ? instance._horrorPregnancy : true;
        private bool _horrorPregnancy = true;
        public static float JellyNutritionEfficiency => instance != null ? instance._jellyNutritionEfficiency : 0.5f;
        public static float JellyMassEfficiency => instance != null ? instance._jellyMassEfficiency : 0.025f;

        private float _jellyNutritionEfficiency = 0.5f;
        private float _jellyMassEfficiency = 0.025f;

        private static Vector2 scrollPosition;
        private static float height_modifier = 100f;

        public static int MinimumOpinionForHiveFriend = instance != null ? instance._minimumOpinionForHiveFriend : 80;

        private int _minimumOpinionForHiveFriend = 80;

        public static float WildEmbryoChance => instance != null ? instance._wildEmbryoChance : 0.25f;
        private float _wildEmbryoChance = 0.25f;

        public static float WildMorphHuntChance => instance != null ? instance._wildMorphHuntChance : 0.25f;
        private float _wildMorphHuntChance = 0.25f;

        public static float LaidEggMaturationTime => instance != null ? instance._laidEggMaturationTime : 1f;
        private float _laidEggMaturationTime = 1;

        private string TooltipForEggMaturation()
        {
            return Mathf.Floor(_laidEggMaturationTime * 24) + " hours";
        }

        private string TooltipForChance(float value)
        {
            return Mathf.Floor(value * 100) + "% chance";
        }
        private string TooltipForJellyNutrition()
        {
            return Mathf.Floor(_jellyNutritionEfficiency * 100) + "% efficient";
        }

        private string TooltipForJellyMass()
        {
            return Mathf.Floor(_jellyMassEfficiency * 100) + "% efficient";
        }
        public void DoWindowContents(Rect inRect)
        {
            Rect outRect = new Rect(0f, 30f, inRect.width, inRect.height - 30f);
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, inRect.height + height_modifier);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect); 

            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.maxOneColumn = true;
            listingStandard.ColumnWidth = viewRect.width;
            listingStandard.Begin(viewRect);
            listingStandard.Gap(4f);
            listingStandard.CheckboxLabeled("Log Job Giver", ref _logJobGiver, "Enable log reporting on job giver");
            listingStandard.Gap(5f);
            listingStandard.CheckboxLabeled("Log Rituals", ref _logRituals, "Enable log reporting on rituals");
            listingStandard.Gap(5f);
            listingStandard.CheckboxLabeled("Log Bio Horror", ref _logBiohorror, "Enable log reporting on mutations and other biohorror");
            listingStandard.Gap(5f);
            listingStandard.CheckboxLabeled("Log World Horror", ref _logWorld, "Enable log reporting on world horror system");
            listingStandard.Gap(5f);

            listingStandard.CheckboxLabeled("Enable Player Sabotage Behavior", ref _playerSabotage, "If enabled player xenomorphs will sabotage human infrastructure when recreation or mood is low.");
            listingStandard.Gap(5f);
            listingStandard.CheckboxLabeled("Enable Horror Pregnancies", ref _horrorPregnancy, "If enabled xenomorph corruption will cause horror pregnancies.");
            listingStandard.Gap(5f);

            _jellyNutritionEfficiency = listingStandard.SliderLabeled("Jelly Nutrition Efficiency", _jellyNutritionEfficiency, 0.01f, 4f, tooltip: TooltipForJellyNutrition());
            listingStandard.Gap(2f);
            listingStandard.LabelDouble("",TooltipForJellyNutrition());
            listingStandard.Gap(5f);
            _jellyMassEfficiency = listingStandard.SliderLabeled("Jelly Mass Efficiency", _jellyMassEfficiency, 0.01f, 4f, tooltip: TooltipForJellyMass());
            listingStandard.Gap(2f);
            listingStandard.LabelDouble("", TooltipForJellyMass());
            listingStandard.Gap(5f);
            _wildEmbryoChance = listingStandard.SliderLabeled("Wild Embryo Chance Factor", _wildEmbryoChance, 0.00f, 1f, tooltip: TooltipForChance(_wildEmbryoChance));
            listingStandard.Gap(2f);
            listingStandard.LabelDouble("", TooltipForChance(_wildEmbryoChance));
            listingStandard.Gap(5f);
            _wildMorphHuntChance = listingStandard.SliderLabeled("Wild Alien Chance Factor", _wildMorphHuntChance, 0.00f, 1f, tooltip: TooltipForChance(_wildMorphHuntChance));
            listingStandard.Gap(2f);
            listingStandard.LabelDouble("", TooltipForChance(_wildMorphHuntChance));
            listingStandard.Gap(5f);
            _minimumOpinionForHiveFriend = Mathf.FloorToInt(listingStandard.SliderLabeled("Min Relationship For Hive Friend", _minimumOpinionForHiveFriend, -100, 100f, tooltip: ""));
            listingStandard.Gap(2f);
            listingStandard.LabelDouble("", _minimumOpinionForHiveFriend.ToString());
            listingStandard.Gap(5f);
            _laidEggMaturationTime = listingStandard.SliderLabeled("Time for Laid Ovamorphs to Incubate", _laidEggMaturationTime, 0, 10f, tooltip: TooltipForEggMaturation());
            listingStandard.Gap(2f);
            listingStandard.LabelDouble("", TooltipForEggMaturation());
            listingStandard.Gap(5f);

            listingStandard.End();
            Widgets.EndScrollView();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _logJobGiver, "logJobGiver", false, false);
            Scribe_Values.Look(ref _logBiohorror, "logBiohorror", false, false);
            Scribe_Values.Look(ref _logRituals, "logRituals", false, false);
            Scribe_Values.Look(ref _logWorld, "logWorld", false, false);

            Scribe_Values.Look(ref _playerSabotage, "playerSabotage", true, false);
            Scribe_Values.Look(ref _horrorPregnancy, "horrorPregnancy", true, false);

            Scribe_Values.Look(ref _jellyNutritionEfficiency, "jellyNutritionEfficiency", 0.5f, false);
            Scribe_Values.Look(ref _jellyMassEfficiency, "jellyMassEfficiency", 0.025f, false);
            Scribe_Values.Look(ref _wildEmbryoChance, "wildEmbryoChance", 0.25f, false);
            Scribe_Values.Look(ref _wildMorphHuntChance, "wildMorphHuntChance", 0.25f, false);
            Scribe_Values.Look(ref _minimumOpinionForHiveFriend, "minimumOpinionForHiveFriend", 80, false);
            Scribe_Values.Look(ref _laidEggMaturationTime, "laidEggMaturationTime", 1, false); 
            instance = this;
        }
        
    }
}
