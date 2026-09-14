using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VefAbility = VEF.Abilities.Ability;

namespace Xenomorphtype
{
    public static class NemesisEvidenceReporter
    {
        private sealed class PendingDeath
        {
            public Pawn victim;
            public DamageInfo? damage;
        }

        private static readonly Dictionary<int, PendingDeath> PendingDeaths = new Dictionary<int, PendingDeath>();
        private static GameComponent_Nemesis Component => Current.Game?.GetComponent<GameComponent_Nemesis>();

        public static void ReportCryptimorphInjury(Pawn victim, DamageInfo damage, float amount)
        {
            if (victim == null || victim.Faction == Faction.OfPlayer || amount <= 0f)
            {
                return;
            }

            float normalized = Mathf.Clamp(amount / Mathf.Max(20f, victim.MaxHitPoints), 0f, 1f);
            Record("XMT_Nemesis_CryptimorphInjured", normalized, damage.Instigator, victim, damage.Def,
                "rawDamage=" + amount.ToString("0.###") + ",normalized=" + normalized.ToString("0.###")
                + ",weapon=" + (damage.Weapon?.defName ?? "none"));
        }

        public static void QueueCryptimorphDeathResolution(Pawn victim, DamageInfo? damage)
        {
            if (victim == null || victim.Faction == Faction.OfPlayer)
            {
                return;
            }

            PendingDeaths[victim.thingIDNumber] = new PendingDeath { victim = victim, damage = damage };
            if (victim.Corpse is StarbeastCorpse corpse)
            {
                ResolveCryptimorphCorpse(corpse);
            }
        }

        public static void ResolveCryptimorphCorpse(StarbeastCorpse corpse)
        {
            Pawn victim = corpse?.InnerPawn;
            if (victim == null || !PendingDeaths.TryGetValue(victim.thingIDNumber, out PendingDeath pending)
                || pending.victim != victim)
            {
                return;
            }

            PendingDeaths.Remove(victim.thingIDNumber);
            corpse.ResolveInitialNemesisDeath(pending.damage);
        }

        public static void ReportCryptimorphPlayedDead(Pawn victim, DamageInfo? damage)
        {
            if (victim == null || victim.Faction == Faction.OfPlayer)
            {
                return;
            }

            Record("XMT_Nemesis_CryptimorphPlayedDead", 1f, damage?.Instigator, victim, damage?.Def,
                "weapon=" + (damage?.Weapon?.defName ?? "none") + ",survived=true");
        }

        public static void ReportCryptimorphConfirmedDeath(Pawn victim, DamageInfo? damage, string reason)
        {
            if (victim == null || victim.Faction == Faction.OfPlayer)
            {
                return;
            }

            Thing instigator = damage?.Instigator;
            DamageDef source = damage?.Def;
            bool usefulDeath = victim.GetAcidBloodComp() != null || IsExplosiveDeath(victim);
            Record("XMT_Nemesis_CryptimorphKilled", usefulDeath ? 0.25f : 1f, instigator, victim, source,
                "weapon=" + (damage?.Weapon?.defName ?? "none")
                + ",acid=" + (victim.GetAcidBloodComp() != null)
                + ",victimExplodesOnDeath=" + IsExplosiveDeath(victim)
                + ",reason=" + (reason ?? "unknown"));
            Component?.NotifyConfirmedQueenDeath(victim);
        }

        public static void ReportButchery(Pawn corpsePawn, Pawn butcher)
        {
            if (corpsePawn == null || butcher?.Faction != Faction.OfPlayer)
            {
                return;
            }

            Record("XMT_Nemesis_CryptimorphButchered", 1f, butcher, corpsePawn, corpsePawn.def);
        }

        public static void ReportHarvest(Pawn target, Pawn extractor, Def source, string detail)
        {
            if (target == null || extractor?.Faction != Faction.OfPlayer)
            {
                return;
            }

            Record("XMT_Nemesis_CryptimorphHarvested", 1f, extractor, target, source, detail);
        }

        public static void ReportAbduction(Pawn victim, Pawn abductor)
        {
            if (victim == null || abductor == null)
            {
                return;
            }

            Record("XMT_Nemesis_PawnAbducted", 1f, abductor, victim, victim.def,
                "xenotype=" + (victim.genes?.Xenotype?.defName ?? victim.genes?.xenotypeName ?? "none"));
            Component?.QueueAbductedXenotype(victim);
        }

        public static void ReportVanillaPsycast(Psycast psycast)
        {
            if (psycast?.pawn?.Faction != Faction.OfPlayer)
            {
                return;
            }

            string detail = "framework=Vanilla,psylink=" + psycast.pawn.GetPsylinkLevel();
            Record("XMT_Nemesis_PsycastCommitted", 1f, psycast.pawn, null, psycast.def, detail);
        }

        public static void ReportVpePsycast(VefAbility ability, NemesisVpePsycastDescriptor descriptor,
            float psyfocusBefore, float entropyBefore, string outcome)
        {
            Pawn caster = ability?.pawn;
            if (caster?.Faction != Faction.OfPlayer || descriptor == null)
            {
                return;
            }

            float psyfocusAfter = caster.psychicEntropy?.CurrentPsyfocus ?? psyfocusBefore;
            float entropyAfter = caster.psychicEntropy?.EntropyValue ?? entropyBefore;
            string detail = "framework=VPE,path=" + (descriptor.pathDefName ?? "none")
                + ",level=" + descriptor.level
                + ",declaredPsyfocus=" + descriptor.psyfocusCost.ToString("0.###")
                + ",declaredEntropy=" + descriptor.entropyGain.ToString("0.###")
                + ",focusDelta=" + (psyfocusBefore - psyfocusAfter).ToString("0.###")
                + ",entropyDelta=" + (entropyAfter - entropyBefore).ToString("0.###")
                + ",outcome=" + outcome;

            float magnitude = 1f + 0.2f * System.Math.Max(0, descriptor.level - 1);
            Record("XMT_Nemesis_PsycastCommitted", magnitude, caster, null, ability.def, detail);
        }

        private static void Record(string defName, float amount, Thing actor, Thing subject, Def source, string detail = null)
        {
            NemesisEvidenceDef def = DefDatabase<NemesisEvidenceDef>.GetNamedSilentFail(defName);
            Component?.RecordEvidence(def, amount, actor, subject, source, detail);
        }

        private static bool IsExplosiveDeath(Pawn pawn)
        {
            return NemesisThreatUtility.IsVolatilePawn(pawn, out _);
        }
    }
}
