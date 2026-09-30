using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    // Only the gauge animates. Artwork stays fixed; no combat RNG or artificial wait.
    public sealed class DoodleLoadingMotion : MonoBehaviour
    {
        public RectTransform art, shine;
        public Image fill;
        public float progress;
        public float DisplayedProgress => shown;
        float age, shown;
        RawImage artwork;

        void Update()
        {
            float dt=Time.unscaledDeltaTime;
            age+=dt;
            shown=Mathf.MoveTowards(shown,Mathf.Clamp01(progress),dt*.9f);
            fill.rectTransform.anchorMax=new Vector2(shown,1);
            shine.anchorMin=shine.anchorMax=new Vector2(Mathf.Repeat(age*.55f,1)*1.6f-.3f,.5f);
            shine.anchoredPosition=Vector2.zero;
            if(!artwork) artwork=art.GetComponent<RawImage>();
            if(!artwork.texture) return;
            var texture=artwork.texture;
            var bounds=((RectTransform)art.parent).rect;
            float size=Mathf.Max(bounds.width/texture.width,bounds.height/texture.height);
            var fitted=new Vector2(texture.width,texture.height)*size;
            if(art.sizeDelta!=fitted)art.sizeDelta=fitted;
        }
    }
}
