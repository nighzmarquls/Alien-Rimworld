using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public static class TissueKnowledge
    {
        private sealed class RecognitionCache
        {
            public readonly System.WeakReference<Game> game = new System.WeakReference<Game>(null);
            public int tick = -1;
            public bool recognized;
        }

        private static readonly ConditionalWeakTable<MutationTissueExtension, RecognitionCache> recognition = new ConditionalWeakTable<MutationTissueExtension, RecognitionCache>();
        private static readonly MutationTissueExtension defaults = new MutationTissueExtension { useDefaultRecognition = true };

        public static bool Unlocked => (DebugSettings.godMode && TissueAssessmentDef.Current.allowGodMode) || Recognizes(defaults);

        public static bool Recognizes(MutationTissueExtension data)
        {
            if (!data.useDefaultRecognition && data.revealResearchProjects.Count == 0 && data.revealQueenAdvancements.Count == 0) return false;
            var cache = recognition.GetValue(data, _ => new RecognitionCache());
            Game game = Current.Game;
            int tick = game == null ? 0 : Find.TickManager?.TicksGame ?? 0;
            cache.game.TryGetTarget(out Game previous);
            if (previous != game || cache.tick != tick)
            {
                cache.game.SetTarget(game);
                cache.tick = tick;
                var settings = data.useDefaultRecognition ? TissueAssessmentDef.Current : null;
                cache.recognized = data.revealResearchProjects.Concat(settings?.researchUnlocks ?? Enumerable.Empty<ResearchProjectDef>()).Any(r => r != null && r.IsFinished) ||
                    data.revealQueenAdvancements.Concat(settings?.queenUnlocks ?? Enumerable.Empty<RoyalEvolutionDef>()).Any(e => e != null && XMTUtility.HasQueenWithEvolution(e));
            }
            return cache.recognized;
        }
    }
}
