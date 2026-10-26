using System.Collections.Generic;

namespace Xenomorphtype
{
    // Short-lived, bounded history shared by replacement jobs, never by different pawns.
    internal sealed class TraversalRetryBudget<TKey>
    {
        private const int MaximumEntries = 8;
        private const int FailureThreshold = 3;
        private const int HistoryTicks = 600;
        private const int RetryDelayTicks = 120;
        private sealed class Entry
        {
            public TKey Key;
            public int Failures;
            public int Expires;
            public int BlockedUntil;
        }

        private readonly List<Entry> entries = new List<Entry>();
        internal int Count => entries.Count;

        internal bool IsBlocked(TKey key, int tick)
        {
            Prune(tick);
            return entries.Exists(entry => EqualityComparer<TKey>.Default.Equals(entry.Key, key) && tick < entry.BlockedUntil);
        }

        internal bool RecordFailure(TKey key, int tick)
        {
            Prune(tick);
            Entry entry = entries.Find(candidate => EqualityComparer<TKey>.Default.Equals(candidate.Key, key));
            if (entry == null)
            {
                if (entries.Count == MaximumEntries)
                    entries.RemoveAt(0);
                entries.Add(entry = new Entry { Key = key });
            }
            entry.Expires = tick + HistoryTicks;
            if (++entry.Failures < FailureThreshold)
                return false;
            entry.BlockedUntil = tick + RetryDelayTicks;
            return true;
        }

        internal void Reset() => entries.Clear();
        private void Prune(int tick) => entries.RemoveAll(entry => tick >= entry.Expires);
    }
}
