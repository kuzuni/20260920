using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        readonly Image[] skillBars = new Image[22];
        float skillBarWidth;
        void BuildSkillGrid(Transform parent)
        {
            string[] names = { "방망이 검기", "대시", "바나나", "돌멩이", "화살 10발", "탱탱볼", "불꽃 3발", "드론", "나선 지렁이",
                "5방향 뱀", "산탄 20발", "수호검", "설치 대포", "굴러라 오이", "추적 뱀", "번개 구름", "원형 불꽃", "모래 뿌리기", "화염 드래곤", "빨간 검기 5연발", "화염병", "음파" };
            Sprite[] icons = { sprites[4], sprites[0], sprites[5], sprites[6], skillArt[0], skillArt[1], skillArt[2], skillArt[3], skillArt[5],
                summonArt["SnakeHead"], summonArt["Shotgun"], summonArt["GuardianSword"], summonArt["Cannon"], summonArt["Cucumber"], summonArt["PurpleSnakeHead"], summonArt["StormCloud"], skillArt[2], summonArt["SandPuff"], summonArt["DragonHead"], summonArt["RedSlashA"], summonArt["Molotov"], summonArt["SoundWave"] };
            int columns = portraitHud ? 5 : 11, rows = Mathf.CeilToInt(names.Length / (float)columns);
            float cell = portraitHud ? 136 : 126, row = portraitHud ? 70 : 76;
            float height = rows * row + 64;
            skillBarWidth = cell - 28;
            var dock = Panel(parent, "All skills", Vector2.zero, new Vector2(1, 0), new Vector2(portraitHud ? 18 : 22, 18), new Vector2(portraitHud ? -18 : -22, 18 + height));
            for (int i = 0; i < names.Length; i++)
            {
                float x = i % columns * cell, y = 60 + (rows - 1 - i / columns) * row;
                var go = new GameObject(names[i] + " icon", typeof(RectTransform), typeof(Image)); go.transform.SetParent(dock, false);
                var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(x + (cell - 38) / 2, y + 27); rect.sizeDelta = Vector2.one * 38;
                var icon = go.GetComponent<Image>(); icon.sprite = icons[i]; icon.preserveAspect = true;
                Label(dock, names[i], portraitHud ? 21 : 18, new Vector2(x, y + 2), new Vector2(cell, 27), TextAnchor.MiddleCenter);
                var bar = new GameObject(names[i] + " cooldown", typeof(RectTransform), typeof(Image)); bar.transform.SetParent(dock, false);
                var r = bar.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
                r.anchoredPosition = new Vector2(x + 14, y); r.sizeDelta = new Vector2(skillBarWidth, 3);
                skillBars[i] = bar.GetComponent<Image>(); skillBars[i].color = i < 9 ? new Color(.57f, .65f, .4f) : new Color(.8f, .5f, .32f);
            }
            pauseText = Button(dock, "일시정지", new Vector2(portraitHud ? -668 : -402, 10), new Vector2(portraitHud ? 320 : 186, 38), TogglePause);
            Button(dock, "다시 시작", new Vector2(portraitHud ? -334 : -202, 10), new Vector2(portraitHud ? 320 : 186, 38), ResetGame);
            if (!portraitHud) Label(dock, "모든 스킬 자동 발동", 18, new Vector2(20, 14), new Vector2(380, 32), TextAnchor.MiddleLeft);
            modeText = Label(parent, "", portraitHud ? 21 : 15, new Vector2(24, height + 25), new Vector2(portraitHud ? 680 : 890, 28), TextAnchor.MiddleLeft);
            waveText = Label(parent, "", portraitHud ? 20 : 15, new Vector2(portraitHud ? 24 : -400, portraitHud ? height + 53 : height + 25), new Vector2(portraitHud ? 680 : 370, 28), portraitHud ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, portraitHud ? Vector2.zero : new Vector2(1, 0));
        }
        void SkillBar(int index, float clock, float interval)
        {
            if (skillBars[index]) skillBars[index].rectTransform.sizeDelta = new Vector2(skillBarWidth * Mathf.Clamp01(1 - clock / Mathf.Max(.01f, interval)), 3);
        }
        void UpdateSkillCooldowns()
        {
            SkillBar(0, attackTimer, attackInterval); SkillBar(1, dashTimer, dashInterval); SkillBar(2, 0, 1); SkillBar(3, stoneTimer, stoneInterval);
            SkillBar(4, arrowClock, arrowInterval); SkillBar(5, ballClock, ballInterval); SkillBar(6, fireClock, fireInterval); SkillBar(7, droneClock, droneInterval); SkillBar(8, wormClock, wormInterval);
            for (int i = 0; i < summonClocks.Length; i++) SkillBar(9 + i, summonClocks[i], SummonInterval(i));
        }
    }
}
