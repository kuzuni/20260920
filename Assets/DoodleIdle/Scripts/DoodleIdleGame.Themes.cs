using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public static readonly string[] ThemeNames = { "초원", "사막", "숲", "늪", "화산", "해변", "수정 동굴", "황혼", "고대 유적", "빙하" };
        static readonly string[] ThemeResources = { "Meadow", "Desert", "Forest", "Swamp", "Volcano", "Coast", "Crystal", "Twilight", "Ruins", "Glacier" };
        static readonly string[][] ThemeEnemies = {
            new[] { "새싹 슬라임", "들쥐", "분홍 버섯" }, new[] { "선인장", "사막 여우", "모래 풍뎅이" },
            new[] { "도토리", "숲 부엉이", "다람쥐" }, new[] { "이끼", "독버섯", "진흙 슬라임" },
            new[] { "용암 슬라임", "불꽃 꼬마", "불도마뱀" }, new[] { "산호 게", "복어", "소라게" },
            new[] { "수정 슬라임", "보석 박쥐", "수정 거북" }, new[] { "황혼 유령", "달빛 부엉이", "밤 나방" },
            new[] { "돌 수호자", "미라 고양이", "황금 풍뎅이" }, new[] { "얼음 슬라임", "눈 여우", "펭귄" }
        };
        readonly Dictionary<Sprite,bool> sourceFrameFacesLeft=new Dictionary<Sprite,bool>();
        readonly Dictionary<int, Sprite[][]> themeFrames = new Dictionary<int, Sprite[][]>();
        readonly Dictionary<int, Sprite> themeGrounds = new Dictionary<int, Sprite>();
        readonly List<SpriteRenderer> groundTiles = new List<SpriteRenderer>();
        int activeTheme = -1;
        public static int ThemeIndexForStage(int stage) => (Math.Max(1, stage) - 1) / 100 % ThemeNames.Length;
        public int CurrentThemeIndex => ThemeIndexForStage(Ui ? Ui.MainStage + 1 : 1);
        public string CurrentThemeName => ThemeNames[CurrentThemeIndex];

        [Serializable] sealed class ThemeAtlasLayout { public SpriteRegion[] frames; }
        [Serializable] sealed class SpriteRegion { public float x,y,width,height; public bool facesLeft; public string resource; }
        Sprite[][] LoadThemeFrames(int index)
        {
            if (themeFrames.TryGetValue(index, out var cached)) return cached;
            string path = "DoodleIdle/Themes/" + ThemeResources[index];
            var texture = Resources.Load<Texture2D>(path);
            var layout = JsonUtility.FromJson<ThemeAtlasLayout>(Resources.Load<TextAsset>(path + "Layout").text);
            var frames = new Sprite[3][];
            for (int kind = 0; kind < 3; kind++)
            {
                frames[kind] = new Sprite[2];
                for (int pose = 0; pose < 2; pose++)
                {
                    var region = layout.frames[pose * 3 + kind];
                    var rect = new Rect(region.x,region.y,region.width,region.height);
                    var frameTexture = string.IsNullOrEmpty(region.resource) ? texture : Resources.Load<Texture2D>("DoodleIdle/Themes/" + region.resource);
                    var frame = Sprite.Create(frameTexture, rect, Vector2.one * .5f, Mathf.Max(rect.width, rect.height));
                    frame.name = ThemeResources[index] + kind + (pose == 0 ? "A" : "B");
                    sourceFrameFacesLeft[frame]=region.facesLeft;
                    actorAnimationSprites.Add(frame); frames[kind][pose] = frame;
                }
            }
            themeFrames[index] = frames; return frames;
        }
        void NormalizeEnemyFrame(Actor actor,Sprite frame)
        {
            // Normalize EACH source pose to face right, retaining the original drawing/view angle.
            // Runtime flipX now has one meaning for every species: true = walking left.
            bool left=sourceFrameFacesLeft.TryGetValue(frame,out var value)&&value;
            var scale=actor.art.transform.localScale;scale.x=Mathf.Abs(scale.x)*(left?-1:1);
            actor.art.transform.localScale=scale;
        }
        Sprite ThemeGround(int index)
        {
            if (themeGrounds.TryGetValue(index, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>("DoodleIdle/Themes/Grounds");
            float width = texture.width / 5f, height = texture.height / 2f;
            // Stay inside each atlas cell so bilinear sampling cannot pull in the neighboring theme.
            var rect = new Rect(index % 5 * width + 2, (1 - index / 5) * height + 2, width - 4, height - 4);
            var sprite = Sprite.Create(texture, rect, Vector2.one * .5f, rect.width / 13f);
            sprite.name = "Ground " + ThemeResources[index]; themeGrounds[index] = sprite; return sprite;
        }
        void ApplyStageTheme()
        {
            int index = CurrentThemeIndex;
            if (activeTheme == index) return;
            activeTheme = index;
            var frames = LoadThemeFrames(index);
            for (int kind = 0; kind < 3; kind++) enemyWalkFrames[kind] = frames[kind];
            foreach (var tile in groundTiles)
            {
                var ground = ThemeGround(index); SetSpriteArt(tile, ground);
                tile.transform.localScale = new Vector3(1, ground.rect.width / ground.rect.height, 1);
            }
        }
        void DisposeThemes()
        {
            foreach (var ground in themeGrounds.Values) if (ground) Destroy(ground);
            themeGrounds.Clear(); themeFrames.Clear(); sourceFrameFacesLeft.Clear(); groundTiles.Clear();
        }
    }
}
