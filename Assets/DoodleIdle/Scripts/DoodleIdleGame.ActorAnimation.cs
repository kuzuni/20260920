using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        static readonly string[] EnemyArtNames = { "Mushroom", "Bat", "Devil" };
        readonly Sprite[][] enemyWalkFrames = new Sprite[3][];
        readonly List<Sprite> actorAnimationSprites = new List<Sprite>();
        Sprite playerWalkB;

        static Rect OpaqueBounds(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a <= 32) continue;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            if (maxX < minX) throw new InvalidOperationException("Empty actor frame: " + texture.name);
            return new Rect(minX / (float)texture.width, minY / (float)texture.height,
                (maxX - minX + 1) / (float)texture.width, (maxY - minY + 1) / (float)texture.height);
        }
        Sprite ActorSprite(Texture2D texture, Rect normalizedBounds, string name)
        {
            var rect = new Rect(normalizedBounds.x * texture.width, normalizedBounds.y * texture.height,
                normalizedBounds.width * texture.width, normalizedBounds.height * texture.height);
            var sprite = Sprite.Create(texture, rect, Vector2.one * .5f, Mathf.Max(rect.width, rect.height));
            sprite.name = name; actorAnimationSprites.Add(sprite); return sprite;
        }
        Texture2D ActorTexture(string name)
        {
            var texture = Resources.Load<Texture2D>("DoodleIdle/" + name);
            if (!texture) throw new InvalidOperationException("Missing generated actor animation: " + name);
            return texture;
        }
        void LoadActorAnimations()
        {
            playerWalkB = DoodleCharacterCatalog.PlayerPortrait(-1, 1);
            for (int i = 0; i < EnemyArtNames.Length; i++)
            {
                var entry = DoodleCharacterCatalog.Current.Enemy(0, i);
                enemyWalkFrames[i] = entry.portraits;
            }
        }
        int enemyAppearanceSequence;
        void ConfigureActorRig(Actor actor)
        {
            var entry = actor.isPlayer ? DoodleCharacterCatalog.Current.Player(DoodleCharacterCatalog.Costume(Ui ? Ui.EquippedAppearanceIcon : "Player"))
                : DoodleCharacterCatalog.Current.Enemy(CurrentThemeIndex, enemyAppearanceSequence++);
            actor.rigVisual.Configure(entry); actor.rigVisual.Paused = paused;
        }
        void AnimateActorFrames(Actor actor, float dt)
        {
            bool moving = actor.body.simulated && actor.body.linearVelocity.sqrMagnitude > .0025f;
            if (actor.isPlayer) {
                var entry = DoodleCharacterCatalog.Current.Player(DoodleCharacterCatalog.Costume(Ui ? Ui.EquippedAppearanceIcon : "Player"));
                actor.rigVisual.Configure(entry);
                actor.art.flipX = facing.x < 0;
            }
            actor.rigVisual.Moving(moving); actor.rigVisual.Paused = paused;
            Actor gazeTarget = actor.isPlayer ? Closest(player.Position) : player;
            Transform gaze = null;
            if (Alive(gazeTarget) && gazeTarget.root.activeInHierarchy)
                gaze = gazeTarget.rigVisual && gazeTarget.rigVisual.Rig.face
                    ? gazeTarget.rigVisual.Rig.face.transform : gazeTarget.art.transform;
            actor.rigVisual.LookAt(gaze);
            // Follow this actor's actual travel, not the player's facing or target position.
            // Keep the last direction at rest or during vertical motion to avoid left/right flicker.
            if (!actor.isPlayer && moving && Mathf.Abs(actor.body.linearVelocity.x) > .05f)
                actor.art.flipX = actor.body.linearVelocity.x < 0;
            actor.walkClock = moving ? actor.walkClock + dt : 0;
            int frame = moving ? (int)(actor.walkClock * 6 + actor.phase) % 2 : 0;
            actor.art.sprite = DoodleCharacterCatalog.Portrait(actor.rigVisual.Entry, frame);
            actor.art.enabled = false;
        }
        void DisposeActorAnimations()
        {
            foreach (var sprite in actorAnimationSprites) if (sprite) Destroy(sprite);
            actorAnimationSprites.Clear();
        }
    }
}
