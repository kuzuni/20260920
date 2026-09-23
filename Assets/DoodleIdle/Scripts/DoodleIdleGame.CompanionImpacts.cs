using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        readonly ParticleSystem[] companionImpactParticles = new ParticleSystem[34];

        void BuildCompanionImpactParticles(List<ParticleSystem> all)
        {
            for (int i = 0; i < companionImpactParticles.Length; i++) {
                var art = DoodleCollectionArt.CompanionImpact(i);
                if (!art) continue;
                var system = MakeParticles("Companion impact: " + i, art, 548, 512, false);
                if (i == 0 || i == 1 || i == 5 || i == 7 || i == 11)
                    SetFlamePalette(system, i == 0 ? new Color(.64f,.3f,.82f) : i == 1 ? new Color(1,.94f,.73f) : i == 7 ? new Color(.7f,.42f,.22f) : new Color(.77f,.79f,.82f));
                var size = system.sizeOverLifetime;
                size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
                    new Keyframe(0,.5f), new Keyframe(.12f,1), new Keyframe(.55f,.7f), new Keyframe(1,.08f)));
                var color = system.colorOverLifetime;
                var fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
                    new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.08f), new GradientAlphaKey(1,.35f), new GradientAlphaKey(0,1) });
                color.color = fade;
                var spin = system.rotationOverLifetime;
                spin.enabled = i <= 2 || i == 4 || i == 5 || i == 7 || i == 11 || i == 13 || i == 14 || i == 16 || i == 22;
                if (spin.enabled) spin.z = new ParticleSystem.MinMaxCurve(-5,5);
                var force = system.forceOverLifetime;
                force.enabled = true; force.space = ParticleSystemSimulationSpace.World;
                force.y = i == 6 || i == 8 || i == 12 ? -5f : spin.enabled ? -2.5f : i == 18 || i == 21 || i == 23 ? 1.8f : 0;
                companionImpactParticles[i] = system; all.Add(system);
            }
        }

        void EmitCompanionImpact(int index, Vector2 position, float radius)
        {
            var system = companionImpactParticles[index];
            if (!system) return;
            bool precise = index == 19;
            bool electric = index == 9 || index == 10;
            float lifetime = precise ? .22f : electric ? .32f : .6f;
            float speed = precise ? 3.4f : Mathf.Clamp(radius * 2.4f,2.8f,5.2f);
            int count = precise ? 5 : 10 + index / 4;
            for (int i = 0; i < count; i++) {
                float angle = (i + ParticleRandom(-.35f,.35f)) * Mathf.PI * 2 / count;
                Vector2 direction = Direction(angle);
                // Water/honey artwork has its rounded leading edge on the left; embers face right.
                float rotation = -angle * Mathf.Rad2Deg + (index == 6 || index == 8 || index == 12 ? 180 : 0);
                system.Emit(new ParticleSystem.EmitParams {
                    position = position + direction * ParticleRandom(0,.1f),
                    velocity = direction * ParticleRandom(speed * .35f,speed), startColor = Color.white,
                    startSize = ParticleRandom(precise ? .12f : .18f,precise ? .24f : .42f),
                    startLifetime = lifetime * ParticleRandom(.75f,1.15f), rotation = rotation, randomSeed = ++particleSeed
                },1);
            }
        }
    }
}
