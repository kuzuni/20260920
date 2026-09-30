using System.Collections.Generic;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        readonly List<Actor> retiredHitKeys = new List<Actor>(128);

        void PruneRetiredHits(Dictionary<Actor, float> hits, ref int nextCleanup)
        {
            if (hits.Count < nextCleanup) return;
            retiredHitKeys.Clear();
            foreach (var pair in hits)
                if (pair.Key.returnedToPool) retiredHitKeys.Add(pair.Key);
            foreach (var retired in retiredHitKeys) hits.Remove(retired);
            retiredHitKeys.Clear();
            // A pooled enemy receives a new Actor identity. Retired keys can never
            // become targets again; live targets retain their exact hit deadlines.
            // Grow the watermark when many targets are alive, avoiding a scan every tick.
            nextCleanup = System.Math.Max(128, hits.Count * 2);
        }
    }
}
