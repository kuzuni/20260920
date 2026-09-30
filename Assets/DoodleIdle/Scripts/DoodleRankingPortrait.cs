using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    // Only visible rows own a rig/camera. Static previews render once, not every frame.
    public sealed class DoodleRankingPortrait : MonoBehaviour
    {
        public DoodlePlayerLook Look {get;private set;}
        DoodleUi owner;
        RectTransform rect,viewport;
        DoodleIdlePortrait preview;
        Image image;
        float nextCheck;
        readonly Vector3[] corners=new Vector3[4];
        public void Configure(DoodleUi ui,DoodlePlayerLook look)
        {
            owner=ui;Look=look??new DoodlePlayerLook();rect=(RectTransform)transform;
            image=GetComponent<Image>();image.enabled=false;
            viewport=GetComponentInParent<ScrollRect>()?.viewport;
        }
        void LateUpdate()
        {
            if(Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.15f;
            bool visible=true;
            if(viewport){
                rect.GetWorldCorners(corners);
                Vector2 min=viewport.InverseTransformPoint(corners[0]),max=viewport.InverseTransformPoint(corners[2]);
                visible=viewport.rect.Overlaps(Rect.MinMaxRect(min.x,min.y,max.x,max.y));
            }
            if(visible && !preview){preview=gameObject.AddComponent<DoodleIdlePortrait>();preview.Configure(owner,DoodleIdlePortrait.View.Ranking,Look);}
            else if(!visible && preview)Release();
        }
        void Release()
        {
            if(!preview)return;preview.enabled=false;Destroy(preview);preview=null;
            var child=transform.Find("Live player portrait");if(child){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        }
        void OnDisable(){Release();}
    }
}
