using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    // All inventory/preview portraits share a body scale. Hats consume extra space
    // instead of changing the size of the face inside the card.
    public sealed class DoodleSkinPortrait : MaskableGraphic
    {
        public bool Animate;
        int costume, pose;
        Sprite frame;
        public override Texture mainTexture => frame ? frame.texture : Texture2D.whiteTexture;
        public void Configure(int index)
        {
            costume = index; raycastTarget = false;
            SetPose(0);
        }
        void Update()
        {
            int next = Animate ? Mathf.FloorToInt(Time.unscaledTime * 6) % 2 : 0;
            if (pose != next) SetPose(next);
        }
        void SetPose(int next)
        {
            pose = next;
            frame = costume < 0 ? DoodlePlayerCostumeArt.BodyFrame(pose) : DoodlePlayerCostumeArt.Frame(costume, pose);
            SetVerticesDirty(); SetMaterialDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (!frame) return;
            Rect r = rectTransform.rect;
            float unit = Mathf.Min(r.width * .75f, r.height * .55f);
            Vector2 origin = new Vector2(r.center.x, r.yMin + r.height * .37f);
            Vector2 min = origin + (Vector2)frame.bounds.min * unit;
            Vector2 max = origin + (Vector2)frame.bounds.max * unit;
            Rect uv = frame.rect;
            uv = new Rect(uv.x / frame.texture.width, uv.y / frame.texture.height, uv.width / frame.texture.width, uv.height / frame.texture.height);
            vh.AddVert(new Vector3(min.x, min.y), color, uv.min);
            vh.AddVert(new Vector3(min.x, max.y), color, new Vector2(uv.xMin, uv.yMax));
            vh.AddVert(new Vector3(max.x, max.y), color, uv.max);
            vh.AddVert(new Vector3(max.x, min.y), color, new Vector2(uv.xMax, uv.yMin));
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
        }
    }
}
