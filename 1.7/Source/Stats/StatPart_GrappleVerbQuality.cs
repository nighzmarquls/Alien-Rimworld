using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public class StatPart_GrappleVerbQuality : StatPart
    {
        private const int CacheTicks = 250;
        private const float MinFactor = 0.75f;
        private const float MaxFactor = 1.75f;

        private static readonly Dictionary<int, CachedVerbFactor> Cache = new Dictionary<int, CachedVerbFactor>();

        public override void TransformValue(StatRequest req, ref float val)
        {
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null)
            {
                return;
            }

            val *= GetVerbFactor(pawn);
        }

        public override string ExplanationPart(StatRequest req)
        {
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null)
            {
                return null;
            }

            CachedVerbFactor factor = GetCachedVerbFactor(pawn);
            return "Available melee attacks: x" + factor.Factor.ToStringPercent() + " (" + factor.VerbCount + " usable, best score " + factor.BestScore.ToString("0.##") + ")";
        }

        public static float GetVerbFactor(Pawn pawn)
        {
            return GetCachedVerbFactor(pawn).Factor;
        }

        private static CachedVerbFactor GetCachedVerbFactor(Pawn pawn)
        {
            if (pawn == null)
            {
                return CachedVerbFactor.Default;
            }

            int key = pawn.thingIDNumber;
            int signature = CacheSignature(pawn);
            int tick = Find.TickManager?.TicksGame ?? 0;

            if (Cache.TryGetValue(key, out CachedVerbFactor cached) && cached.Signature == signature && tick < cached.ExpireTick)
            {
                return cached;
            }

            CachedVerbFactor updated = CalculateVerbFactor(pawn);
            updated.Signature = signature;
            updated.ExpireTick = tick + CacheTicks;
            Cache[key] = updated;
            return updated;
        }

        private static int CacheSignature(Pawn pawn)
        {
            unchecked
            {
                int hash = pawn.Dead ? 1 : 0;
                hash = (hash * 397) ^ (pawn.Downed ? 1 : 0);
                hash = (hash * 397) ^ Gen.HashCombineInt(0, pawn.equipment?.Primary?.thingIDNumber ?? 0);
                hash = (hash * 397) ^ (pawn.health?.hediffSet?.hediffs?.Count ?? 0);
                hash = (hash * 397) ^ ((int)((pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f) * 1000f));
                return hash;
            }
        }

        private static CachedVerbFactor CalculateVerbFactor(Pawn pawn)
        {
            if (pawn.Dead || pawn.meleeVerbs == null)
            {
                return CachedVerbFactor.Default;
            }

            List<VerbEntry> verbs = pawn.meleeVerbs.GetUpdatedAvailableVerbsList(false);
            if (verbs.NullOrEmpty())
            {
                return new CachedVerbFactor(0, 0f, MinFactor);
            }

            float bestScore = 0f;
            float extraScore = 0f;
            int usableCount = 0;

            foreach (VerbEntry entry in verbs)
            {
                Verb verb = entry.verb;
                if (verb == null || !verb.IsMeleeAttack || !verb.Available())
                {
                    continue;
                }

                float score = VerbScore(verb);
                if (score <= 0f)
                {
                    continue;
                }

                usableCount++;
                if (score > bestScore)
                {
                    extraScore += bestScore * 0.2f;
                    bestScore = score;
                }
                else
                {
                    extraScore += score * 0.2f;
                }
            }

            if (usableCount == 0)
            {
                return new CachedVerbFactor(0, 0f, MinFactor);
            }

            float combinedScore = bestScore + extraScore;
            float factor = SimpleCurveFactor(combinedScore);
            return new CachedVerbFactor(usableCount, bestScore, factor);
        }

        private static float VerbScore(Verb verb)
        {
            Tool tool = verb.tool;
            if (tool == null)
            {
                return 0f;
            }

            float cooldown = tool.cooldownTime > 0f ? tool.cooldownTime : 1f;
            float chance = tool.chanceFactor > 0f ? tool.chanceFactor : 1f;
            return (tool.power / cooldown) * chance;
        }

        private static float SimpleCurveFactor(float score)
        {
            if (score <= 0f)
            {
                return MinFactor;
            }

            if (score <= 2f)
            {
                return GenMath.LerpDouble(0f, 2f, MinFactor, 1f, score);
            }

            if (score <= 8f)
            {
                return GenMath.LerpDouble(2f, 8f, 1f, 1.35f, score);
            }

            return GenMath.LerpDoubleClamped(8f, 20f, 1.35f, MaxFactor, score);
        }

        private struct CachedVerbFactor
        {
            public static readonly CachedVerbFactor Default = new CachedVerbFactor(0, 0f, 1f);

            public int VerbCount;
            public float BestScore;
            public float Factor;
            public int Signature;
            public int ExpireTick;

            public CachedVerbFactor(int verbCount, float bestScore, float factor)
            {
                VerbCount = verbCount;
                BestScore = bestScore;
                Factor = factor;
                Signature = 0;
                ExpireTick = 0;
            }
        }
    }
}
