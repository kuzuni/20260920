using System;
using System.Collections.Generic;

namespace DoodleIdle
{
    [Serializable]
    public sealed class DoodleGrowthPoint
    {
        public int position;
        public double factor = 1;
    }

    [Serializable]
    public sealed class DoodleGrowthCurve
    {
        public DoodleGrowthPoint[] points = Array.Empty<DoodleGrowthPoint>();

        public double Evaluate(int position, int origin)
        {
            int left = origin, right = int.MaxValue;
            double a = 1, b = 1;
            bool hasRight = false;
            foreach (var point in points ?? Array.Empty<DoodleGrowthPoint>()) {
                if (point == null || point.position <= origin) continue;
                if (point.position == position) return SafeFactor(point.factor);
                if (point.position < position && point.position > left) { left = point.position; a = SafeFactor(point.factor); }
                if (point.position > position && (!hasRight || point.position < right)) { right = point.position; b = SafeFactor(point.factor); hasRight = true; }
            }
            return !hasRight || position <= origin ? a : a + (b - a) * ((double)position - left) / ((double)right - left);
        }

        static double SafeFactor(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 1 : Math.Max(0, Math.Min(1e100, value));

        public void SetPoint(int position, double factor, int origin)
        {
            if (position <= origin) return;
            var entries = new List<DoodleGrowthPoint>();
            foreach (var point in points ?? Array.Empty<DoodleGrowthPoint>())
                if (point != null && point.position > origin && point.position != position)
                    entries.Add(new DoodleGrowthPoint { position = point.position, factor = SafeFactor(point.factor) });
            entries.Add(new DoodleGrowthPoint { position = position, factor = SafeFactor(factor) });
            entries.Sort((a, b) => a.position.CompareTo(b.position));
            points = entries.ToArray();
        }

        public void RemovePoint(int position) => points = Array.FindAll(points ?? Array.Empty<DoodleGrowthPoint>(), p => p != null && p.position != position);

        public DoodleGrowthCurve Copy()
        {
            var copy = new DoodleGrowthCurve();
            foreach (var point in points ?? Array.Empty<DoodleGrowthPoint>())
                if (point != null) copy.SetPoint(point.position, point.factor, -1);
            return copy;
        }

        // Handle zero before exponentiation; keep finite, ordinary prices exact before rounding.
        public static double Exponential(double start, double growth, int steps, double factor = 1)
        {
            if (start <= 0 || factor <= 0) return 0;
            if (double.IsNaN(start) || double.IsNaN(growth) || double.IsNaN(factor)) return 0;
            double baseline = Math.Min(1e30, start * Math.Pow(1 + Math.Max(0, growth), Math.Max(0, steps)));
            return Math.Min(1e30, baseline * factor);
        }
    }
}
