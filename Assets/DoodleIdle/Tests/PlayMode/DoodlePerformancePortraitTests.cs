using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Profiling;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator PooledThemeChangesKeepDeformedMeshesNearTheirBones()
        {
            game.TogglePause();
            foreach(int stage in new[]{800,1,101,1,901,1}){
                game.Ui.DebugSetMainStage(stage);
                if(stage==800 || stage==901)game.Ui.DebugSetMainStage(1);
                yield return null;yield return null;yield return null;
                foreach(var visual in game.GetComponentsInChildren<DoodleRigVisual>()){
                    foreach(var renderer in visual.Rig.partRenderers){
                        if(!renderer.enabled)continue;
                        var skin=renderer.GetComponent<UnityEngine.U2D.Animation.SpriteSkin>();
                        float limit=Mathf.Max(1,visual.Entry.extent*visual.Rig.transform.lossyScale.magnitude)*4;
                        float maxDistance=0;
                        foreach(var vertex in skin.GetDeformedVertexPositionData()){
                            float distance=Vector3.Distance(renderer.transform.TransformPoint(vertex),visual.Rig.skeleton.position);
                            if(float.IsNaN(distance) || float.IsInfinity(distance))Assert.Fail("Non-finite skin vertex: "+visual.Entry.id);
                            maxDistance=Mathf.Max(maxDistance,distance);
                        }
                        Assert.That(maxDistance,Is.LessThan(limit),stage+"/"+visual.Entry.id+"/"+renderer.name+" malformed skin vertex");
                    }
                }
            }
            Object.Destroy(CaptureFrame("pool-theme-meshes.png",720,1520));
        }
        [UnityTest]
        public IEnumerator RefillPreservesPositionsKindsHealthAndCombatRandomSequence()
        {
            game.TogglePause();
            game.endlessWorld = false;
            game.Ui.ToggleBreakthroughMode();
            game.RestartCombatForStageDebug();
            var actors=(IList)typeof(DoodleIdleGame).GetField("enemies",GrowthPrivate).GetValue(game);
            var release=typeof(DoodleIdleGame).GetMethod("ReleaseEnemy",GrowthPrivate);
            var refill=typeof(DoodleIdleGame).GetMethod("Refill",GrowthPrivate);
            var actorType=actors[0].GetType();
            var bodyField=actorType.GetField("body");
            var originalBounds=game.arenaHalfSize;
            // Replaying the same wave through reused rigs must preserve gameplay data and RNG.
            // A fully excluded arena now intentionally defers spawns instead of overlapping the player.
            for (int scenario = 0; scenario < 4; scenario++) {
                int survivors = scenario == 1 ? 3 : 0;
                game.arenaHalfSize = scenario == 3 ? new Vector2(3, 3) : originalBounds;
                Place(PlayerBody(), scenario == 2 ? new Vector2(-2, -2) : Vector2.zero);
                var expected = new List<Vector2>(); var kinds = new List<int>(); var phases = new List<float>();
                float expectedNextRandom = 0;
                for (int pass = 0; pass < 2; pass++) {
                    for (int i = actors.Count - 1; i >= survivors; i--) { release.Invoke(game, new[] { actors[i] }); actors.RemoveAt(i); }
                    for (int i = 0; i < survivors; i++) Place((Rigidbody2D)bodyField.GetValue(actors[i]), new Vector2(-2 + i * 2, -2));
                    typeof(DoodleIdleGame).GetField("spawnBlockedUntil", GrowthPrivate).SetValue(game, 0f);
                    Random.InitState(1987 + scenario); refill.Invoke(game, null);
                    float nextRandom = Random.value;
                    Assert.That(actors.Count, Is.EqualTo(scenario == 3 ? 0 : game.targetPopulation));
                    var health = game.Ui.EnemyHealthAmount(game.Ui.CombatDifficultyStage);
                    for (int n = 0; n < actors.Count; n++) {
                        var position = ((Rigidbody2D)bodyField.GetValue(actors[n])).position;
                        int kind = (int)actorType.GetField("kind").GetValue(actors[n]);
                        float phase = (float)actorType.GetField("phase").GetValue(actors[n]);
                        if (pass == 0) { expected.Add(position); kinds.Add(kind); phases.Add(phase); }
                        else {
                            Assert.That(position, Is.EqualTo(expected[n]));
                            Assert.That(kind, Is.EqualTo(kinds[n])); Assert.That(phase, Is.EqualTo(phases[n]));
                        }
                        if (n < survivors) Assert.That(position, Is.EqualTo(new Vector2(-2 + n * 2, -2)));
                        else {
                            Assert.That(Vector2.Distance(position, PlayerBody().position), Is.GreaterThan(5.05f));
                            Assert.That((GameNumber)actorType.GetField("hp").GetValue(actors[n]), Is.EqualTo(health));
                            Assert.That((GameNumber)actorType.GetField("maxHp").GetValue(actors[n]), Is.EqualTo(health));
                        }
                    }
                    if (pass == 0) expectedNextRandom = nextRandom;
                    else Assert.That(nextRandom, Is.EqualTo(expectedNextRandom), "Cosmetic pool reuse cannot change combat RNG.");
                }
            }
            game.arenaHalfSize=originalBounds;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyFacingIsSynchronizedBeforeAnimationAndSkinning()
        {
            game.TogglePause();
            var actors=(IList)typeof(DoodleIdleGame).GetField("enemies",GrowthPrivate).GetValue(game);
            var actor=actors[0];var type=actor.GetType();
            var body=(Rigidbody2D)type.GetField("body").GetValue(actor);
            var art=(SpriteRenderer)type.GetField("art").GetValue(actor);
            var visual=(DoodleRigVisual)type.GetField("rigVisual").GetValue(actor);
            var animate=typeof(DoodleIdleGame).GetMethod("Animate",GrowthPrivate);
            body.simulated=true;
            foreach(float direction in new[]{-1f,1f,-1f}){
                art.flipX=direction>0;visual.Sync();
                body.linearVelocity=new Vector2(direction*2,0);
                animate.Invoke(game,new[]{actor});
                Assert.That(Mathf.Sign(visual.Rig.transform.localScale.x),Is.EqualTo(direction),"The native animation/skinning stages must see the new facing before LateUpdate");
                Assert.That(Vector3.Distance(visual.GroundShadow.transform.position,visual.Rig.groundContact.position),Is.LessThan(.0001f));
            }
            body.simulated=false;
            yield return null;
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator StandardMainGameplayKeepsMovingAttackingAndRefilling()
        {
            Time.timeScale=1;game.autoPlay=true;game.basicSkillsEnabled=game.extraSkillsEnabled=game.summonSkillsEnabled=game.companionsEnabled=true;
            var start=PlayerBody().position;bool moved=false;float elapsed=0;double frameMs=0;int frames=0;
            using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1)){
                long allocated=0;
                while(elapsed<30){yield return null;elapsed+=Time.unscaledDeltaTime;frameMs+=Time.unscaledDeltaTime*1000;allocated+=gc.LastValue;frames++;moved|=(PlayerBody().position-start).sqrMagnitude>.25f;}
                System.IO.Directory.CreateDirectory("artifacts/performance");
                System.IO.File.WriteAllText("artifacts/performance/normal-main.json","{\"seconds\":30,\"timeScale\":1,\"meanFrameMs\":"+(frameMs/frames).ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"gcBytesPerFrame\":"+allocated/frames+",\"kills\":"+game.Kills+",\"refills\":"+game.Refills+",\"companionShots\":"+game.CompanionShotsLaunched+"}");
            }
            Assert.That(moved,Is.True);Assert.That(game.SlashHits+game.DashHits,Is.GreaterThan(0));Assert.That(game.Kills,Is.GreaterThan(0));
            Assert.That(game.EnemyCount,Is.InRange(1,200));Assert.That(Application.targetFrameRate,Is.EqualTo(60));
            Object.Destroy(CaptureFrame("normal-main-after-performance.png",720,1520));
        }
        [UnityTest]
        public IEnumerator SpatialSkillQueriesPreserveEverySweptHitAndEnemyOrder()
        {
            game.TogglePause();
            var snapshot=typeof(DoodleIdleGame).GetMethod("SnapshotSummonTargets",GrowthPrivate);
            var query=typeof(DoodleIdleGame).GetMethod("FindSegmentCandidates",GrowthPrivate);
            var candidates=(List<int>)typeof(DoodleIdleGame).GetField("segmentCandidates",GrowthPrivate).GetValue(game);
            var actors=(IList)typeof(DoodleIdleGame).GetField("enemies",GrowthPrivate).GetValue(game);
            var positions=actors.Cast<object>().Select(a=>((Rigidbody2D)a.GetType().GetField("body").GetValue(a)).position).ToArray();
            snapshot.Invoke(game,null);
            for(int n=0;n<300;n++)
            {
                Vector2 a=Random.insideUnitCircle*35,b=n%3==0?-a:a+Random.insideUnitCircle*3;float radius=Random.Range(.1f,6);
                query.Invoke(game,new object[]{a,b,radius});
                Assert.That(candidates.Distinct().Count(),Is.EqualTo(candidates.Count));
                Assert.That(candidates,Is.Ordered.Descending);
                for(int i=0;i<positions.Length;i++){
                    var ab=b-a;var nearest=a+ab*Mathf.Clamp01(Vector2.Dot(positions[i]-a,ab)/Mathf.Max(.000001f,ab.sqrMagnitude));
                    if(Vector2.Distance(positions[i],nearest)<=radius)Assert.That(candidates.Contains(i),Is.True,"Missing swept hit "+i);
                }
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator SkillPowerSnapshotMatchesLiveValuesAndRefreshesAfterChanges()
        {
            game.TogglePause();var ui=game.Ui;var skill=ui.Items("Skill").First();
            var before=ui.SkillPowerAmount(skill.ability);
            try{
                ui.BeginCombatSnapshot();Assert.That(ui.SkillPowerAmount(skill.ability),Is.EqualTo(before));
                skill.level+=25;Assert.That(ui.SkillPowerAmount(skill.ability),Is.EqualTo(before));
            }finally{ui.EndCombatSnapshot();}
            var after=ui.SkillPowerAmount(skill.ability);Assert.That(after>before,Is.True);
            try{ui.BeginCombatSnapshot();Assert.That(ui.SkillPowerAmount(skill.ability),Is.EqualTo(after));}
            finally{ui.EndCombatSnapshot();}
            yield return null;
        }
        [UnityTest, Timeout(1200000)]
        public IEnumerator EverySkillAndCompanionPerformanceAudit()
        {
            System.IO.Directory.CreateDirectory("artifacts/performance");
            const string report="artifacts/performance/all-skills-companions.csv";
            System.IO.File.WriteAllText(report,"category,id,ability,frames,meanFrameMs,gcBytesPerFrame,created,reused,castsOrShots\n");
            var skills=game.Ui.Items("Skill");var friends=game.Ui.Items("Companion");
            foreach(var item in skills.Concat(friends))item.equipped=false;
            game.Ui.Items("Armor").OrderByDescending(a=>a.rarity).First().discovered=true;
            game.enemyContactDamage=0;Time.timeScale=4;
            foreach(var item in skills.Concat(friends))
            {
                bool companion=item.category=="Companion";
                foreach(var friend in friends)friend.equipped=false;
                game.RestartCombatForStageDebug();
                var bodies=DurableSkillTargets();
                typeof(DoodleIdleGame).GetMethod("TickEnemyArrivals", GrowthPrivate).Invoke(game, new object[] { 1f });
                var actors=(IList)typeof(DoodleIdleGame).GetField("enemies",GrowthPrivate).GetValue(game);
                foreach(var actor in actors){actor.GetType().GetField("hp").SetValue(actor,(GameNumber)1e90);actor.GetType().GetField("maxHp").SetValue(actor,(GameNumber)1e90);}
                for(int n=0;n<bodies.Length;n++){float angle=n*2.399963f;float r=2.5f+(n%12)*.48f;Place(bodies[n],new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r);}
                game.companionsEnabled=companion;
                if(companion){item.discovered=true;item.equipped=true;item.slot=0;}
                else Assert.That(game.DebugCastSkill(item.id),Is.True,item.id);
                float start=Time.time;bool recast=false;int frames=0;double ms=0;long bytes=0;
                int created=game.VisualObjectsCreated,reused=game.VisualObjectsReused;
                using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1))
                {
                    while(Time.time-start<(companion?12:24))
                    {
                        if(!companion&&!recast&&Time.time-start>=12){Assert.That(game.DebugCastSkill(item.id),Is.True);recast=true;}
                        yield return null;frames++;ms+=Time.unscaledDeltaTime*1000;bytes+=gc.LastValue;
                    }
                }
                int count=companion?game.CompanionShotCount(item.id):game.SkillActivationCount(item.ability);
                Assert.That(count,Is.GreaterThanOrEqualTo(companion?1:2),item.id);
                Assert.That(game.PooledVisualCount,Is.LessThanOrEqualTo(2048));
                string row=string.Join(",",item.category,item.id,item.ability,frames,(ms/frames).ToString("F3",System.Globalization.CultureInfo.InvariantCulture),bytes/frames,game.VisualObjectsCreated-created,game.VisualObjectsReused-reused,count);
                System.IO.File.AppendAllText(report,row+"\n");System.IO.File.WriteAllText("Library/Performance.audit-progress",row);
            }
            Assert.That(skills.Count,Is.EqualTo(40));Assert.That(friends.Count,Is.EqualTo(32));
        }

        [UnityTest]
        public IEnumerator MainCombatPerformanceSample()
        {
            game.companionsEnabled=true;
            int slot=0;foreach(var companion in game.Ui.Items("Companion").Take(5)){companion.discovered=true;companion.equipped=true;companion.slot=slot++;}
            string[] abilities={"Golem","FireGolem","MightyDragon","SawSnakes","Dragon","Cloud","RedCloud","IceSnakes"};
            var skills=game.Ui.Items("Skill").Where(s=>abilities.Contains(s.ability)).ToArray();
            foreach(var skill in skills)game.DebugCastSkill(skill.id);
            for(int i=0;i<120;i++)yield return null;
            var handles=new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
            Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
            var names=new List<string>();var detailed=new List<ProfilerRecorder>();var totals=new List<long>();
            foreach(var handle in handles){var d=Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(handle);
                if(d.UnitType!=Unity.Profiling.ProfilerMarkerDataUnit.TimeNanoseconds)continue;
                if(d.Name.StartsWith("Doodle/")||d.Name.Contains("SpriteSkin")||d.Name.Contains("Deform")||d.Name.Contains("Physics2D")||d.Name.Contains("RenderLoop")||d.Name.Contains("Behaviour")||d.Name.Contains("Animator")||d.Name.Contains("TransformAccess")||d.Name.Contains("GC.Collect")||d.Name.Contains("Camera.Render")||d.Name.Contains("Canvas.BuildBatch")||d.Name.Contains("EditorLoop"))
                {names.Add(d.Name);detailed.Add(ProfilerRecorder.StartNew(d.Category,d.Name,1));totals.Add(0);}}
            using(var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1))
            using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1))
            using(var batches=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1))
            {
                var frames=new List<float>();var cpu=new List<float>();long allocated=0,batchSum=0;
                int created=game.VisualObjectsCreated,reused=game.VisualObjectsReused;
                for(int i=0;i<300;i++)
                {
                    if(i%90==0)foreach(var skill in skills)game.DebugCastSkill(skill.id);
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime*1000);cpu.Add(main.LastValue/1000000f);allocated+=gc.LastValue;batchSum+=batches.LastValue;
                    for(int n=0;n<detailed.Count;n++)totals[n]+=detailed[n].LastValue;
                }
                frames.Sort();cpu.Sort();
                string result="{\"frames\":300,\"meanFrameMs\":"+frames.Average().ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"p95FrameMs\":"+frames[284].ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"meanMainThreadMs\":"+cpu.Average().ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"gcBytesPerFrame\":"+(allocated/300)+",\"batchesPerFrame\":"+(batchSum/300)+",\"visualsCreated\":"+(game.VisualObjectsCreated-created)+",\"visualsReused\":"+(game.VisualObjectsReused-reused)+",\"enemyCount\":"+game.EnemyCount+",\"skills\":"+skills.Length+"}";
                System.IO.Directory.CreateDirectory("artifacts/performance");
                string label=System.IO.File.Exists("Library/Performance.label")?System.IO.File.ReadAllText("Library/Performance.label").Trim():"sample";
                System.IO.File.WriteAllText("artifacts/performance/"+label+".json",result);
                System.IO.File.WriteAllLines("artifacts/performance/"+label+"-markers.txt",names.Select((name,i)=>new{name,ms=totals[i]/300000000d}).OrderByDescending(x=>x.ms).Select(x=>x.ms.ToString("F3")+" ms "+x.name));
                foreach(var recorder in detailed)recorder.Dispose();
                Object.Destroy(CaptureFrame("combat-performance-"+label+".png",720,1520));
                Assert.That(Application.targetFrameRate,Is.EqualTo(60));Assert.That(game.VisualObjectsReused,Is.GreaterThan(0));
            }
        }
        [UnityTest]
        public IEnumerator LivePortraitColorsIgnoreWorldLighting()
        {
            game.TogglePause();
            var lights=Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None);
            var enabled=lights.Select(l=>l.enabled).ToArray();
            var lightObject=new GameObject("Portrait lighting regression");
            var light=lightObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            try {
                foreach(var existing in lights)existing.enabled=false;
                light.lightType=UnityEngine.Rendering.Universal.Light2D.LightType.Global;
                light.intensity=1;light.color=Color.white;
                lightObject.layer=0; // World light is outside the portrait camera's layer 31.
                foreach(string page in new[]{"Stats","Pvp"}) {
                    if(game.Ui.ActivePage!=null)game.Ui.ClosePage();
                    UiOpen(page);yield return null;yield return null;yield return null;
                    foreach(var portrait in Object.FindObjectsByType<DoodleIdlePortrait>(FindObjectsSortMode.None)) {
                        portrait.PreviewRig.face.blinking=false;
                        portrait.PreviewRig.animator.speed=0;
                        portrait.RenderNow();
                        var target=portrait.PreviewCamera.targetTexture;
                        var previous=RenderTexture.active;
                        var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                        try {
                            RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
                            System.IO.Directory.CreateDirectory("artifacts/screenshots");
                            System.IO.File.WriteAllBytes("artifacts/screenshots/portrait-lighting-"+portrait.view+".png",pixels.EncodeToPNG());
                            var colors=pixels.GetPixels32();
                            int opaque=colors.Count(c=>c.a>200),colored=colors.Count(c=>c.a>200&&c.r>90&&c.g>65&&c.b>35);
                            Assert.That(opaque,Is.GreaterThan(100),portrait.view+" must contain visible geometry.");
                            Assert.That(colored,Is.GreaterThan(opaque*.15f),portrait.view+" must retain skin/eye/equipment colors instead of a black silhouette.");
                        } finally { RenderTexture.active=previous;Object.Destroy(pixels); }
                    }
                }
            } finally {
                Object.Destroy(lightObject);
                for(int i=0;i<lights.Length;i++)if(lights[i])lights[i].enabled=enabled[i];
            }
        }

        [UnityTest]
        public IEnumerator LivePortraitsMatchEquipmentAndExposeLayoutControls()
        {
            game.TogglePause();UiOpen("Stats");yield return null;yield return null;
            var stats=Object.FindObjectsByType<DoodleIdlePortrait>(FindObjectsSortMode.None).Single(p=>p.view==DoodleIdlePortrait.View.Stats);
            Assert.That(stats.PreviewRig.appearance,Is.SameAs(game.PlayerPortraitRig.appearance));
            Assert.That(stats.PreviewRig.weaponRenderer.sprite,Is.SameAs(game.PlayerPortraitRig.weaponRenderer.sprite));
            Assert.That(stats.GetComponent<RectTransform>().rect.width,Is.EqualTo(248).Within(1));
            float before=stats.PreviewRig.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(stats.PreviewRig.animator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.GreaterThan(before));
            stats.RenderNow();Object.Destroy(CaptureFrame("live-portrait-stats.png",720,1520));
            var settings=stats.settings;float size=settings.statsPortraitSize;float zoom=settings.statsZoom;
            try{
                settings.statsPortraitSize=size+30;settings.statsZoom=zoom*1.2f;
                yield return null;stats.RenderNow();
                Assert.That(stats.GetComponent<RectTransform>().rect.width,Is.EqualTo(size+30).Within(1));
            }finally{settings.statsPortraitSize=size;settings.statsZoom=zoom;}
            var skin=game.Ui.Skins("Appearance").Last();foreach(var other in game.Ui.Skins("Appearance"))other.equipped=false;skin.equipped=true;
            var weaponSkin=game.Ui.Skins("Weapon").Last();foreach(var other in game.Ui.Skins("Weapon"))other.equipped=false;weaponSkin.equipped=true;
            yield return null;yield return null;
            Assert.That(stats.PreviewRig.appearance,Is.SameAs(game.PlayerPortraitRig.appearance));
            Assert.That(stats.PreviewRig.weaponRenderer.sprite,Is.SameAs(game.PlayerPortraitRig.weaponRenderer.sprite));
            game.Ui.ClosePage();UiOpen("Pvp");yield return null;yield return null;
            var podium=Object.FindObjectsByType<DoodleIdlePortrait>(FindObjectsSortMode.None).Where(p=>p.view==DoodleIdlePortrait.View.Pvp).ToArray();
            Assert.That(podium.Length,Is.EqualTo(3));
            foreach(var portrait in podium){Assert.That(portrait.PreviewRig.rigType,Is.EqualTo(game.PlayerPortraitRig.rigType));portrait.RenderNow();}
            Object.Destroy(CaptureFrame("live-portrait-pvp.png",720,1520));
            podium[0].playerLook.appearanceIcon="SkinAppearance_3_0";
            yield return null;Assert.That(podium[0].PreviewRig.appearance,Is.SameAs(DoodleCharacterCatalog.Current.Player(3).appearance));
            game.Ui.ClosePage();yield return new WaitForSecondsRealtime(.35f);
            Assert.That(Object.FindObjectsByType<DoodleIdlePortrait>(FindObjectsSortMode.None).Count(p=>p.view==DoodleIdlePortrait.View.Pvp),Is.Zero);
            Object.Destroy(CaptureFrame("live-portrait-profile.png",720,1520));
        }
    }
}
