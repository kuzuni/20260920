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
            var playerTexture = ActorTexture("PlayerWalkB");
            playerWalkB = ActorSprite(playerTexture, OpaqueBounds(playerTexture), "PlayerWalkB");
            for (int i = 0; i < EnemyArtNames.Length; i++)
            {
                string name = EnemyArtNames[i];
                var a = ActorTexture(name + "A"); var b = ActorTexture(name + "B");
                Rect first = OpaqueBounds(a), second = OpaqueBounds(b);
                // Use a shared crop for both poses so folding wings does not rescale the whole head.
                Rect union = Rect.MinMaxRect(Mathf.Min(first.xMin, second.xMin), Mathf.Min(first.yMin, second.yMin),
                    Mathf.Max(first.xMax, second.xMax), Mathf.Max(first.yMax, second.yMax));
                enemyWalkFrames[i] = new[] { ActorSprite(a, union, name + "A"), ActorSprite(b, union, name + "B") };
            }
        }
        void AnimateActorFrames(Actor actor, float dt)
        {
            bool moving = actor.body.simulated && actor.body.linearVelocity.sqrMagnitude > .0025f;
            actor.walkClock = moving ? actor.walkClock + dt : 0;
            int frame = moving ? (int)(actor.walkClock * 6 + actor.phase) % 2 : 0;
            Sprite art = actor.isPlayer ? (frame == 0 ? sprites[0] : playerWalkB) : enemyWalkFrames[actor.kind][frame];
            if (actor.art.sprite != art) SetSpriteArt(actor.art, art);
        }
        void DisposeActorAnimations()
        {
            foreach (var sprite in actorAnimationSprites) if (sprite) Destroy(sprite);
            actorAnimationSprites.Clear();
        }
    }
}
