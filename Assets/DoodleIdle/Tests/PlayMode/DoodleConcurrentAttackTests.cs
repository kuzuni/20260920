using System.Collections;
using System.Linq;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    // Read-only tests of the user's original clips through native Animator layers.
    public sealed class DoodleConcurrentAttackTests
    {
        [TestCase("standard")]
        [TestCase("wing")]
        [TestCase("biped")]
        [TestCase("floating")]
        [TestCase("quad")]
        public void PrefabTriggerControlsAttackRangeAndRuntimeResizing(string rigType)
        {
            var actor = new GameObject("Range test enemy", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(DoodleRigVisual));
            var target = new GameObject("Range test player", typeof(Rigidbody2D), typeof(CircleCollider2D));
            try
            {
                actor.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                target.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                var collider = target.GetComponent<CircleCollider2D>(); collider.radius = .61f;
                var visual = actor.GetComponent<DoodleRigVisual>();
                var entry = DoodleCharacterCatalog.Current.entries.First(e => e.group == "Enemies" && e.appearance.rigType == rigType);
                visual.Configure(entry); visual.Rig.animator.enabled = false;
                var range = visual.Rig.attackRange as CircleCollider2D;
                Assert.That(range, Is.Not.Null, rigType);
                Assert.That(range.isTrigger, Is.True);
                Assert.That(range.name, Is.EqualTo("AttackRange"));
                foreach (float size in new[] { 1f, 3f })
                {
                    actor.transform.localScale = Vector3.one * size;
                    float radius = range.radius * Mathf.Abs(range.transform.lossyScale.x);
                    Vector2 center = range.transform.TransformPoint(range.offset);
                    target.transform.position = center + Vector2.right * (radius + collider.radius + .05f);
                    Physics2D.SyncTransforms();
                    Assert.That(visual.Rig.ContainsAttackTarget(collider), Is.False, "Outside must not attack");
                    target.transform.position = center + Vector2.right * (radius + collider.radius - .05f);
                    Physics2D.SyncTransforms();
                    Assert.That(visual.Rig.ContainsAttackTarget(collider), Is.True, "Entering the trigger enables attack");
                    float original = range.radius;
                    range.radius *= .5f; Physics2D.SyncTransforms();
                    Assert.That(visual.Rig.ContainsAttackTarget(collider), Is.False, "Inspector radius edits immediately change reach");
                    range.radius = original; Physics2D.SyncTransforms();
                    Assert.That(visual.Rig.ContainsAttackTarget(collider), Is.True);
                    range.enabled = false;
                    Assert.That(visual.Rig.ContainsAttackTarget(collider), Is.False);
                    range.enabled = true;
                }
                var companion = DoodleCharacterCatalog.Current.entries.FirstOrDefault(e => e.group == "Companions" && e.prefab == entry.prefab);
                if (companion != null)
                {
                    visual.Configure(companion);
                    Assert.That(visual.Rig.attackRange.enabled, Is.False, "Shared companion rigs must not enable enemy sensors");
                    visual.Configure(entry);
                    Assert.That(visual.Rig.attackRange.enabled, Is.True);
                }
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(target); }
        }

        [UnityTest]
        public IEnumerator LiveEnemiesCompleteAttacksWhileTakingRepeatedDamage()
        {
            DoodlePrefs.UseAccount("live-enemy-attack-" + System.Guid.NewGuid());
            var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/DoodleIdle/DoodleIdle.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
#else
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("DoodleIdle", UnityEngine.SceneManagement.LoadSceneMode.Additive);
#endif
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("DoodleIdle");
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            try
            {
                yield return null;
                var game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DoodleIdleGame>()).Single();
                game.autoPlay = false; game.moveSpeed = 0; game.enemyContactDamage = 1;
                game.basicSkillsEnabled = game.extraSkillsEnabled = game.summonSkillsEnabled = game.companionsEnabled = false;
                game.refillBelow = 0;
                foreach (var item in game.Ui.Items("Skill")) item.equipped = false;
                var tuning = game.Ui.ReadBalanceTuning(); tuning.enemyStartingDamage = 64;
                game.Ui.ApplyBalanceTuning(tuning);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", flags).GetValue(game);
                foreach (var actor in actors)
                {
                    var body = (Rigidbody2D)actor.GetType().GetField("body").GetValue(actor);
                    body.simulated = false; body.position = new Vector2(100, 100);
                }
                var target = actors[0]; var type = target.GetType();
                var enemyBody = (Rigidbody2D)type.GetField("body").GetValue(target);
                var visual = (DoodleRigVisual)type.GetField("rigVisual").GetValue(target);
                var player = game.GetComponentsInChildren<DoodleRigVisual>().Single(v => v.Entry.group == "Player");
                var playerBody = player.GetComponentInParent<Rigidbody2D>();
                var damage = typeof(DoodleIdleGame).GetMethod("Damage", flags);
                foreach (string rigType in new[] { "standard", "wing", "biped", "floating", "quad" })
                {
                    visual.Configure(DoodleCharacterCatalog.Current.entries.First(e => e.group == "Enemies" && e.appearance.rigType == rigType));
                    type.GetField("hp").SetValue(target, (GameNumber)1e12);
                    type.GetField("maxHp").SetValue(target, (GameNumber)1e12);
                    type.GetField("meleeCooldown").SetValue(target, 0f);
                    enemyBody.simulated = true;
                    enemyBody.position = playerBody.position + Vector2.right * 1.3f;
                    int before = game.PlayerContactHits;
                    bool sawAttack = false;
                    float until = Time.time + 3;
                    while (Time.time < until)
                    {
                        // Exercise production FixedUpdate and frame-driven Animator,
                        // including the frequent hits from equipped skills/companions.
                        damage.Invoke(game, new object[] { target, 1f, Vector2.zero });
                        yield return new WaitForSeconds(.05f);
                        int layer = Mathf.Max(0, visual.Rig.animator.GetLayerIndex("Upper Body"));
                        sawAttack |= visual.Rig.animator.GetCurrentAnimatorStateInfo(layer).IsName("Attack");
                    }
                    Assert.That(sawAttack, Is.True, rigType + " must visibly enter Attack during actual combat");
                    Assert.That(game.PlayerContactHits, Is.GreaterThan(before), rigType + " must reach its impact event despite repeated incoming hits");
                }
            }
            finally
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
                foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
                DoodlePrefs.DeleteAccountCache();
            }
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        }

        [TestCase("Player_Standard")]
        [TestCase("Character_standard")]
        [TestCase("Character_wing")]
        [TestCase("Character_biped")]
        [TestCase("Character_floating")]
        [TestCase("Character_quad")]
        public void AuthoredAttackEventFiresOnceAndCancelsOnHitOrPoolReturn(string prefabName)
        {
            var source = DoodleCharacterCatalog.Current.entries.Select(e => e.prefab).First(p => p.name == prefabName);
            var rig = Object.Instantiate(source);
            try
            {
                var clip = rig.animator.runtimeAnimatorController.animationClips.First(c => c.name == "Attack");
                Assert.That(clip.events.Count(e => e.functionName == "OnAttackImpact"), Is.EqualTo(1));
                float impactTime = clip.events.Single(e => e.functionName == "OnAttackImpact").time;
                rig.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                rig.animator.Update(0);
                int impacts = 0;
                Assert.That(rig.TryAttack(() => impacts++), Is.True);
                Assert.That(rig.TryAttack(() => impacts += 100), Is.False, "Do not restart the wind-up each physics tick");
                rig.animator.Update(.01f);
                rig.animator.Update(impactTime * .4f);
                Assert.That(impacts, Is.Zero, "Starting an attack must not apply damage");
                rig.animator.speed = 0;
                rig.animator.Update(2);
                Assert.That(impacts, Is.Zero, "Pause must hold the impact");
                rig.animator.speed = 1;
                for (int i = 0; i < 120; i++) rig.animator.Update(.01f);
                Assert.That(impacts, Is.EqualTo(1), "The actual Animator must dispatch the clip event");
                rig.OnAttackImpact();
                Assert.That(impacts, Is.EqualTo(1), "Duplicate events must not repeat damage");
                Assert.That(rig.TryAttack(() => impacts++), Is.True);
                rig.animator.Update(.02f); rig.Hit();
                for (int i = 0; i < 120; i++) rig.animator.Update(.01f);
                Assert.That(impacts, Is.EqualTo(1));
                Assert.That(rig.TryAttack(() => impacts++), Is.True);
                rig.animator.Update(.02f); rig.gameObject.SetActive(false); rig.gameObject.SetActive(true);
                for (int i = 0; i < 120; i++) rig.animator.Update(.01f);
                Assert.That(impacts, Is.EqualTo(1), "Pool reuse must not retain the previous actor's hit");
            }
            finally { Object.DestroyImmediate(rig.gameObject); }
        }

        [TestCase("Player_Standard", false)]
        [TestCase("Player_Standard", true)]
        [TestCase("Character_standard", false)]
        [TestCase("Character_standard", true)]
        [TestCase("Character_wing", false)]
        [TestCase("Character_wing", true)]
        public void PlayerBlendsUpperBodyWhileMonstersPlayTheWholeAttack(string prefabName, bool moving)
        {
            var source = DoodleCharacterCatalog.Current.entries.Select(e => e.prefab).First(p => p.name == prefabName);
            string limb = source.rigType == "wing" ? "날개" : "팔";
            var control = Object.Instantiate(source); var combined = Object.Instantiate(source); var sample = Object.Instantiate(source);
            try
            {
                var clip = source.animator.runtimeAnimatorController.animationClips.First(c => c.name == "Attack");
                int upper = combined.animator.GetLayerIndex("Upper Body");
                bool blend = prefabName == "Player_Standard";
                Assert.That(upper, blend ? Is.GreaterThan(0) : Is.EqualTo(-1));
                int attackLayer = blend ? upper : 0;
                if (blend) Assert.That(combined.animator.GetLayerWeight(upper), Is.EqualTo(1));
                foreach (var rig in new[] { control, combined })
                {
                    rig.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    rig.SetMoving(moving); rig.animator.Update(0);
                }
                for (int i = 0; i < 20; i++) { control.animator.Update(.05f); combined.animator.Update(.05f); }
                combined.Attack();
                for (int i = 0; i < 4; i++) { control.animator.Update(.04f); combined.animator.Update(.04f); }
                if (blend) Assert.That(combined.animator.GetCurrentAnimatorStateInfo(0).IsName(moving ? "Move" : "Idle"), Is.True);
                Assert.That(combined.animator.GetCurrentAnimatorStateInfo(attackLayer).IsName("Attack"), Is.True);
                clip.SampleAnimation(sample.gameObject, combined.animator.GetCurrentAnimatorStateInfo(attackLayer).normalizedTime * clip.length);
                var attackBones = combined.skeleton.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                var reference = sample.skeleton.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                bool armChanged = false;
                foreach (var bone in control.skeleton.GetComponentsInChildren<Transform>())
                {
                    var mixed = attackBones[bone.name];
                    if (blend && (bone.name.StartsWith("다리") || bone.name == "몸통"))
                    {
                        Assert.That(Vector3.Distance(bone.localPosition, mixed.localPosition), Is.LessThan(.001f), bone.name);
                        Assert.That(Quaternion.Angle(bone.localRotation, mixed.localRotation), Is.LessThan(.05f), bone.name);
                    }
                    // Only compare rotations actually keyed by this clip. Unkeyed tip
                    // bones retain locomotion; sampling Attack cannot supply their pose.
                    bool keyedLimb = !blend || bone.name.StartsWith(limb) || bone.name == "머리";
#if UNITY_EDITOR
                    string path = UnityEditor.AnimationUtility.CalculateTransformPath(bone, control.transform);
                    var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip).Where(b => b.path == path).ToArray();
                    if ((!blend || bone.name == "머리") && bindings.Any(b => b.propertyName.StartsWith("m_LocalPosition")))
                        Assert.That(Vector3.Distance(reference[bone.name].localPosition, mixed.localPosition), Is.LessThan(.001f), "Attack position: " + bone.name);
                    keyedLimb &= UnityEditor.AnimationUtility.GetCurveBindings(clip).Any(b =>
                        (b.propertyName.Contains("Euler") || b.propertyName.Contains("Rotation"))
                        && b.path == UnityEditor.AnimationUtility.CalculateTransformPath(bone, control.transform));
#endif
                    if (keyedLimb)
                    {
                        Assert.That(Quaternion.Angle(reference[bone.name].localRotation, mixed.localRotation), Is.LessThan(.05f), "Attack must drive keyed limb " + bone.name + " at full weight");
                        armChanged |= Quaternion.Angle(bone.localRotation, mixed.localRotation) > 1;
                    }
                }
                Assert.That(armChanged, Is.True);
                control.SetMoving(!moving); combined.SetMoving(!moving);
                for (int i = 0; i < 4; i++) { control.animator.Update(.04f); combined.animator.Update(.04f); }
                if (blend)
                    foreach (var bone in control.skeleton.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("다리")))
                        Assert.That(Quaternion.Angle(bone.localRotation, attackBones[bone.name].localRotation), Is.LessThan(.05f));
                for (int i = 0; i < 120; i++) combined.animator.Update(.01f);
                Assert.That(combined.animator.GetCurrentAnimatorStateInfo(attackLayer).IsName(blend ? "Locomotion" : moving ? "Idle" : "Move"), Is.True);
            }
            finally { Object.DestroyImmediate(control.gameObject); Object.DestroyImmediate(combined.gameObject); Object.DestroyImmediate(sample.gameObject); }
        }

        [Test]
        public void OnlyPlayerAppearancesUseUpperBodyBlendingAndIncludeTheHead()
        {
            var entries = DoodleCharacterCatalog.Current.entries.Where(e => e.appearance.rigType == "standard" || e.appearance.rigType == "wing").ToArray();
            Assert.That(entries.Select(e => e.prefab).Distinct().Count(), Is.EqualTo(3));
            foreach (var entry in entries)
            {
                var instance = Object.Instantiate(entry.prefab);
                try
                {
                    instance.SetAppearance(entry.appearance);
                    bool player = entry.group == "Player";
                    Assert.That(instance.animator.GetLayerIndex("Upper Body"), player ? Is.GreaterThan(0) : Is.EqualTo(-1), entry.id);
                    Assert.That(instance.animator.parameters.Any(p => p.name == "UpperAttack"), Is.EqualTo(player), entry.id);
#if UNITY_EDITOR
                    if (player)
                    {
                        var mask = ((UnityEditor.Animations.AnimatorController)instance.animator.runtimeAnimatorController).layers[1].avatarMask;
                        var head = instance.skeleton.GetComponentsInChildren<Transform>().Single(t => t.name == "머리");
                        string headPath = UnityEditor.AnimationUtility.CalculateTransformPath(head, instance.transform);
                        int index = Enumerable.Range(0, mask.transformCount).Single(i => mask.GetTransformPath(i) == headPath);
                        Assert.That(mask.GetTransformActive(index), Is.True, "Player Attack must include authored head curves");
                    }
#endif
                }
                finally { Object.DestroyImmediate(instance.gameObject); }
            }
        }

        [UnityTest]
        public IEnumerator EnemyAndPlayerDamageWaitForAnimationEventsAndHpBarsAreConfigurable()
        {
            Assert.That(DoodlePrefs.HasAccount, Is.False);
            DoodlePrefs.UseAccount("biped-attack-test-" + System.Guid.NewGuid());
            var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/DoodleIdle/DoodleIdle.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
#else
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("DoodleIdle", UnityEngine.SceneManagement.LoadSceneMode.Additive);
#endif
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("DoodleIdle");
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            try
            {
                yield return null; yield return null;
                var game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DoodleIdleGame>()).Single();
                Assert.That(game.Ready, Is.True);
                game.enabled = false; game.enemyContactDamage = 1;
                var tuning = game.Ui.ReadBalanceTuning(); tuning.enemyStartingDamage = 64;
                game.Ui.ApplyBalanceTuning(tuning); // Production stage 1 is intentionally harmless.
                var actors = game.GetComponentsInChildren<DoodleRigVisual>();
                var player = actors.Single(v => v.Entry.group == "Player");
                var enemies = actors.Where(v => v.Entry.group == "Enemies").ToArray();
                foreach (var enemy in enemies)
                {
                    var body = enemy.GetComponentInParent<Rigidbody2D>();
                    body.simulated = false; body.position = new Vector2(100, 100);
                }
                var target = enemies[0];
                target.GetComponentInParent<Rigidbody2D>().simulated = true;
                target.GetComponentInParent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var tick = typeof(DoodleIdleGame).GetMethod("TickPlayerContactDamage", flags);
                var enemyActors = (IList)typeof(DoodleIdleGame).GetField("enemies", flags).GetValue(game);
                var targetActor = enemyActors.Cast<object>().Single(a => (DoodleRigVisual)a.GetType().GetField("rigVisual").GetValue(a) == target);
                foreach (string type in new[] { "standard", "wing", "biped", "floating", "quad" })
                {
                    target.Configure(DoodleCharacterCatalog.Current.entries.First(e => e.group == "Enemies" && e.appearance.rigType == type));
                    target.GetComponentInParent<Rigidbody2D>().position = player.GetComponentInParent<Rigidbody2D>().position;
                    target.Rig.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    // Isolate this attack from combat that ran while the scene loaded.
                    target.Rig.CancelAttack(); target.Rig.animator.Rebind();
                    target.Rig.animator.Update(0);
                    int hits = game.PlayerContactHits;
                    tick.Invoke(game, new object[] { 2f });
                    Assert.That(game.PlayerContactHits, Is.EqualTo(hits), "No damage before the event: " + type);
                    target.Rig.animator.Update(.1f);
                    int layer = Mathf.Max(0, target.Rig.animator.GetLayerIndex("Upper Body"));
                    Assert.That(target.Rig.animator.GetCurrentAnimatorStateInfo(layer).IsName("Attack")
                        || target.Rig.animator.GetNextAnimatorStateInfo(layer).IsName("Attack"), Is.True, type);
                    for (int i = 0; i < 120; i++) target.Rig.animator.Update(.01f);
                    Assert.That(game.PlayerContactHits, Is.EqualTo(hits + 1), type);
                    Assert.That(player.Rig.face.IsHurt, Is.True, "The real contact-damage path must change the player's face: " + type);
                    target.Rig.OnAttackImpact();
                    Assert.That(game.PlayerContactHits, Is.EqualTo(hits + 1));
                }
                // A player who leaves melee range during the wind-up avoids the hit.
                int previousHits = game.PlayerContactHits;
                tick.Invoke(game, new object[] { 2f });
                target.GetComponentInParent<Rigidbody2D>().position = new Vector2(100, 100);
                for (int i = 0; i < 120; i++) target.Rig.animator.Update(.01f);
                Assert.That(game.PlayerContactHits, Is.EqualTo(previousHits));

                player.Rig.CancelAttack(); player.Rig.animator.Rebind(); player.Rig.animator.Update(0);
                var shots = (IList)typeof(DoodleIdleGame).GetField("shots", flags).GetValue(game);
                int shotCount = shots.Count;
                Assert.That((bool)typeof(DoodleIdleGame).GetMethod("BeginPlayerAttack", flags).Invoke(game, new object[] { Vector2.right }), Is.True);
                Assert.That(shots.Count, Is.EqualTo(shotCount));
                player.Rig.animator.Update(.2f);
                Assert.That(shots.Count, Is.EqualTo(shotCount));
                for (int i = 0; i < 120; i++) player.Rig.animator.Update(.01f);
                Assert.That(shots.Count, Is.EqualTo(shotCount + 1));
                Assert.That((bool)typeof(DoodleIdleGame).GetMethod("BeginPlayerAttack", flags).Invoke(game, new object[] { Vector2.right }), Is.True);
                game.SetBasicAttackEnabled(false);
                int disabledCount = shots.Count;
                for (int i = 0; i < 120; i++) player.Rig.animator.Update(.01f);
                Assert.That(shots.Count, Is.EqualTo(disabledCount), "Disabling basic attacks must cancel the pending slash");

                game.enemyContactDamage = 0;
                target.GetComponentInParent<Rigidbody2D>().position = player.GetComponentInParent<Rigidbody2D>().position;
                tick.Invoke(game, new object[] { 2f });
                target.Rig.animator.Update(.1f);
                Assert.That(target.Rig.animator.GetCurrentAnimatorStateInfo(0).IsName("Attack")
                    || target.Rig.animator.GetNextAnimatorStateInfo(0).IsName("Attack"), Is.True, "Harmless enemies still animate");
                for (int i = 0; i < 120; i++) target.Rig.animator.Update(.01f);
                Assert.That(game.PlayerContactHits, Is.EqualTo(previousHits), "Harmless enemies do not fake damage");

                game.playerHealthBarOffset = new Vector2(.2f, 2.8f);
                game.enemyHealthBarOffset = new Vector2(-.3f, 3.2f);
                var playerActor = typeof(DoodleIdleGame).GetField("player", flags).GetValue(game);
                var refresh = typeof(DoodleIdleGame).GetMethod("RefreshHealthBar", flags);
                foreach (var actor in new[] { playerActor, targetActor })
                {
                    refresh.Invoke(game, new[] { actor });
                    var back = (SpriteRenderer)actor.GetType().GetField("healthBack").GetValue(actor);
                    var fill = (SpriteRenderer)actor.GetType().GetField("healthFill").GetValue(actor);
                    Vector2 expected = actor == playerActor ? game.playerHealthBarOffset : game.enemyHealthBarOffset;
                    Assert.That((Vector2)back.transform.localPosition, Is.EqualTo(expected));
                    Assert.That(fill.transform.localPosition.y, Is.EqualTo(expected.y));
                }
                var targetType = targetActor.GetType();
                var hpBack = (SpriteRenderer)targetType.GetField("healthBack").GetValue(targetActor);
                var hpFill = (SpriteRenderer)targetType.GetField("healthFill").GetValue(targetActor);
                targetType.GetField("hp").SetValue(targetActor, (GameNumber)1000000);
                targetType.GetField("maxHp").SetValue(targetActor, (GameNumber)1000000);
                refresh.Invoke(game, new[] { targetActor });
                Assert.That(hpBack.enabled || hpFill.enabled, Is.False, "Unhurt enemies must not display an HP bar");
                typeof(DoodleIdleGame).GetMethod("Damage", flags).Invoke(game, new object[] { targetActor, 1f, Vector2.zero });
                Assert.That(hpBack.enabled && hpFill.enabled, Is.True, "Taking damage reveals the enemy HP bar");
                targetType.GetField("isBoss").SetValue(targetActor, true);
                refresh.Invoke(game, new[] { targetActor });
                Assert.That(((SpriteRenderer)targetType.GetField("healthBack").GetValue(targetActor)).enabled, Is.False);
                Assert.That(((SpriteRenderer)targetType.GetField("healthFill").GetValue(targetActor)).enabled, Is.False);
                targetType.GetField("isBoss").SetValue(targetActor, false);
                targetType.GetField("hp").SetValue(targetActor, targetType.GetField("maxHp").GetValue(targetActor));
                refresh.Invoke(game, new[] { targetActor });
                Assert.That(hpBack.enabled || hpFill.enabled, Is.False, "Respawned full-health enemies must start with the bar hidden again");
            }
            finally
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
                foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
                DoodlePrefs.DeleteAccountCache();
            }
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ActualAnimatorFrameSwingsArmWhileLegsMoveAndHonorsPause()
        {
            var rig = Object.Instantiate(DoodleCharacterCatalog.Current.Player(-1).prefab);
            var sample = Object.Instantiate(rig); sample.gameObject.SetActive(false);
            try
            {
                rig.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var arm = rig.skeleton.GetComponentsInChildren<Transform>().Single(t => t.name == "팔2");
                var original = arm.localRotation;
                var clip = rig.animator.runtimeAnimatorController.animationClips.First(c => c.name == "Attack");
                int upper = rig.animator.GetLayerIndex("Upper Body");
                rig.SetMoving(true); yield return new WaitForSeconds(.2f); rig.Attack();
                float greatestAngle = 0;
                for (int i = 0; i < 20; i++)
                {
                    yield return new WaitForSeconds(.02f);
                    greatestAngle = Mathf.Max(greatestAngle, Quaternion.Angle(original, arm.localRotation));
                    var state = rig.animator.GetCurrentAnimatorStateInfo(upper);
                    Assert.That(rig.animator.GetCurrentAnimatorStateInfo(0).IsName("Move"), Is.True);
                    if (state.IsName("Attack"))
                    {
                        clip.SampleAnimation(sample.gameObject, state.normalizedTime * clip.length);
                        var reference = sample.skeleton.GetComponentsInChildren<Transform>(true).Single(t => t.name == "팔2");
                        Assert.That(Quaternion.Angle(reference.localRotation, arm.localRotation), Is.LessThan(.1f));
                    }
                }
                Assert.That(greatestAngle, Is.GreaterThan(25), "Attacking arm must visibly swing in actual frame updates");
                rig.Attack(); yield return new WaitForSeconds(.1f);
                rig.animator.speed = 0; yield return null;
                var before = arm.localRotation; yield return new WaitForSeconds(.1f);
                Assert.That(Quaternion.Angle(before, arm.localRotation), Is.LessThan(.05f));
                rig.animator.speed = 1; rig.gameObject.SetActive(false); rig.gameObject.SetActive(true);
                rig.SetMoving(false); rig.Attack(); yield return new WaitForSeconds(.1f);
                Assert.That(rig.animator.GetCurrentAnimatorStateInfo(upper).IsName("Attack"), Is.True);
                rig.Hit(); yield return new WaitForSeconds(.1f);
                Assert.That(rig.animator.GetCurrentAnimatorStateInfo(upper).IsName("Locomotion"), Is.True);
            }
            finally { Object.DestroyImmediate(rig.gameObject); Object.DestroyImmediate(sample.gameObject); }
        }
    }
}
