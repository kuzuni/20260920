using DG.Tweening;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleRigVisual
    {
        float spawnScale = 1;
        Tween spawnTween;
        bool spawning;
        public void BeginSpawnFeedback()
        {
            if (spawnTween == null) spawnTween = DG.Tweening.DOTween.To(() => spawnScale, v => spawnScale = v, 1, 1)
                .From(.05f).SetEase(Ease.OutCubic).SetAutoKill(false).Pause();
            spawnTween.Goto(0, false); spawnScale = .001f; spawning = true;
            Rig.CancelAttack(); Sync();
        }
        public void AdvanceSpawnFeedback(float progress)
        {
            spawnTween.Goto(Mathf.Clamp01(progress), false); Sync();
        }
        public void ResetSpawnFeedback() { spawnScale = 1; spawning = false; }
    }
}
