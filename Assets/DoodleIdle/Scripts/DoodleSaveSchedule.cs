using System;

namespace DoodleIdle
{
    /// <summary>Monotonic deadlines; changes to gameplay state do not move a deadline.</summary>
    public sealed class DoodleSaveSchedule
    {
        public const double LocalInterval = 1;
        public const double ServerInterval = 300;
        public const double RetryInterval = 5;
        readonly double interval, retryInterval;
        public double NextAttempt { get; private set; }
        public bool Running { get; private set; }

        public DoodleSaveSchedule(double interval, double retryInterval)
        {
            if (interval <= 0 || retryInterval <= 0) throw new ArgumentOutOfRangeException();
            this.interval = interval; this.retryInterval = retryInterval;
        }
        public void Reset(double now) { Running = false; NextAttempt = now + interval; }
        public bool TryBegin(double now, bool immediate = false)
        {
            if (Running || (!immediate && now < NextAttempt)) return false;
            Running = true; return true;
        }
        public void Complete(double now, bool success)
        {
            Running = false; NextAttempt = now + (success ? interval : retryInterval);
        }
    }
}
