using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    // ParticleSystem supplies motion and lifetime; uGUI draws it above the dim layer
    // in overlay and camera canvases without a second world camera/material.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleRewardCelebration : MaskableGraphic
    {
        ParticleSystem particles;
        readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[160];
        public int LiveParticleCount => particles ? particles.particleCount : 0;
        protected override void Start()
        {
            base.Start(); Canvas.ForceUpdateCanvases();
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main; main.playOnAwake=false; main.loop=false; main.duration=2.4f;
            main.maxParticles=160; main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.useUnscaledTime=true; main.gravityModifier=40; main.startSpeed=0;
            var emission=particles.emission; emission.enabled=false;
            var shape=particles.shape; shape.enabled=false;
            var fade=particles.colorOverLifetime; fade.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.65f),new GradientAlphaKey(0,1)});
            fade.color=gradient;
            var rotation=particles.rotationOverLifetime; rotation.enabled=true;
            rotation.z=new ParticleSystem.MinMaxCurve(-7,7);
            GetComponent<ParticleSystemRenderer>().enabled=false;
            var rng=new System.Random(2718); var r=rectTransform.rect;
            Color[] colors={new Color(1,.79f,.23f),new Color(.36f,.73f,1),new Color(1,.45f,.62f),new Color(.45f,.86f,.44f),new Color(.73f,.5f,1)};
            particles.Play();
            for(int i=0;i<144;i++) {
                float side=i%2==0?-1:1;
                var emit=new ParticleSystem.EmitParams {
                    position=new Vector3(r.center.x+side*r.width*.44f,r.center.y-r.height*.26f,0),
                    velocity=new Vector3(-side*(100+(float)rng.NextDouble()*250),220+(float)rng.NextDouble()*300,0),
                    startLifetime=1.5f+(float)rng.NextDouble()*.8f,
                    startSize=9+(float)rng.NextDouble()*7,
                    rotation=(float)rng.NextDouble()*360,
                    startColor=colors[i%colors.Length]
                };
                particles.Emit(emit,1);
            }
            SetVerticesDirty();
        }
        void Update() { if(particles) SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if(!particles)return;
            int count=particles.GetParticles(buffer);
            for(int i=0;i<count;i++) {
                var particle=buffer[i]; float angle=particle.rotation*Mathf.Deg2Rad;
                Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                float size=particle.GetCurrentSize(particles);
                Vector2 a=direction*size*.35f,b=new Vector2(-direction.y,direction.x)*size*.8f,p=particle.position;
                DoodleCommerceMesh.Polygon(vh,new[]{p-a-b,p+a-b,p+a+b,p-a+b},particle.GetCurrentColor(particles),1.3f);
            }
        }
        protected override void OnDisable() { if(particles)particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); base.OnDisable(); }
    }
}
