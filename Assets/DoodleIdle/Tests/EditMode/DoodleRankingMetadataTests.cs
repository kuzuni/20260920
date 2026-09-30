using NUnit.Framework;
using UnityEngine;

namespace DoodleIdle.Tests
{
    public sealed class DoodleRankingMetadataTests
    {
        [Test]
        public void RankingMetadataPreservesHugePowerAndEquippedAppearance()
        {
            var power=new GameNumber(7.25,500);
            var look=new DoodlePlayerLook{appearanceIcon="SkinAppearanceMint",weaponIcon="SkinWeaponVine",appearanceTint=new Color32(37,128,221,255),weaponTint=new Color32(211,94,53,255)};
            string encoded=DoodleRankMetadata.Encode(power,look);
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(encoded),Is.LessThanOrEqualTo(256));
            var decoded=DoodleRankMetadata.Decode(encoded,out var restored);
            Assert.That(restored,Is.EqualTo(power));Assert.That(decoded.appearanceIcon,Is.EqualTo(look.appearanceIcon));Assert.That(decoded.weaponIcon,Is.EqualTo(look.weaponIcon));
            Assert.That((Color32)decoded.appearanceTint,Is.EqualTo((Color32)look.appearanceTint));Assert.That((Color32)decoded.weaponTint,Is.EqualTo((Color32)look.weaponTint));
            Assert.That(DoodleRankMetadata.PowerScore(power),Is.GreaterThan(DoodleRankMetadata.PowerScore(new GameNumber(9,499))));
            Assert.That(DoodleRankMetadata.PowerScore(power),Is.LessThan(DoodleRankMetadata.PowerScore(new GameNumber(8,500))));
        }
        [TestCase(null)][TestCase("")][TestCase("old metadata")]
        public void LegacyRowsUseDefaultPlayer(string metadata)
        {
            var look=DoodleRankMetadata.Decode(metadata,out _);
            Assert.That(look.appearanceIcon,Is.EqualTo("Player"));Assert.That(look.weaponIcon,Is.EqualTo("Club"));
        }
    }
}
