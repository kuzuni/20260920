using System;
using UnityEngine;

namespace DoodleIdle
{
    // Compact ASCII metadata fits BACKND's 256-byte leaderboard extra-data limit.
    public static class DoodleRankMetadata
    {
        public static string Encode(GameNumber power,DoodlePlayerLook look) => string.Join("|","1",power.ToString(),
            look.appearanceIcon,look.weaponIcon,ColorUtility.ToHtmlStringRGBA(look.appearanceTint),ColorUtility.ToHtmlStringRGBA(look.weaponTint));
        public static DoodlePlayerLook Decode(string text,out GameNumber power)
        {
            power=0;var look=new DoodlePlayerLook();
            if(string.IsNullOrEmpty(text))return look;
            var fields=text.Split('|');if(fields.Length!=6 || fields[0]!="1")return look;
            GameNumber.TryParse(fields[1],out power);
            look.appearanceIcon=fields[2];look.weaponIcon=fields[3];
            if(ColorUtility.TryParseHtmlString("#"+fields[4],out var tint))look.appearanceTint=tint;
            if(ColorUtility.TryParseHtmlString("#"+fields[5],out tint))look.weaponTint=tint;
            return look;
        }
        // The leaderboard uses a double; logarithms preserve magnitude beyond double HP/ATK limits.
        public static double PowerScore(GameNumber power)
        {
            if(power<=1)return 0;
            double exponent=(double)power.Exponent;
            if(double.IsInfinity(exponent))throw new ArgumentOutOfRangeException(nameof(power));
            return exponent+Math.Log10(power.Mantissa);
        }
    }
}
