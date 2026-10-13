using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public readonly struct PenetrationRequest
    {
        public readonly Apparel Armor;
        public readonly BodyPartRecord Part;
        public readonly DamageDef DamageDef;
        public readonly float Damage;
        public readonly float Penetration;

        public PenetrationRequest(Apparel armor, BodyPartRecord part, DamageDef damageDef, float damage, float penetration)
        {
            Armor = armor;
            Part = part;
            DamageDef = damageDef;
            Damage = Mathf.Max(0f, damage);
            Penetration = Mathf.Max(0f, penetration);
        }
    }

    public readonly struct PenetrationResult
    {
        public readonly bool Penetrated;
        public readonly DamageDef DamageDef;
        public readonly float Damage;
        public readonly float Penetration;
        public readonly float ApparelDamage;

        public PenetrationResult(bool penetrated, DamageDef damageDef, float damage, float penetration, float apparelDamage)
        {
            Penetrated = penetrated;
            DamageDef = damageDef;
            Damage = damage;
            Penetration = penetration;
            ApparelDamage = apparelDamage;
        }
    }

    public static class PenetrationUtility
    {
        // Compatibility adapters use their own armor units. Queries never damage equipment.
        public static Func<PenetrationRequest, float, PenetrationResult> Resolver;

        public static PenetrationResult Resolve(PenetrationRequest request)
        {
            return (Resolver ?? ResolveVanilla)(request, Rand.Value);
        }

        public static PenetrationResult ResolveVanilla(PenetrationRequest request, float roll)
        {
            StatDef stat = request.DamageDef.armorCategory?.armorRatingStat;
            float rating = stat != null ? request.Armor.GetStatValue(stat) : 0f;
            float effectiveArmor = Mathf.Max(0f, rating - request.Penetration);
            float wear = request.Damage * 0.25f;
            if (roll < effectiveArmor * 0.5f)
            {
                return new PenetrationResult(false, request.DamageDef, 0f, request.Penetration, wear);
            }
            if (roll < effectiveArmor)
            {
                DamageDef damageDef = request.DamageDef.armorCategory == DamageArmorCategoryDefOf.Sharp
                    ? DamageDefOf.Blunt : request.DamageDef;
                return new PenetrationResult(false, damageDef, request.Damage * 0.5f, request.Penetration, wear);
            }
            return new PenetrationResult(true, request.DamageDef, request.Damage, request.Penetration, wear);
        }

        public static void ApplyApparelDamage(PenetrationRequest request, PenetrationResult result, Pawn attacker)
        {
            if (!request.Armor.Destroyed && result.ApparelDamage > 0f)
            {
                request.Armor.TakeDamage(new DamageInfo(request.DamageDef, GenMath.RoundRandom(result.ApparelDamage),
                    0f, instigator: attacker));
            }
        }
    }
}
