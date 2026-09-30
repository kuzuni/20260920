using UnityEngine;
namespace DoodleIdle
{
    public sealed class DoodlePodiumPortraitLayout : MonoBehaviour
    {
        public int rank;
        public RectTransform portrait,label,shadow,podium;
        void LateUpdate()=>Apply();
        public void Apply()
        {
            var s=DoodlePortraitSettings.Current;if(!s||!portrait)return;
            float size=rank==0?s.pvpFirstSize:s.pvpOtherSize;
            float contact=(rank==0?84:rank==1?57:42)-2+s.pvpFootOffset;
            UiKit.Height(podium,s.pvpRowHeight);UiKit.Height(transform,s.pvpRowHeight);
            portrait.sizeDelta=Vector2.one*size;portrait.anchoredPosition=new Vector2(0,contact);
            label.anchoredPosition=new Vector2(0,contact+size+5);
            shadow.anchoredPosition=new Vector2(0,contact+1);shadow.sizeDelta=new Vector2(size*.62f,7);
        }
    }
}
