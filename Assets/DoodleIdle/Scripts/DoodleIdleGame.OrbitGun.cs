using UnityEngine;
using DG.Tweening;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public const float OrbitSkillLifetime = 8, OrbitSkillCycle = 12, OrbitGunRadius = 2.5f;
        public bool BananasActive { get; private set; }
        public bool OrbitGunActive => orbitGun;
        public int OrbitBulletsLaunched { get; private set; }
        public event System.Action<float, Vector2, Vector2> OrbitBulletLaunched;
        float bananaCycleAge, orbitGunAge, orbitGunAngle, orbitGunShotClock;
        SpriteRenderer orbitGun;
        Transform orbitGunMuzzle;
        Tween orbitGunRecoil;

        void SpawnOrbitGun()
        {
            if (orbitGun) return;
            orbitGunAge = orbitGunAngle = 0;
            orbitGunShotClock = .09f;
            // The gun has no collider or contact damage. Only its moving bullets call Impact.
            orbitGun = Visual("Orbiting automatic gun", summonArt["OrbitGun"], player.Position + Vector2.right * OrbitGunRadius,
                Vector2.one * 1.3f, 460);
            orbitGunMuzzle = new GameObject("Orbit gun muzzle").transform;
            orbitGunMuzzle.SetParent(orbitGun.transform, false);
            orbitGunMuzzle.localPosition = new Vector3(.46f, .1f, 0);
        }
        void TickOrbitGun(float dt)
        {
            if (!orbitGun) return;
            if (orbitGunRecoil != null && orbitGunRecoil.IsActive()) orbitGunRecoil.ManualUpdate(dt, dt);
            orbitGunAge += dt;
            if (orbitGunAge >= OrbitSkillLifetime)
            {
                orbitGunRecoil?.Kill(); orbitGun.gameObject.SetActive(false); ReleaseVisual(orbitGun.gameObject); orbitGun = null; return;
            }
            orbitGunAngle += dt * 1.65f;
            orbitGunShotClock -= dt;
            Vector2 position = player.Position + Direction(orbitGunAngle) * OrbitGunRadius;
            var target = InRange(position, 10);
            Vector2 direction = target != null ? (target.Position - position).normalized : Direction(orbitGunAngle);
            orbitGun.transform.position = position;
            orbitGun.transform.rotation = Aim(direction);
            orbitGun.flipY = direction.x < 0;
            orbitGunMuzzle.localPosition = new Vector3(.46f, orbitGun.flipY ? -.1f : .1f, 0);
            orbitGun.sortingOrder = Order(position) + 3;
            if (target == null) { orbitGunShotClock = 0; return; }
            if (orbitGunShotClock > 0) return;
            // Finish the previous punch before sampling the actual child muzzle for this shot.
            orbitGunRecoil?.Kill(); orbitGun.transform.localScale = new Vector3(1.3f, 1.3f, 1);
            Vector2 muzzle = orbitGunMuzzle.position;
            direction = (target.Position - muzzle).normalized;
            AddMoving(SummonSkill.OrbitGun, summonArt["OrbitBullet"], muzzle, direction, .38f, 20, .7f, 8, .65f);
            Echo("Orbit gun muzzle flash", summonArt["MuzzleFlash"], muzzle + direction * .15f, Vector2.one * .5f, Aim(direction), .065f, 1, 470);
            orbitGunShotClock += .09f;
            orbitGunRecoil = orbitGun.transform.DOPunchScale(new Vector3(-.18f, .14f, 0), .085f, 2, .6f)
                .SetTarget(orbitGun.transform).SetLink(orbitGun.gameObject).SetUpdate(UpdateType.Manual);
            OrbitBulletsLaunched++; OrbitBulletLaunched?.Invoke(Time.fixedTime, muzzle, direction);
        }
        void ClearOrbitGun()
        {
            orbitGunRecoil?.Kill();
            if (orbitGun) { orbitGun.gameObject.SetActive(false); ReleaseVisual(orbitGun.gameObject); }
            orbitGun = null; OrbitBulletsLaunched = 0;
        }
    }
}
