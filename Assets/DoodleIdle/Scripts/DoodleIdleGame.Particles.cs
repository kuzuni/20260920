using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        ParticleSystem dustParticles, explosionParticles, goldParticles, sandParticles, groundFireParticles, purpleFireParticles,blueFireParticles;
        ParticleSystem meteorTrailParticles, meteorExplosionParticles, golemSlamParticles;
        readonly List<Material> particleMaterials = new List<Material>();
        ParticleSystem[] particleSystems;
        uint particleSeed = 1;
        public int GoldCoinsEmitted { get; private set; }

        void BuildParticles()
        {
            dustParticles = MakeParticles("Dust Particle System", summonArt["SandPuff"], 600, 2048, false);
            explosionParticles = MakeParticles("Cannon Explosion Particle System", summonArt["Explosion"], 610, 256, false);
            goldParticles = MakeParticles("Gold Coin Particle System", summonArt["GoldCoin"], 620, 2048, true);
            sandParticles = MakeParticles("Sand Spray Particle System", summonArt["SandPuff"], 510, 1024, false);
            groundFireParticles = MakeParticles("Molotov Ground Fire Particle System", summonArt["GroundFlame"], 530, 2048, false);
            var fireSpin = groundFireParticles.rotationOverLifetime; fireSpin.enabled = false;
            purpleFireParticles=MakeParticles("Purple Arrow Fire Trail Particle System",summonArt["GroundFlame"],525,1024,false);
            blueFireParticles=MakeParticles("Blue Molotov Fire Particle System",summonArt["GroundFlame"],531,2048,false);
            SetFlamePalette(purpleFireParticles,new Color(.7f,.3f,1));
            SetFlamePalette(blueFireParticles,new Color(.2f,.65f,1));
            meteorTrailParticles = MakeParticles("Meteor Fire Trail Particle System", summonArt["GroundFlame"], 640, 512, false);
            SetFlamePalette(meteorTrailParticles, new Color(1, .18f, .06f));
            meteorExplosionParticles = MakeParticles("Meteor Explosion Particle System", summonArt["Explosion"], 655, 256, false);
            golemSlamParticles = MakeParticles("Golem Ground Slam Dust Particle System", summonArt["SandPuff"], 515, 512, false);
            var all = new List<ParticleSystem> { dustParticles, explosionParticles, goldParticles, sandParticles, groundFireParticles,purpleFireParticles,blueFireParticles,meteorTrailParticles,meteorExplosionParticles,golemSlamParticles };
            BuildCompanionImpactParticles(all);
            particleSystems = all.ToArray();
        }

        void SetFlamePalette(ParticleSystem system,Color color)
        {
            var material=system.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            material.SetFloat("_Recolor",1);material.SetColor("_Palette",color);
            var spin=system.rotationOverLifetime;spin.enabled=false;
        }

        ParticleSystem MakeParticles(string label, Sprite art, int order, int capacity, bool coins)
        {
            var go = new GameObject(label);
            go.transform.SetParent(world, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = capacity;
            main.startSpeed = 0;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var color = system.colorOverLifetime; color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, coins ? .65f : .15f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var size = system.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, coins ? 1 : .65f, 1, coins ? .7f : 1.5f));
            var spin = system.rotationOverLifetime; spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(coins ? -7 : -1, coins ? 7 : 1);
            if (coins)
            {
                var force = system.forceOverLifetime; force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.y = -4;
            }
            var material = new Material(Resources.Load<Shader>("DoodleIdle/DoodleParticles"));
            material.name = label + " material"; material.mainTexture = art.texture;
            Rect rect = art.textureRect;
            material.SetVector("_UvRect", new Vector4(rect.x / art.texture.width, rect.y / art.texture.height, rect.width / art.texture.width, rect.height / art.texture.height));
            particleMaterials.Add(material);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingOrder = order;
            renderer.maxParticleSize = 1;
            system.useAutoRandomSeed = false; system.randomSeed = (uint)(order + 1);
            return system;
        }

        // Emissions share reusable systems; hundreds of impacts never create hundreds of emitters.
        // Private deterministic noise avoids consuming the combat/spawn Random stream for cosmetics.
        float ParticleRandom(float min, float max)
        {
            particleSeed = particleSeed * 1664525u + 1013904223u;
            return Mathf.Lerp(min, max, (particleSeed & 0x00ffffff) / 16777216f);
        }
        void EmitBurst(ParticleSystem system, Vector2 position, Color color, int count, float minSize, float maxSize, float speed, float minLife, float maxLife)
        {
            if (!system) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = Direction(ParticleRandom(0, Mathf.PI * 2)) * ParticleRandom(speed * .35f, speed);
                if (system == goldParticles) velocity += Vector2.up * 3.4f;
                var particle = new ParticleSystem.EmitParams
                {
                    position = position, velocity = velocity, startColor = color,
                    startSize = ParticleRandom(minSize, maxSize), startLifetime = ParticleRandom(minLife, maxLife),
                    rotation = ParticleRandom(0, 360), randomSeed = ++particleSeed
                };
                system.Emit(particle, 1);
            }
        }
        void EmitCannonExplosion(Vector2 position)
        {
            EmitBurst(explosionParticles, position, Color.white, 1, 3.2f, 3.2f, 0, .4f, .4f);
            EmitBurst(explosionParticles, position, new Color(1, .85f, .6f), 12, .45f, .95f, 4.5f, .4f, .65f);
            EmitBurst(dustParticles, position, new Color(.7f, .6f, .45f, .7f), 14, .5f, 1.1f, 3.8f, .6f, .95f);
        }
        void EmitMeteorFlame(Vector2 position, Vector2 travelDirection)
        {
            // The flame artwork points up. Its tip points back along the travel path;
            // unlike the rock, these world-space particles never inherit its spin.
            Vector2 tail = -travelDirection;
            meteorTrailParticles.Emit(new ParticleSystem.EmitParams {
                position = position, velocity = tail * .45f, startColor = Color.white,
                startSize = ParticleRandom(1.25f, 1.75f), startLifetime = .42f,
                rotation = -(Mathf.Atan2(tail.y, tail.x) * Mathf.Rad2Deg - 90), randomSeed = ++particleSeed
            }, 1);
        }
        void EmitGold(Vector2 position)
        {
            EmitBurst(goldParticles, position, Color.white, 9, .22f, .4f, 6.8f, .4f, .625f);
            GoldCoinsEmitted += 9;
        }
        void EmitSand(Vector2 position, Vector2 direction)
        {
            for (int i = 0; i < 3; i++)
            {
                var particle = new ParticleSystem.EmitParams
                {
                    position = position, velocity = direction * ParticleRandom(.3f, 1.6f),
                    startColor = new Color(1, .9f, .68f, .65f), startSize = ParticleRandom(.3f, .75f),
                    startLifetime = ParticleRandom(.25f, .5f), rotation = ParticleRandom(0, 360), randomSeed = ++particleSeed
                };
                sandParticles.Emit(particle, 1);
            }
        }
        void TickParticles(float dt)
        {
            // Manual fixed-step simulation obeys the game's pause flag and keeps coin origins in world space.
            foreach (var system in particleSystems) system.Simulate(dt, false, false, false);
        }
        void ClearParticles()
        {
            if (particleSystems != null) foreach (var system in particleSystems) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            GoldCoinsEmitted = 0; particleSeed = 1;
        }
        void DisposeParticleMaterials()
        {
            foreach (var material in particleMaterials) if (material) Destroy(material);
            particleMaterials.Clear();
        }
    }
}
