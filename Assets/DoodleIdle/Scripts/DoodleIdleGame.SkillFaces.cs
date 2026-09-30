using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        internal Transform SkillFaceTarget(Vector2 position)
        {
            var target=Closest(position);
            if(!Alive(target) || !target.root.activeInHierarchy)return null;
            return target.rigVisual && target.rigVisual.Rig.face
                ? target.rigVisual.Rig.face.transform : target.art.transform;
        }
        Sprite ConfigureSkillFace(SpriteRenderer renderer,Sprite source)
        {
            var entry=DoodleSkillFaceCatalog.Find(source);
            var visual=renderer.GetComponent<DoodleSkillFaceVisual>();
            if(entry==null && !visual)return source;
            if(!visual)visual=renderer.gameObject.AddComponent<DoodleSkillFaceVisual>();
            return visual.Configure(this,renderer,source,entry);
        }
    }
}
