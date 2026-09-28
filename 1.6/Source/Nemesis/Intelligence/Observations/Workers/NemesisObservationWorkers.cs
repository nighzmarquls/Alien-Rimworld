using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using VEF.Abilities;

namespace Xenomorphtype
{
    public sealed class NemesisObservationWorker_Xenoforming : NemesisObservationWorker
    {
        public override NemesisObservationResult Complete(NemesisCensusContext context)
        {
            GameComponent_Xenomorph xenomorph = Current.Game?.GetComponent<GameComponent_Xenomorph>();
            return new NemesisObservationResult { observation = def, value = xenomorph?.Xenoforming ?? 0f, confidence = def.baseConfidence };
        }
    }

    public sealed class NemesisObservationWorker_PawnMetric : NemesisObservationWorker
    {
        private float total;
        private readonly HashSet<string> xenotypes = new HashSet<string>();
        private readonly List<NemesisSpatialContact> contacts = new List<NemesisSpatialContact>();

        public override void Begin(NemesisCensusContext context)
        {
            total = 0f;
            xenotypes.Clear();
            contacts.Clear();
        }

        public override void VisitPawn(Pawn pawn, bool held, NemesisCensusContext context)
        {
            if (pawn == null || pawn.Dead) return;

            if (def.metric == "ContainedCryptimorphs")
            {
                if (held && XMTUtility.IsXenomorph(pawn) && IsPlayerControlledHolder(pawn.ParentHolder))
                {
                    Add(pawn, context, 1f, "ContainedCryptimorph", "ExploitationTarget");
                }
                return;
            }

            if (pawn.Faction != Faction.OfPlayer) return;

            switch (def.metric)
            {
                case "Population":
                    if (pawn.RaceProps.Humanlike && !pawn.IsVehicle() && !pawn.IsHorror() && !XMTUtility.IsXenomorph(pawn)) total++;
                    break;
                case "Mechanoids":
                    if (pawn.RaceProps.IsMechanoid) Add(pawn, context, 1f, "Mechanoid");
                    break;
                case "MechanoidBandwidthUsed":
                    total += InorganicSubversionUtility.PlayerControlledMechanoidBandwidth(pawn);
                    break;
                case "Inorganic":
                    if (pawn.IsInorganic()) Add(pawn, context, 1f, "Inorganic");
                    break;
                case "Vehicles":
                    if (pawn.IsVehicle()) Add(pawn, context, 1f, "Vehicle");
                    break;
                case "PsycasterLevels":
                    total += Mathf.Max(0, pawn.GetPsylinkLevel());
                    break;
                case "PsychicPotential":
                    float sensitivity = Mathf.Max(0f, pawn.GetStatValue(StatDefOf.PsychicSensitivity));
                    total += pawn.GetPsylinkLevel() > 0 ? sensitivity : Mathf.Max(0f, sensitivity - 1f);
                    break;
                case "XenotypeDiversity":
                    if (pawn.RaceProps.Humanlike && pawn.genes != null && !XMTUtility.IsXenomorph(pawn))
                    {
                        NemesisXenotypeRecord sample = NemesisXenotypeRecord.FromPawn(pawn, "player census", context.tick, context.tick);
                        if (sample != null) xenotypes.Add(sample.signature);
                        context.RememberXenotype(pawn, "player census");
                    }
                    break;
                case "XmtMutatedPawns":
                    if (BioUtility.HasMutations(pawn, false)) Add(pawn, context, 1f, "XmtMutated", "FleshShaperTarget");
                    break;
                case "BrainMutatedPawns":
                    if (pawn.HasBrainMutation()) Add(pawn, context, 1f, "BrainMutated", "FleshShaperTarget", "SubjugationTarget");
                    break;
                case "Horrors":
                    if (pawn.IsHorror()) Add(pawn, context, 1f, "XmtHorror", "FleshShaperTarget");
                    break;
                case "TamedCryptimorphs":
                    if (pawn.GetMorphComp()?.Tamed == true) Add(pawn, context, 1f, "TamedCryptimorph");
                    break;
                case "ObsessedPawns":
                    if (KnowledgeUtility.IsObsessed(pawn)) Add(pawn, context, 1f, "Obsessed", "WeakLink", "CollaboratorCandidate");
                    break;
                case "Armor":
                    if (pawn.RaceProps.Humanlike)
                    {
                        total += (Mathf.Max(0f, pawn.GetStatValue(StatDefOf.ArmorRating_Sharp))
                            + Mathf.Max(0f, pawn.GetStatValue(StatDefOf.ArmorRating_Blunt))) * 0.5f;
                    }
                    break;
                case "WeaponDamage":
                    total += NemesisThreatUtility.WeaponDirectThreat(pawn.equipment?.Primary);
                    break;
                case "AreaEffectAttrition":
                    float pawnAttrition = NemesisThreatUtility.WeaponAreaThreat(pawn.equipment?.Primary, out float pawnAttritionDamage, out _);
                    if (pawnAttritionDamage < 75f) total += pawnAttrition;
                    break;
                case "AreaEffectOverkill":
                    float pawnOverkill = NemesisThreatUtility.WeaponAreaThreat(pawn.equipment?.Primary, out float pawnOverkillDamage, out _);
                    if (pawnOverkillDamage >= 75f) total += pawnOverkill;
                    break;
                case "VolatilePawns":
                    if (NemesisThreatUtility.IsVolatilePawn(pawn, out float volatileThreat))
                        Add(pawn, context, volatileThreat, "VolatilePawn", "SabotageOpportunity");
                    break;
            }
        }

        public override NemesisObservationResult Complete(NemesisCensusContext context)
        {
            NemesisObservationResult result = new NemesisObservationResult
            {
                observation = def,
                value = def.metric == "XenotypeDiversity" ? xenotypes.Count : total,
                confidence = def.baseConfidence
            };
            result.contacts.AddRange(contacts);
            return result;
        }

        private void Add(Pawn pawn, NemesisCensusContext context, float amount, params string[] tags)
        {
            total += amount;
            if (!def.recordsSpatialContacts) return;
            NemesisSpatialContact contact = NemesisSpatialContact.FromThing(def, pawn, context.tick, def.baseConfidence, amount, tags);
            if (contact != null) contacts.Add(contact);
        }

        private static bool IsPlayerControlledHolder(IThingHolder holder)
        {
            HashSet<IThingHolder> visited = new HashSet<IThingHolder>();
            while (holder != null && visited.Add(holder))
            {
                if (holder is Thing thing && (thing.Faction == Faction.OfPlayer
                    || (thing is MinifiedThing minified && minified.InnerThing?.Faction == Faction.OfPlayer)))
                {
                    return true;
                }
                holder = holder.ParentHolder;
            }
            return false;
        }
    }

    public sealed class NemesisObservationWorker_LivingCryptimorphCorpses : NemesisObservationWorker
    {
        private readonly List<NemesisSpatialContact> contacts = new List<NemesisSpatialContact>();

        public override void Begin(NemesisCensusContext context)
        {
            contacts.Clear();
        }

        public override void VisitCorpse(StarbeastCorpse corpse, NemesisCensusContext context)
        {
            if (corpse?.InnerPawn == null || !corpse.IsViableLivingCorpseForObservation)
            {
                return;
            }

            NemesisSpatialContact contact = NemesisSpatialContact.FromThing(def, corpse, context.tick,
                def.baseConfidence, 1f, "DormantLivingCryptimorph", "AmbushOpportunity", "WakeDuringMission", "RecoveryTarget");
            if (contact != null)
            {
                contact.subjectId = corpse.InnerPawn.GetUniqueLoadID();
                contacts.Add(contact);
            }
        }

        public override NemesisObservationResult Complete(NemesisCensusContext context)
        {
            NemesisObservationResult result = new NemesisObservationResult
            {
                observation = def,
                value = contacts.Count,
                confidence = def.baseConfidence
            };
            result.contacts.AddRange(contacts);
            return result;
        }
    }

    public sealed class NemesisObservationWorker_XenotypePool : NemesisObservationWorker
    {
        private readonly HashSet<string> signatures = new HashSet<string>();

        public override void Begin(NemesisCensusContext context) => signatures.Clear();

        public override void VisitPawn(Pawn pawn, bool held, NemesisCensusContext context)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike || pawn.genes == null || XMTUtility.IsXenomorph(pawn)) return;
            context.RememberXenotype(pawn, "world census");
            NemesisXenotypeRecord record = NemesisXenotypeRecord.FromPawn(pawn, "world census", context.tick, context.tick);
            if (record != null) signatures.Add(record.signature);
        }

        public override NemesisObservationResult Complete(NemesisCensusContext context)
        {
            return new NemesisObservationResult { observation = def, value = signatures.Count, confidence = def.baseConfidence };
        }
    }

    public sealed class NemesisObservationWorker_BuildingMetric : NemesisObservationWorker
    {
        private float total;
        private readonly List<NemesisSpatialContact> contacts = new List<NemesisSpatialContact>();

        public override void Begin(NemesisCensusContext context)
        {
            total = 0f;
            contacts.Clear();
        }

        public override void VisitBuilding(Building building, NemesisCensusContext context)
        {
            if (building == null || building.Destroyed) return;
            switch (def.metric)
            {
                case "Walls":
                    if (building.def.IsWall) Add(building, context, 1f, "Wall", "Fortification");
                    break;
                case "HostBeds":
                    if (building is Building_Bed bed && !(building is CocoonBase))
                        Add(building, context, bed.SleepingSlotsCount, "HostBed", "SleepingLocation");
                    break;
                case "Turrets":
                    if (building.def.building?.IsTurret == true || building is Building_Turret) Add(building, context, 1f, "Turret");
                    break;
                case "DeployedTrapThreat":
                    if (building.def.building?.isTrap == true)
                    {
                        float threat = NemesisThreatUtility.TrapThreat(building);
                        float score = NemesisThreatUtility.TrapStrategicScore(building, threat);
                        bool catastrophic = NemesisThreatUtility.IsCatastrophicExplosive(building.def);
                        bool hasExplosive = building.def.GetCompProperties<CompProperties_Explosive>() != null;
                        bool harmful = hasExplosive ? NemesisThreatUtility.IsHarmfulExplosive(building.def) : threat > 1f;
                        Add(building, context, score, threat, "Trap", catastrophic ? "CatastrophicStaticDefense"
                            : harmful ? "StaticDefense" : "UtilityTrap");
                    }
                    break;
                case "CatastrophicStaticDefense":
                    if (building.def.building?.isTrap == true)
                    {
                        float threat = NemesisThreatUtility.TrapThreat(building);
                        if (NemesisThreatUtility.IsCatastrophicExplosive(building.def))
                            Add(building, context, Mathf.Clamp(threat / 500f, 1f, 10f), threat,
                                "Trap", "CatastrophicStaticDefense", "SacrificialDetonationCandidate");
                    }
                    break;
                case "AreaEffectAttrition":
                    float area = NemesisThreatUtility.WeaponAreaThreat(building.def.building?.turretGunDef, out float damage, out _);
                    if (area > 0f && damage < 75f) Add(building, context, area, "AreaEffect", "Attrition");
                    break;
                case "AreaEffectOverkill":
                    float overkill = NemesisThreatUtility.WeaponAreaThreat(building.def.building?.turretGunDef, out float overkillDamage, out _);
                    if (overkill > 0f && overkillDamage >= 75f) Add(building, context, overkill, "AreaEffect", "Overkill");
                    break;
                case "IndirectFirepower":
                    if (building.def.building?.IsMortar == true)
                    {
                        float indirect = Mathf.Max(1f, NemesisThreatUtility.WeaponDirectThreat(building.def.building.turretGunDef) / 20f);
                        Add(building, context, indirect, "IndirectFire", "Mortar");
                    }
                    break;
                case "ContainedCryptimorphs":
                    Pawn held = XMTContainmentUtility.HeldPawn(building);
                    if (held == null && building is Building_BioContainer container) held = container.ContainedThing as Pawn;
                    if (held != null && XMTUtility.IsXenomorph(held)) Add(building, context, 1f, "ContainedCryptimorph", "ExploitationTarget");
                    break;
            }
        }

        public override NemesisObservationResult Complete(NemesisCensusContext context)
        {
            NemesisObservationResult result = new NemesisObservationResult { observation = def, value = total, confidence = def.baseConfidence };
            result.contacts.AddRange(contacts);
            return result;
        }

        private void Add(Building building, NemesisCensusContext context, float amount, params string[] tags)
        {
            Add(building, context, amount, amount, tags);
        }

        private void Add(Building building, NemesisCensusContext context, float amount, float contactThreat, params string[] tags)
        {
            total += amount;
            if (!def.recordsSpatialContacts) return;
            NemesisSpatialContact contact = NemesisSpatialContact.FromThing(def, building, context.tick, def.baseConfidence, contactThreat, tags);
            if (contact != null) contacts.Add(contact);
        }
    }

    public sealed class NemesisObservationWorker_StoredItemMetric : NemesisObservationWorker
    {
        private float total;
        private readonly List<NemesisSpatialContact> contacts = new List<NemesisSpatialContact>();

        public override void Begin(NemesisCensusContext context)
        {
            total = 0f;
            contacts.Clear();
        }

        public override void VisitItem(Thing thing, NemesisCensusContext context)
        {
            float amount = 0f;
            string[] tags = null;
            switch (def.metric)
            {
                case "StoredWeaponDamage":
                    amount = NemesisThreatUtility.WeaponDirectThreat(thing) * thing.stackCount;
                    break;
                case "StoredArmor":
                    if (thing.def.IsApparel)
                        amount = (thing.GetStatValue(StatDefOf.ArmorRating_Sharp) + thing.GetStatValue(StatDefOf.ArmorRating_Blunt)) * 0.5f * thing.stackCount;
                    break;
                case "ExplosiveReserve":
                    amount = NemesisThreatUtility.StoredExplosiveThreat(thing, false);
                    tags = new[] { "ExplosiveReserve", "SabotageOpportunity" };
                    break;
                case "CatastrophicExplosives":
                    amount = NemesisThreatUtility.StoredExplosiveThreat(thing, true);
                    tags = new[] { "CatastrophicExplosive", "SabotageOpportunity" };
                    break;
            }
            if (amount <= 0f) return;
            total += amount;
            if (!def.recordsSpatialContacts) return;
            NemesisSpatialContact contact = NemesisSpatialContact.FromThing(def, thing, context.tick, def.baseConfidence, amount, tags);
            if (contact != null) contacts.Add(contact);
        }

        public override NemesisObservationResult Complete(NemesisCensusContext context)
        {
            NemesisObservationResult result = new NemesisObservationResult { observation = def, value = total, confidence = def.baseConfidence };
            result.contacts.AddRange(contacts);
            return result;
        }
    }

    internal static class NemesisThreatUtility
    {
        internal static float WeaponDirectThreat(Thing weapon)
        {
            if (weapon?.def?.IsWeapon != true) return 0f;
            return Mathf.Max(Mathf.Max(0f, weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS)), WeaponDirectThreat(weapon.def));
        }

        internal static float WeaponDirectThreat(ThingDef weaponDef)
        {
            float threat = 0f;
            if (weaponDef?.Verbs == null) return threat;
            foreach (VerbProperties verb in weaponDef.Verbs)
            {
                ProjectileProperties projectile = verb?.defaultProjectile?.projectile;
                if (projectile != null) threat = Mathf.Max(threat, projectile.GetDamageAmount(null, null));
            }
            return threat;
        }

        internal static float WeaponAreaThreat(Thing weapon, out float damage, out float radius)
        {
            return WeaponAreaThreat(weapon?.def, out damage, out radius);
        }

        internal static float WeaponAreaThreat(ThingDef weaponDef, out float damage, out float radius)
        {
            damage = 0f;
            radius = 0f;
            float best = 0f;
            if (weaponDef?.Verbs == null) return 0f;
            foreach (VerbProperties verb in weaponDef.Verbs)
            {
                ProjectileProperties projectile = verb?.defaultProjectile?.projectile;
                if (projectile == null || projectile.explosionRadius <= 0f) continue;
                float candidateDamage = projectile.GetDamageAmount(null, null);
                float candidate = candidateDamage * Mathf.Max(1f, projectile.explosionRadius) / 20f;
                if (candidate <= best) continue;
                best = candidate;
                damage = candidateDamage;
                radius = projectile.explosionRadius;
            }
            return best;
        }

        internal static float TrapThreat(Building building)
        {
            CompProperties_Explosive explosive = building.def.GetCompProperties<CompProperties_Explosive>();
            if (explosive != null) return ExplosiveDamage(explosive) * Mathf.Max(1f, explosive.explosiveRadius);
            return Mathf.Max(1f, building.GetStatValue(StatDefOf.TrapMeleeDamage));
        }

        internal static float StoredExplosiveThreat(Thing thing, bool catastrophicOnly)
        {
            CompProperties_Explosive explosive = thing?.def?.GetCompProperties<CompProperties_Explosive>();
            float damage = explosive != null ? ExplosiveDamage(explosive) : 0f;
            float radius = explosive?.explosiveRadius ?? 0f;
            float baseThreat = damage * Mathf.Max(1f, radius);
            bool catastrophic = damage >= 100f || baseThreat >= 300f
                || thing?.def?.defName?.IndexOf("antigrain", StringComparison.OrdinalIgnoreCase) >= 0;
            if (catastrophicOnly)
            {
                if (!catastrophic) return 0f;
                float perItem = Mathf.Clamp(baseThreat / 500f, 1f, 10f);
                float countFactor = 1f + Mathf.Log10(Mathf.Max(1, thing.stackCount));
                return Mathf.Min(20f, perItem * countFactor);
            }
            return baseThreat > 0f ? Mathf.Min(10f, baseThreat * thing.stackCount / 200f) : 0f;
        }

        internal static bool IsCatastrophicExplosive(ThingDef thingDef)
        {
            CompProperties_Explosive explosive = thingDef?.GetCompProperties<CompProperties_Explosive>();
            if (explosive == null) return false;
            float damage = ExplosiveDamage(explosive);
            float threat = damage * Mathf.Max(1f, explosive.explosiveRadius);
            return damage >= 100f || threat >= 300f
                || thingDef.defName?.IndexOf("antigrain", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsHarmfulExplosive(ThingDef thingDef)
        {
            CompProperties_Explosive explosive = thingDef?.GetCompProperties<CompProperties_Explosive>();
            return explosive?.explosiveDamageType?.harmsHealth == true;
        }

        internal static float TrapStrategicScore(Building building, float rawThreat)
        {
            CompProperties_Explosive explosive = building?.def?.GetCompProperties<CompProperties_Explosive>();
            if (explosive != null && explosive.explosiveDamageType?.harmsHealth != true)
            {
                return 0f;
            }
            if (explosive == null && rawThreat <= 1f)
            {
                return 0f;
            }
            return Mathf.Clamp(Mathf.Log10(1f + Mathf.Max(0f, rawThreat) / 10f), 0f, 3f);
        }

        internal static bool IsVolatilePawn(Pawn pawn, out float threat)
        {
            CompProperties_Explosive explosive = pawn?.def?.GetCompProperties<CompProperties_Explosive>();
            if (explosive != null)
            {
                threat = Mathf.Max(0.25f, ExplosiveDamage(explosive) * Mathf.Max(1f, explosive.explosiveRadius) / 200f);
                return true;
            }
            string deathWorker = pawn?.RaceProps?.DeathActionWorker?.GetType().Name;
            bool explosiveDeathAction = !deathWorker.NullOrEmpty()
                && (deathWorker.IndexOf("explos", StringComparison.OrdinalIgnoreCase) >= 0
                    || deathWorker.IndexOf("boom", StringComparison.OrdinalIgnoreCase) >= 0);
            bool explosiveGene = pawn?.genes?.GenesListForReading?.Exists(gene =>
                gene?.def?.defName?.IndexOf("boom", StringComparison.OrdinalIgnoreCase) >= 0
                || gene?.def?.defName?.IndexOf("explode", StringComparison.OrdinalIgnoreCase) >= 0) == true;
            threat = explosiveGene || explosiveDeathAction ? 0.5f : 0f;
            return explosiveGene || explosiveDeathAction;
        }

        private static float ExplosiveDamage(CompProperties_Explosive explosive)
        {
            return Mathf.Max(1f, explosive.damageAmountBase > 0
                ? explosive.damageAmountBase
                : explosive.explosiveDamageType?.defaultDamage ?? 10f);
        }
    }
}
