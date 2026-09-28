using System.Collections;
using System.Reflection;
using CodeStage.AntiCheat.Detectors;
using CodeStage.AntiCheat.ObscuredTypes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public sealed class DoodleSecurityTests
    {
        [UnityTest]
        public IEnumerator ObscuredWalletMutationTriggersDetection()
        {
            bool detected = false;
            ObscuredCheatingDetector.StartDetection(() => detected = true);
            try
            {
                object boxed = (ObscuredInt)100;
                var hidden = typeof(ObscuredInt).GetField("hiddenValue", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(hidden, Is.Not.Null);
                hidden.SetValue(boxed, (int)hidden.GetValue(boxed) ^ 16);
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Obscured Cheating Detector: Detection backlog"));
                int tampered = (ObscuredInt)boxed;
                Assert.That(tampered, Is.Not.EqualTo(100));
                Assert.That(detected, Is.True, "Memory changes must be detected on the next read.");
            }
            finally { ObscuredCheatingDetector.Dispose(); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpeedDetectorAcceptsAuthorizedPauseAndRejectsAnExternalSpeedChange()
        {
            bool detected = false;
            float original = Time.timeScale;
            SpeedHackDetector.StartDetection(() => detected = true, .1f, 0, 1);
            try
            {
                SpeedHackDetector.SetTimeScale(0);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(detected, Is.False, "An authorized pause must not terminate a legitimate game.");
                SpeedHackDetector.SetTimeScale(1);
                Time.timeScale = 4;
                double deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (!detected && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(detected, Is.True, "An unauthorized timeScale change must be detected.");
            }
            finally { SpeedHackDetector.SetTimeScale(original); SpeedHackDetector.Dispose(); }
        }
    }
}
