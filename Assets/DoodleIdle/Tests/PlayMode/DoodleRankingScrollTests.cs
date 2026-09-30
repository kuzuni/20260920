using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator RankingScrollOnlyKeepsVisibleCharacterRigs()
        {
            var ui=game.Ui;ui.OpenRankings();yield return null;
            var rows=ui.Canvas.GetComponentsInChildren<Transform>().Single(x=>x.name=="Ranking rows");
            var rowType=typeof(DoodleUi).GetNestedType("RankingRow",BindingFlags.NonPublic);
            var draw=typeof(DoodleUi).GetMethod("DrawRankingRow",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int i=0;i<100;i++){
                var entry=System.Activator.CreateInstance(rowType);
                rowType.GetField("rank").SetValue(entry,(i+1).ToString());rowType.GetField("name").SetValue(entry,"Player "+i);
                rowType.GetField("value").SetValue(entry,"100");rowType.GetField("look").SetValue(entry,new DoodlePlayerLook());
                draw.Invoke(ui,new object[]{rows,entry,false});
            }
            Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)rows);
            yield return new WaitForSecondsRealtime(.5f);
            var portraits=rows.GetComponentsInChildren<DoodleRankingPortrait>();Assert.That(portraits.Length,Is.EqualTo(100));
            Assert.That(portraits[0].GetComponent<DoodleIdlePortrait>(),Is.Not.Null);
            Assert.That(rows.GetComponentsInChildren<DoodleIdlePortrait>().Length,Is.InRange(1,20));
            var scroll=rows.GetComponentInParent<ScrollRect>();scroll.verticalNormalizedPosition=0;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(portraits[0].GetComponent<DoodleIdlePortrait>(),Is.Null);
            Assert.That(portraits[99].GetComponent<DoodleIdlePortrait>(),Is.Not.Null);
            Assert.That(rows.GetComponentsInChildren<DoodleIdlePortrait>().Length,Is.InRange(1,20));
            ui.CloseDetail();yield return new WaitForSecondsRealtime(.5f);
            Assert.That(Object.FindObjectsByType<DoodleIdlePortrait>(FindObjectsSortMode.None).Count(x=>x.view==DoodleIdlePortrait.View.Ranking),Is.Zero);
        }
    }
}
