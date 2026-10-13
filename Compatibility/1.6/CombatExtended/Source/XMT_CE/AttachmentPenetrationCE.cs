using CombatExtended;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;
using Xenomorphtype;

namespace XMT_CE
{
    internal static class AttachmentPenetrationCE
    {
        // Extracted from the installed CE ArmorUtilityCE.TryPenetrateArmor arithmetic.
        // Apparel layers use zero body-part density; native damage application is a separate step.
        internal static PenetrationResult Resolve(PenetrationRequest request, float roll)
        {
            StatDef stat = request.DamageDef.armorCategory?.armorRatingStat;
            float armor = stat != null ? ArmorUtilityCE.PartialStat(request.Armor, stat, request.Part) : 0f;
            float penetration = request.Penetration;
            float damage = request.Damage;
            bool sharp = request.DamageDef.armorCategory == DamageArmorCategoryDefOf.Sharp;
            bool heat = request.DamageDef.armorCategory == CE_DamageArmorCategoryDefOf.Heat;
            bool deflected = sharp && armor > penetration;
            DamageDefExtensionCE extension = request.DamageDef.GetModExtension<DamageDefExtensionCE>();
            float fraction = deflected && extension?.noDamageOnDeflect == true ? 0f
                : penetration == 0f ? 1f : Mathf.Clamp01((penetration - armor) / penetration);
            deflected |= fraction == 0f;
            float passedDamage = damage * fraction;
            bool soft = request.Armor.Stuff?.stuffProps?.categories?.Contains(StuffCategoryDefOf.Fabric) == true
                || request.Armor.def.GetModExtension<ApparelDefExtension>()?.isSoftArmor == true;
            float wear;
            if (soft)
            {
                wear = heat ? request.Armor.GetStatValue(StatDefOf.Flammability) * damage
                    : sharp ? Mathf.Max(damage * 0.2f, damage - passedDamage) : 0f;
            }
            else if (!sharp && penetration / armor < 0.5f)
            {
                wear = 0f;
            }
            else if (heat)
            {
                wear = request.Armor.GetStatValue(StatDefOf.Flammability) * damage * 0.5f;
            }
            else
            {
                wear = penetration == 0f || armor == 0f ? 0f : 0.5f * ((damage - passedDamage)
                    * Mathf.Min(1f, penetration * penetration / (armor * armor))
                    + passedDamage * Mathf.Clamp01(armor / penetration));
            }
            return new PenetrationResult(!deflected, request.DamageDef, deflected ? 0f : passedDamage,
                Mathf.Max(0f, penetration - armor), wear);
        }
    }
}
