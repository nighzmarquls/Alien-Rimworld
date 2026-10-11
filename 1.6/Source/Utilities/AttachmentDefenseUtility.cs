using UnityEngine;

namespace Xenomorphtype
{
    public static class AttachmentDefenseUtility
    {
        public static float AdjustDefenseChance(float chance, float trauma, ObsessionSeverity obsession)
        {
            if (obsession == ObsessionSeverity.Enthralled)
            {
                return 0f;
            }
            chance = Mathf.Clamp01(chance);
            if (obsession >= ObsessionSeverity.Obsessed)
            {
                return chance * 0.25f;
            }
            // Halve passage through each check at full trauma: both together retain 25%.
            return 1f - (1f - chance) * (1f - 0.5f * Mathf.Clamp01(trauma));
        }
    }
}
