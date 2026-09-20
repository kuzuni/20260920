using System;
using System.Globalization;

namespace DoodleIdle
{
    /// <summary>Display only: a = 1,000, b = 1,000,000, then z, aa, ab…</summary>
    public static class UiNumber
    {
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
