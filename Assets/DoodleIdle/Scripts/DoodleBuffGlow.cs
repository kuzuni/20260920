using UnityEngine;
using UnityEngine.UI;
namespace DoodleIdle
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleBuffGlow : MaskableGraphic
    {
        float nextFrame;
        public static DoodleBuffGlow Create(Transform icon)
        {
            var rect=UiKit.Rect(icon.parent,"Active buff glow");UiKit.Stretch(rect,-25,-25,-25,-25);rect.SetSiblingIndex(icon.GetSiblingIndex());
            var layout=rect.gameObject.AddComponent<LayoutElement>();layout.ignoreLayout=true;
            var follower=rect.gameObject.AddComponent<DoodleBuffGlowAnchor>();follower.target=(RectTransform)icon;
            var glow=rect.gameObject.AddComponent<DoodleBuffGlow>();glow.raycastTarget=false;return glow;
        }
        void Update(){if(Time.unscaledTime<nextFrame)return;nextFrame=Time.unscaledTime+1f/24;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=rectTransform.rect;var center=rect.center;float t=Time.unscaledTime;
            const int segments=48;
            for(int i=0;i<segments;i++){
                float a=i*2*Mathf.PI/segments,b=(i+1)*2*Mathf.PI/segments;
                float ra=1+.10f*Mathf.Sin(a*7-t*5)+.06f*Mathf.Sin(a*3+t*4),rb=1+.10f*Mathf.Sin(b*7-t*5)+.06f*Mathf.Sin(b*3+t*4);
                int n=vh.currentVertCount;var gold=new Color(1,.63f,.04f,.85f+.12f*Mathf.Sin(t*4));
                vh.AddVert(center,gold,Vector2.zero);vh.AddVert(center+new Vector2(Mathf.Cos(a)*rect.width,Mathf.Sin(a)*rect.height)*(.5f*ra),new Color(1,.5f,0,0),Vector2.zero);
                vh.AddVert(center+new Vector2(Mathf.Cos(b)*rect.width,Mathf.Sin(b)*rect.height)*(.5f*rb),new Color(1,.5f,0,0),Vector2.zero);vh.AddTriangle(n,n+1,n+2);
            }
        }
    }
}

namespace DoodleIdle { public sealed class DoodleBuffGlowAnchor : UnityEngine.MonoBehaviour {
 public UnityEngine.RectTransform target;
 void LateUpdate(){if(!target)return;var rect=(UnityEngine.RectTransform)transform;rect.anchorMin=rect.anchorMax=target.anchorMin;rect.pivot=target.pivot;rect.anchoredPosition=target.anchoredPosition;rect.sizeDelta=target.rect.size+UnityEngine.Vector2.one*50;}
} }
