using System;
using System.Globalization;
using System.Numerics;

namespace DoodleIdle
{
    // Floating point precision (~15 significant digits), with an arbitrary-size decimal exponent.
    // Gameplay must keep this type through arithmetic; conversions are only for legacy APIs/geometry.
    public readonly struct GameNumber : IComparable<GameNumber>, IEquatable<GameNumber>
    {
        public readonly double Mantissa;
        public readonly BigInteger Exponent;
        public GameNumber(double value) : this(value, BigInteger.Zero) { }
        public GameNumber(double mantissa, BigInteger exponent)
        {
            if (double.IsNaN(mantissa) || double.IsInfinity(mantissa)) throw new ArgumentOutOfRangeException(nameof(mantissa), "A game number must be finite.");
            if (mantissa == 0) { Mantissa = 0; Exponent = 0; return; }
            int shift = (int)Math.Floor(Math.Log10(Math.Abs(mantissa)));
            // Split subnormal powers so 10^-324 cannot underflow before division.
            if (shift < -300) { mantissa *= 1e300; exponent -= 300; shift += 300; }
            mantissa /= Math.Pow(10, shift); exponent += shift;
            if (Math.Abs(mantissa) >= 10) { mantissa /= 10; exponent++; }
            if (Math.Abs(mantissa) < 1) { mantissa *= 10; exponent--; }
            Mantissa = mantissa; Exponent = exponent;
        }
        public static implicit operator GameNumber(double value) => new GameNumber(value);
        public static explicit operator double(GameNumber value) => value.ToDouble();
        public static explicit operator float(GameNumber value) => (float)Math.Max(-float.MaxValue, Math.Min(float.MaxValue, value.ToDouble()));
        public static explicit operator long(GameNumber value)
        {
            double n = value.ToDouble();
            if (n >= long.MaxValue) return long.MaxValue;
            if (n <= long.MinValue) return long.MinValue;
            double nearest = Math.Round(n);
            if (Math.Abs(n - nearest) <= Math.Abs(n) * 8.881784197001252e-16) n = nearest;
            return (long)n;
        }
        public static explicit operator int(GameNumber value) => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)value));
        public double ToDouble()
        {
            if (Mantissa == 0 || Exponent < -324) return 0;
            if (Exponent > 308) return Math.Sign(Mantissa) * double.MaxValue;
            int exponent = (int)Exponent;
            double result = exponent < -300 ? Mantissa * 1e-300 * Math.Pow(10, exponent + 300) : Mantissa * Math.Pow(10, exponent);
            return double.IsInfinity(result) ? Math.Sign(Mantissa) * double.MaxValue : result;
        }
        public static GameNumber operator +(GameNumber a, GameNumber b)
        {
            if (a.Mantissa == 0) return b;
            if (b.Mantissa == 0) return a;
            if (a.Exponent < b.Exponent) { var swap = a; a = b; b = swap; }
            var gap = a.Exponent - b.Exponent;
            return gap > 17 ? a : new GameNumber(a.Mantissa + b.Mantissa * Math.Pow(10, -(int)gap), a.Exponent);
        }
        public static GameNumber operator -(GameNumber a) => new GameNumber(-a.Mantissa, a.Exponent);
        public static GameNumber operator -(GameNumber a, GameNumber b) => a + -b;
        public static GameNumber operator *(GameNumber a, GameNumber b) => new GameNumber(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);
        public static GameNumber operator /(GameNumber a, GameNumber b)
        {
            if (b.Mantissa == 0) throw new DivideByZeroException();
            return new GameNumber(a.Mantissa / b.Mantissa, a.Exponent - b.Exponent);
        }
        public static GameNumber Pow(GameNumber value, int power)
        {
            if (power == 0) return 1;
            if (value.Mantissa == 0) { if (power < 0) throw new DivideByZeroException(); return 0; }
            double log = Math.Log10(Math.Abs(value.Mantissa)) * power;
            double whole = Math.Floor(log);
            return new GameNumber((value.Mantissa < 0 && power % 2 != 0 ? -1 : 1) * Math.Pow(10, log - whole), value.Exponent * power + new BigInteger(whole));
        }
        public static GameNumber Min(GameNumber a, GameNumber b) => a <= b ? a : b;
        public static GameNumber Max(GameNumber a, GameNumber b) => a >= b ? a : b;
        public static GameNumber Clamp(GameNumber n, GameNumber min, GameNumber max) => Min(Max(n, min), max);
        public static GameNumber Round(GameNumber value) => value.Exponent < 15 ? new GameNumber(Math.Round(value.ToDouble())) : value;
        public static GameNumber Ceiling(GameNumber value) => value.Exponent < 15 ? new GameNumber(Math.Ceiling(value.ToDouble())) : value;
        public int CompareTo(GameNumber other)
        {
            int sign = Math.Sign(Mantissa), otherSign = Math.Sign(other.Mantissa);
            if (sign != otherSign) return sign.CompareTo(otherSign);
            if (sign == 0) return 0;
            int exponent = Exponent.CompareTo(other.Exponent);
            return exponent == 0 ? Mantissa.CompareTo(other.Mantissa) : exponent * sign;
        }
        public bool Equals(GameNumber other) => Mantissa.Equals(other.Mantissa) && Exponent.Equals(other.Exponent);
        public override bool Equals(object other) => other is GameNumber value && Equals(value);
        public override int GetHashCode() => Mantissa.GetHashCode() ^ Exponent.GetHashCode();
        public static bool operator ==(GameNumber a, GameNumber b) => a.Equals(b);
        public static bool operator !=(GameNumber a, GameNumber b) => !a.Equals(b);
        public static bool operator <(GameNumber a, GameNumber b) => a.CompareTo(b) < 0;
        public static bool operator >(GameNumber a, GameNumber b) => a.CompareTo(b) > 0;
        public static bool operator <=(GameNumber a, GameNumber b) => a.CompareTo(b) <= 0;
        public static bool operator >=(GameNumber a, GameNumber b) => a.CompareTo(b) >= 0;
        public override string ToString() => Mantissa.ToString("R", CultureInfo.InvariantCulture) + "e" + Exponent.ToString(CultureInfo.InvariantCulture);
        public static bool TryParse(string text, out GameNumber value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            int split = text.IndexOfAny(new[] { 'e', 'E' });
            if (!double.TryParse(split < 0 ? text : text.Substring(0, split), NumberStyles.Float, CultureInfo.InvariantCulture, out double mantissa) || double.IsNaN(mantissa) || double.IsInfinity(mantissa)) return false;
            BigInteger exponent = 0;
            if (split >= 0 && !BigInteger.TryParse(text.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out exponent)) return false;
            value = new GameNumber(mantissa, exponent); return true;
        }
    }
}
