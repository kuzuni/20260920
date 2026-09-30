using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public bool IsPvpEngine {get;private set;}
        public bool PvpSessionActive => pvpSessionActive;
        bool pvpSessionActive;
        DoodleIdleGame pvpOpponent;

        internal static DoodleIdleGame CreatePvpEngine(DoodleIdleGame source,DoodlePvpLoadout loadout,Vector2 position)
        {
            var go=new GameObject("PVP combat: "+loadout.playerName);
            try {
            var engine=go.AddComponent<DoodleIdleGame>();engine.IsPvpEngine=true;
            // Preserve the scene's authored timings and tuning for every existing skill.
            foreach(var field in typeof(DoodleIdleGame).GetFields(BindingFlags.Public|BindingFlags.Instance))
                if(!field.IsInitOnly && (field.FieldType.IsPrimitive || field.FieldType==typeof(Vector2)))
                    field.SetValue(engine,field.GetValue(source));
            engine.paused=true;engine.autoPlay=true;
            engine.Ui=DoodleUi.CreatePvpCombatModel(engine,loadout);
            engine.sprites=engine.LoadAtlas();engine.LoadSkillArt();engine.LoadSummonArt();engine.LoadActorAnimations();
            engine.disc=MakeDisc();engine.slash=MakeSlash();
            engine.spriteMaterial=new Material(source.spriteMaterial);
            engine.frictionless=new PhysicsMaterial2D("PVP frictionless"){friction=0,bounciness=0};
            engine.world=new GameObject("PVP combat world").transform;engine.world.SetParent(go.transform,false);
            engine.gameCamera=source.gameCamera;engine.uiFont=source.uiFont;
            engine.BuildParticles();engine.BuildCombatFeedback();engine.ResetGame();
            engine.player.body.position=position;engine.player.root.transform.position=position;engine.PrepareEquippedCompanions();
            foreach(var banana in engine.bananas)banana.gameObject.SetActive(false);
            engine.Ready=true;
            foreach(var rig in engine.world.GetComponentsInChildren<DoodleRigVisual>()){rig.Paused=true;rig.Rig.ResetPooledAnimation();rig.Sync();}
            return engine;
            } catch { go.SetActive(false);Destroy(go);throw; }
        }

        internal void SetPvpOpponent(DoodleIdleGame other)
        {
            pvpOpponent=other;enemies.Clear();enemies.Add(other.player);
            facing=(other.player.Position-player.Position).normalized;
            Animate(player);UpdateHeldClub();
        }

        void TickPvpRegeneration(float dt)
        {
            player.hp=GameNumber.Min(player.maxHp,player.hp+Ui.HealthRegenAmount*dt);
            UpdatePlayerHealthBar();
        }

        internal IEnumerator RunPvpBattle(DoodlePvpLoadout own,DoodlePvpLoadout opponent,Action<bool> completed)
        {
            if(pvpSessionActive || !Ready)yield break;
            bool wasPaused=paused;
            var rng=UnityEngine.Random.state;
            var originalRenderers=world.GetComponentsInChildren<Renderer>(true);
            var originalVisibility=new bool[originalRenderers.Length];
            for(int i=0;i<originalRenderers.Length;i++)originalVisibility[i]=originalRenderers[i].forceRenderingOff;
            bool hudEnabled=Ui.Canvas.enabled;
            float oldSize=gameCamera.orthographicSize;var oldPosition=gameCamera.transform.position;
            GameObject overlay=null;DoodleIdleGame left=null,right=null;bool won=false,finished=false;
            pvpSessionActive=true;
            try {
            if(!paused)TogglePause();
            for(int i=0;i<originalRenderers.Length;i++){
                // Keep the field's floor, while hiding its paused actors and effects.
                if(originalRenderers[i].name!="Generated dirt floor")originalRenderers[i].forceRenderingOff=true;
            }
            Ui.Canvas.enabled=false;
            gameCamera.orthographicSize=7.5f;gameCamera.transform.position=new Vector3(0,0,-10);
            overlay=new GameObject("PVP countdown",typeof(Canvas),typeof(CanvasScaler));
            var canvas=overlay.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
            var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(720,1520);scaler.matchWidthOrHeight=1;
            var title=Label(overlay.transform,own.playerName+"  VS  "+opponent.playerName,31,new Vector2(-330,-180),new Vector2(660,100),TextAnchor.MiddleCenter,new Vector2(.5f,1));
            var countdown=Label(overlay.transform,"대전 준비 중",74,new Vector2(-300,-65),new Vector2(600,130),TextAnchor.MiddleCenter,Vector2.one*.5f);
            var status=Label(overlay.transform,"",25,new Vector2(-330,125),new Vector2(660,100),TextAnchor.MiddleCenter,new Vector2(.5f,0));
            title.supportRichText=false;
                left=CreatePvpEngine(this,own,new Vector2(-2.2f,0));
                foreach(var renderer in left.world.GetComponentsInChildren<Renderer>())renderer.forceRenderingOff=true;
                yield return null;
                right=CreatePvpEngine(this,opponent,new Vector2(2.2f,0));
                foreach(var renderer in right.world.GetComponentsInChildren<Renderer>())renderer.forceRenderingOff=true;
                left.SetPvpOpponent(right);right.SetPvpOpponent(left);
                // Native SpriteSkin/Animator initialization occurs behind the countdown.
                yield return null;yield return null;
                // Evaluate idle after both native Animator graphs have bound their skeletons.
                foreach(var engine in new[]{left,right})
                    foreach(var visual in engine.world.GetComponentsInChildren<DoodleRigVisual>()) {
                        visual.Rig.ResetPooledAnimation();visual.Sync();
                    }
                yield return null;yield return null;
                foreach(var engine in new[]{left,right})
                    foreach(var renderer in engine.world.GetComponentsInChildren<Renderer>())renderer.forceRenderingOff=false;
                for(int n=3;n>0;n--){countdown.text=n.ToString();yield return new WaitForSecondsRealtime(1);}
                countdown.text="";left.TogglePause();right.TogglePause();
                float nextStatus=0;
                // Bound a regen stalemate; remaining health ratio decides at the limit.
                const float limit=90;
                while(left.PlayerHealthAmount>0 && right.PlayerHealthAmount>0 && left.Elapsed<limit){
                    var center=(left.player.Position+right.player.Position)*.5f;
                    var separation=left.player.Position-right.player.Position;
                    gameCamera.transform.position=new Vector3(center.x,center.y,-10);
                    gameCamera.orthographicSize=Mathf.Max(7.5f,Mathf.Abs(separation.y)*.5f+3,(Mathf.Abs(separation.x)*.5f+3)/Mathf.Max(.2f,gameCamera.aspect));
                    if(Time.unscaledTime>=nextStatus){
                        nextStatus=Time.unscaledTime+.1f;
                        status.text=UiNumber.Format(left.PlayerHealthAmount)+" / "+UiNumber.Format(left.PlayerMaxHealthAmount)+"    :    "+UiNumber.Format(right.PlayerHealthAmount)+" / "+UiNumber.Format(right.PlayerMaxHealthAmount);
                    }
                    yield return null;
                }
                won=right.PlayerHealthAmount<=0 || left.PlayerHealthAmount>0 && left.PlayerHealthAmount/left.PlayerMaxHealthAmount>right.PlayerHealthAmount/right.PlayerMaxHealthAmount;
                finished=true;
            } finally {
                if(left){left.paused=true;left.gameObject.SetActive(false);Destroy(left.gameObject);}
                if(right){right.paused=true;right.gameObject.SetActive(false);Destroy(right.gameObject);}
                Destroy(overlay);
                for(int i=0;i<originalRenderers.Length;i++)if(originalRenderers[i])originalRenderers[i].forceRenderingOff=originalVisibility[i];
                gameCamera.orthographicSize=oldSize;gameCamera.transform.position=oldPosition;Ui.Canvas.enabled=hudEnabled;
                UnityEngine.Random.state=rng;pvpSessionActive=false;
                if(!wasPaused && paused)TogglePause();
            }
            if(finished)completed?.Invoke(won);
        }
    }
}
