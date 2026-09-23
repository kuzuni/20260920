using System;
using System.Collections.Generic;

namespace DoodleIdle
{
    public enum DoodleCurveInterpolation { Linear, Smooth, Manual }

    [Serializable]
    public sealed class DoodleGrowthPoint
    {
        public int position;
        public double factor = 1;
        public double inTangent, outTangent;

        public DoodleGrowthPoint Copy() => new DoodleGrowthPoint { position = position, factor = factor, inTangent = inTangent, outTangent = outTangent };
    }

    [Serializable]
    public sealed class DoodleGrowthCurve
    {
        public DoodleGrowthPoint[] points = Array.Empty<DoodleGrowthPoint>();
        public DoodleCurveInterpolation interpolation;
        public double originOutTangent;

        public double Evaluate(double position, int origin)
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
            if (!hasRight || position <= origin) return a;
            double width = (double)right - left, t = (position - left) / width;
            if (interpolation == DoodleCurveInterpolation.Linear) return a + (b - a) * t;
            double m0 = GetTangent(left, origin, false), m1 = GetTangent(right, origin, true);
            double t2 = t * t, t3 = t2 * t;
            return SafeFactor((2*t3-3*t2+1)*a + (t3-2*t2+t)*width*m0 + (-2*t3+3*t2)*b + (t3-t2)*width*m1);
        }

        public DoodleGrowthPoint FindPoint(int position) => Array.Find(points ?? Array.Empty<DoodleGrowthPoint>(), p => p != null && p.position == position);

        public int Neighbor(int position, int origin, bool incoming)
        {
            int result = incoming && position > origin ? origin : -1;
            foreach (var point in points ?? Array.Empty<DoodleGrowthPoint>()) {
                if (point == null || point.position <= origin) continue;
                if (incoming && point.position < position && point.position > result) result = point.position;
                if (!incoming && point.position > position && (result < 0 || point.position < result)) result = point.position;
            }
            return result;
        }
        double KnotValue(int position, int origin) => position == origin ? 1 : SafeFactor(FindPoint(position)?.factor ?? 1);
        static double SafeTangent(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(-1e100, Math.Min(1e100, value));

        public double GetTangent(int position, int origin, bool incoming)
        {
            if (interpolation == DoodleCurveInterpolation.Manual)
                return SafeTangent(position == origin ? originOutTangent : incoming ? FindPoint(position)?.inTangent ?? 0 : FindPoint(position)?.outTangent ?? 0);
            int before = Neighbor(position, origin, true), after = Neighbor(position, origin, false);
            double value = KnotValue(position, origin);
            double h0 = before >= 0 ? (double)position - before : 0, h1 = after >= 0 ? (double)after - position : 0;
            double d0 = h0 > 0 ? (value - KnotValue(before, origin)) / h0 : 0;
            double d1 = h1 > 0 ? (KnotValue(after, origin) - value) / h1 : 0;
            if (interpolation == DoodleCurveInterpolation.Linear) return incoming ? d0 : d1;
            if (before < 0) return d1;
            if (after < 0) return d0;
            // Shape-preserving Hermite tangents avoid bumps/negative rewards in automatic mode.
            if (d0 == 0 || d1 == 0 || Math.Sign(d0) != Math.Sign(d1)) return 0;
            double w0 = 2*h1+h0, w1 = h1+2*h0;
            return SafeTangent((w0+w1)/(w0/d0+w1/d1));
        }

        public void SetInterpolation(DoodleCurveInterpolation mode, int origin)
        {
            if (mode == DoodleCurveInterpolation.Manual && interpolation != mode) {
                originOutTangent = GetTangent(origin, origin, false);
                foreach (var point in points ?? Array.Empty<DoodleGrowthPoint>()) if (point != null) {
                    point.inTangent = GetTangent(point.position, origin, true);
                    point.outTangent = GetTangent(point.position, origin, false);
                }
            }
            interpolation = mode;
        }

        public void SetTangent(int position, int origin, bool incoming, double tangent)
        {
            SetInterpolation(DoodleCurveInterpolation.Manual, origin);
            if (position == origin && !incoming) originOutTangent = SafeTangent(tangent);
            var point = FindPoint(position);
            if (point != null) { if (incoming) point.inTangent = SafeTangent(tangent); else point.outTangent = SafeTangent(tangent); }
        }

        static double SafeFactor(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 1 : Math.Max(0, Math.Min(1e100, value));

        public void SetPoint(int position, double factor, int origin)
        {
            if (position <= origin) return;
            var added = FindPoint(position)?.Copy() ?? new DoodleGrowthPoint { position = position };
            added.factor = SafeFactor(factor);
            var entries = new List<DoodleGrowthPoint>();
            foreach (var point in points ?? Array.Empty<DoodleGrowthPoint>())
                if (point != null && point.position > origin && point.position != position)
                    entries.Add(point.Copy());
            entries.Add(added);
            entries.Sort((a, b) => a.position.CompareTo(b.position));
            points = entries.ToArray();
        }

        public void RemovePoint(int position) => points = Array.FindAll(points ?? Array.Empty<DoodleGrowthPoint>(), p => p != null && p.position != position);

        public void MovePoint(int oldPosition, int position, double factor, int origin)
        {
            if (position <= origin) return;
            var previous = FindPoint(oldPosition)?.Copy();
            RemovePoint(oldPosition); SetPoint(position, factor, origin);
            if (previous != null) { var moved = FindPoint(position); moved.inTangent = previous.inTangent; moved.outTangent = previous.outTangent; }
        }

        public DoodleGrowthCurve Copy()
        {
            var copy = new DoodleGrowthCurve { interpolation = interpolation, originOutTangent = SafeTangent(originOutTangent) };
            var entries = new List<DoodleGrowthPoint>();
            foreach (var point in points ?? Array.Empty<DoodleGrowthPoint>()) if (point != null) {
                var entry = point.Copy(); entry.factor = SafeFactor(entry.factor);
                entry.inTangent = SafeTangent(entry.inTangent); entry.outTangent = SafeTangent(entry.outTangent);
                entries.Add(entry);
            }
            entries.Sort((a,b) => a.position.CompareTo(b.position));
            copy.points = entries.ToArray();
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
