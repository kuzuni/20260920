using UnityEngine;
using UnityEngine.UI;
namespace DoodleIdle
{
    // One UI mesh follows a real particle simulation; no per-particle objects or allocations.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleUiIconBurst : MaskableGraphic
    {
        ParticleSystem particles;
        readonly ParticleSystem.Particle[] buffer=new ParticleSystem.Particle[100];
        Sprite icon, ownedIcon;
        Texture2D ownedTexture;
        int count;
        readonly System.Random visualRandom=new System.Random();
        float Range(float min,float max)=>(float)(min+(max-min)*visualRandom.NextDouble());
        public int LastBurstCount {get;private set;}
        public override Texture mainTexture => icon ? icon.texture : Texture2D.whiteTexture;
        public static void Play(Image source,int upgrades)
        {
            if(!source || upgrades<=0)return;
            var effect=source.GetComponentInChildren<DoodleUiIconBurst>(true);
            if(!effect){var rect=UiKit.Rect(source.transform,"Upgrade icon particles");UiKit.Stretch(rect);effect=rect.gameObject.AddComponent<DoodleUiIconBurst>();effect.raycastTarget=false;}
            if(!source.enabled && source.GetComponentInChildren<DoodleCriticalBadge>() && !effect.ownedIcon)effect.CaptureBadge(source);
            effect.Emit(effect.ownedIcon ? effect.ownedIcon : source.sprite,upgrades);
        }
        // Extended critical tiers are UI geometry + text instead of a sprite atlas.
        // Bake their existing artwork once per visible row, then reuse one texture for all particles.
        void CaptureBadge(Image source)
        {
            var root=new GameObject("Critical badge particle artwork",typeof(Canvas));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var rt=(RectTransform)root.transform;rt.sizeDelta=Vector2.one*128;rt.position=new Vector3(0,-2000,0);
            var cameraObject=new GameObject("Critical badge bake camera",typeof(Camera));
            var target=RenderTexture.GetTemporary(128,128,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            try {
                var original=source.GetComponentInChildren<DoodleCriticalBadge>();
                var shape=Instantiate(original,rt);UiKit.Stretch(shape.rectTransform);shape.color=original.color;
                foreach(var label in source.GetComponentsInChildren<Text>()) {
                    var copy=Instantiate(label,rt);UiKit.Stretch(copy.rectTransform,8,7,8,7);
                }
                foreach(var child in root.GetComponentsInChildren<Transform>())child.gameObject.layer=31;
                var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=64;camera.aspect=1;
                camera.transform.position=rt.position+Vector3.back*10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;camera.targetTexture=target;
                Canvas.ForceUpdateCanvases();
                if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null)UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});else camera.Render();
                RenderTexture.active=target;ownedTexture=new Texture2D(128,128,TextureFormat.RGBA32,false);ownedTexture.ReadPixels(new Rect(0,0,128,128),0,0);ownedTexture.Apply();
                ownedIcon=Sprite.Create(ownedTexture,new Rect(0,0,128,128),Vector2.one*.5f,128);
            } finally {RenderTexture.active=previous;cameraObject.GetComponent<Camera>().targetTexture=null;RenderTexture.ReleaseTemporary(target);root.SetActive(false);Destroy(root);Destroy(cameraObject);}
        }
        protected override void OnDestroy(){if(ownedIcon)Destroy(ownedIcon);if(ownedTexture)Destroy(ownedTexture);base.OnDestroy();}
        void Emit(Sprite sprite,int amount)
        {
            icon=sprite;SetMaterialDirty();
            if(!particles){
                particles=gameObject.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=100;main.simulationSpace=ParticleSystemSimulationSpace.Local;
                main.startLifetime=new ParticleSystem.MinMaxCurve(.55f,.9f);main.startSize=new ParticleSystem.MinMaxCurve(16,27);main.startSpeed=0;
                var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
                GetComponent<ParticleSystemRenderer>().enabled=false;
            }
            particles.Clear();LastBurstCount=Mathf.Clamp(amount,1,100);
            for(int i=0;i<LastBurstCount;i++){
                float angle=Range(25f,155f)*Mathf.Deg2Rad,speed=Range(85f,190f);
                particles.Emit(new ParticleSystem.EmitParams{position=new Vector3(Range(-8f,8f),0,0),velocity=new Vector3(Mathf.Cos(angle)*speed,Mathf.Sin(angle)*speed,0)},1);
            }
            enabled=true;SetVerticesDirty();
        }
        void Update()
        {
            if(!particles || particles.particleCount==0){if(count!=0){count=0;SetVerticesDirty();}return;}
            particles.Simulate(Time.unscaledDeltaTime,false,false,false);count=particles.GetParticles(buffer);SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(!icon)return;var uv=UnityEngine.Sprites.DataUtility.GetOuterUV(icon);
            for(int i=0;i<count;i++){
                var part=buffer[i];float age=part.startLifetime-part.remainingLifetime;
                Vector2 pos=(Vector2)part.position+rectTransform.rect.center+Vector2.down*(95*age*age);
                float half=part.GetCurrentSize(particles)*.5f;Color tint=Color.white;tint.a=Mathf.Clamp01(part.remainingLifetime/.25f);
                int n=vh.currentVertCount;
                vh.AddVert(pos+new Vector2(-half,-half),tint,new Vector2(uv.x,uv.y));vh.AddVert(pos+new Vector2(-half,half),tint,new Vector2(uv.x,uv.w));
                vh.AddVert(pos+new Vector2(half,half),tint,new Vector2(uv.z,uv.w));vh.AddVert(pos+new Vector2(half,-half),tint,new Vector2(uv.z,uv.y));
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
