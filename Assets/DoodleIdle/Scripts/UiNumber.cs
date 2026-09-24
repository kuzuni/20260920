using System;
using System.Globalization;

namespace DoodleIdle
{
    /// <summary>Display only: a = 1,000, b = 1,000,000, then z, aa, ab…</summary>
    public static class UiNumber
    {
        public static string Format(GameNumber value, int decimals = 1)
        {
            if (value.Exponent < 15) return Format((double)value, decimals);
            decimals = Math.Max(0, Math.Min(6, decimals));
            var unit = value.Exponent / 3;
            double scaled = Math.Round(value.Mantissa * Math.Pow(10, (int)(value.Exponent % 3)), decimals, MidpointRounding.AwayFromZero);
            if (Math.Abs(scaled) >= 1000) { scaled /= 1000; unit++; }
            string format = decimals == 0 ? "0" : "0." + new string('#', decimals);
            // Keep labels compact even when the exponent itself has thousands of digits.
            if (unit > 1000000) return value.Mantissa.ToString(format, CultureInfo.InvariantCulture) + "e" + value.Exponent.ToString(CultureInfo.InvariantCulture);
            string suffix = "";
            for (int index = (int)unit; index > 0; index = (index - 1) / 26) suffix = (char)('a' + (index - 1) % 26) + suffix;
            return scaled.ToString(format, CultureInfo.InvariantCulture) + suffix;
        }
        public static string Format(double value, int decimals = 1)
        {
            if (double.IsNaN(value)) return "0";
            if (double.IsInfinity(value)) return value < 0 ? "−∞" : "∞";
            decimals = Math.Max(0, Math.Min(6, decimals));
            bool negative = value < 0;
            double scaled = Math.Abs(value);
            int unit = 0;
            while (scaled >= 1000) { scaled /= 1000; unit++; }
            scaled = Math.Round(scaled, decimals, MidpointRounding.AwayFromZero);
            if (scaled >= 1000) { scaled /= 1000; unit++; }
            string suffix = "";
            for (int index = unit; index > 0; index = (index - 1) / 26)
                suffix = (char)('a' + (index - 1) % 26) + suffix;
            string format = decimals == 0 ? "0" : "0." + new string('#', decimals);
            return (negative && scaled != 0 ? "-" : "") + scaled.ToString(format, CultureInfo.InvariantCulture) + suffix;
        }
    }
}
