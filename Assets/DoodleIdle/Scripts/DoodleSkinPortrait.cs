using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    // Weapon-free, tightly framed still portraits for appearance slots.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleSkinPortrait : MaskableGraphic
    {
        Sprite frame;
        public override Texture mainTexture => frame ? frame.texture : Texture2D.whiteTexture;
        public void Configure(int index)
        {
            raycastTarget = false;
            frame = DoodleCharacterCatalog.PlayerAppearancePortrait(index);
            SetVerticesDirty(); SetMaterialDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (!frame) return;
            Rect r = rectTransform.rect;
            float unit = Mathf.Min(r.width * 1.1f, r.height);
            Vector2 origin = r.center;
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
