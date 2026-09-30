using DoodleIdle.CharacterRigs;
using UnityEngine;

namespace DoodleIdle
{
    // Sprite-frame motion and mirroring live on an outer attachment. The prefab's
    // authored eye/mouth transforms stay editable and are never overwritten.
    [DefaultExecutionOrder(50)]
    public sealed class DoodleSkillFaceVisual : MonoBehaviour
    {
        public CharacterFace Face { get; private set; }
        public Transform Attachment { get; private set; }
        DoodleSkillFaceCatalog.Entry entry;
        DoodleIdleGame owner;
        SpriteRenderer body;
        CharacterFace prefab;
        float targetRefresh;

        public Sprite Configure(DoodleIdleGame game, SpriteRenderer renderer, Sprite source, DoodleSkillFaceCatalog.Entry next)
        {
            owner=game; body=renderer;
            if(next==null)
            {
                entry=null;
                if(Attachment)Attachment.gameObject.SetActive(false);
                return source;
            }
            // Reapplying the same animation frame needs no second attachment sync;
            // LateUpdate still picks up live tint, facing, pause and target changes.
            if(entry==next && Face && prefab==next.facePrefab)return next.body;
            if(!Face || prefab!=next.facePrefab)
            {
                if(Attachment){Attachment.gameObject.SetActive(false);Destroy(Attachment.gameObject);}
                Attachment=new GameObject("SkillFaceMotion").transform;Attachment.SetParent(transform,false);
                Face=Instantiate(next.facePrefab,Attachment,false);
                Face.View = owner ? owner.FaceView : null;
                Face.headRenderer=body;
                prefab=next.facePrefab;targetRefresh=0;
            }
            entry=next;Sync();return next.body;
        }
        void LateUpdate() { Sync(); }
        void Sync()
        {
            if(entry==null || !Face || !body)return;
            bool visible=body.enabled && body.color.a>0;
            if (Attachment.gameObject.activeSelf != visible) Attachment.gameObject.SetActive(visible);
            float x=body.flipX?-1:1,y=body.flipY?-1:1;
            var scale = new Vector3(x,y,1);
            var position = new Vector3(entry.frameOffset.x*x,entry.frameOffset.y*y,0);
            if (!Attachment.localScale.Equals(scale)) Attachment.localScale = scale;
            if (!Attachment.localPosition.Equals(position)) Attachment.localPosition = position;
            Face.Paused=owner && owner.paused;
            Face.SetTint(body.color);
            if(owner && !Face.Paused && (Time.time>=targetRefresh || !Face.target || !Face.target.gameObject.activeInHierarchy))
            { Face.target=owner.SkillFaceTarget(transform.position);targetRefresh=Time.time+.1f; }
        }
        void OnDisable()
        {
            if(!Face)return;
            Face.target=null;
            // Returning the whole object already invokes CharacterFace.OnDisable.
            // Reset here only when this component alone is being disabled.
            if(Face.isActiveAndEnabled)Face.ResetExpression();
        }
    }
}
