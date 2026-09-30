using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace DoodleIdle
{
    sealed class DoodleVisualLease : MonoBehaviour
    {
        public string key;
        public SpriteRenderer art;
        public bool returned;
        public CircleCollider2D trigger;
    }

    public sealed partial class DoodleIdleGame
    {
        readonly Dictionary<string, Stack<DoodleVisualLease>> visualPool = new Dictionary<string, Stack<DoodleVisualLease>>();
        int pooledVisualCount;
        Transform visualPoolRoot;
        readonly Stack<DoodleVisualLease> preparedVisuals = new Stack<DoodleVisualLease>();
        readonly Dictionary<DoodleIdle.CharacterRigs.CharacterFace, Stack<DoodleVisualLease>> preparedFaces = new Dictionary<DoodleIdle.CharacterRigs.CharacterFace, Stack<DoodleVisualLease>>();
        public int VisualObjectsCreated { get; private set; }
        public int VisualObjectsReused { get; private set; }
        public int PooledVisualCount => pooledVisualCount;

        SpriteRenderer RentVisual(string label, Sprite sprite, Vector2 position, Vector2 scale, int order)
        {
            DoodleVisualLease lease = null;
            if (visualPool.TryGetValue(label, out var spare))
                while (spare.Count > 0 && !lease) { lease = spare.Pop(); pooledVisualCount--; }
            if (!lease) {
                var face = DoodleSkillFaceCatalog.Find(sprite);
                if (face != null && preparedFaces.TryGetValue(face.facePrefab, out var faces) && faces.Count > 0)
                    lease = faces.Pop();
                else if (preparedVisuals.Count > 0) lease = preparedVisuals.Pop();
                if (lease) pooledVisualCount--;
            }
            if (!lease) {
                var go = new GameObject(label);
                lease = go.AddComponent<DoodleVisualLease>(); lease.key = label;
                lease.art = go.AddComponent<SpriteRenderer>(); VisualObjectsCreated++;
            } else VisualObjectsReused++;
            lease.key = label; lease.returned = false; lease.gameObject.name = label;
            var transform = lease.transform;
            transform.SetParent(world, false); transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = new Vector3(scale.x, scale.y, 1);
            var art = lease.art;
            art.enabled = true; art.color = Color.white; art.flipX = art.flipY = false;
            art.sortingOrder = order; art.sortingLayerID = 0; art.SetPropertyBlock(null);
            SetSpriteArt(art, sprite);
            if (lease.trigger) lease.trigger.enabled = false;
            lease.gameObject.SetActive(true);
            return art;
        }
        void ReleaseVisual(GameObject go)
        {
            if (!go) return;
            var lease = go.GetComponent<DoodleVisualLease>();
            if (!lease) { Destroy(go); return; }
            if (lease.returned) return;
            lease.returned = true; go.transform.DOKill(); go.SetActive(false);
            var face = go.GetComponent<DoodleSkillFaceVisual>();
            // Cannons own a muzzle child. Do not accumulate children or active tweens across rentals.
            for (int i = go.transform.childCount - 1; i >= 0; i--) {
                var child = go.transform.GetChild(i).gameObject;
                if (face && child.transform == face.Attachment) continue;
                if (child.GetComponent<DoodleVisualLease>()) ReleaseVisual(child);
                else Destroy(child);
            }
            if (!visualPool.TryGetValue(lease.key, out var spare)) visualPool[lease.key] = spare = new Stack<DoodleVisualLease>();
            if (pooledVisualCount >= 2048 || spare.Count >= 128) { Destroy(go); return; }
            if (!visualPoolRoot) {
                visualPoolRoot = new GameObject("Reusable combat visuals").transform;
                visualPoolRoot.SetParent(world, false);
            }
            go.transform.SetParent(visualPoolRoot, false);
            spare.Push(lease); pooledVisualCount++;
        }
        static CircleCollider2D VisualTrigger(SpriteRenderer art)
        {
            var lease = art.GetComponent<DoodleVisualLease>();
            var collider = lease ? lease.trigger : art.GetComponent<CircleCollider2D>();
            if (!collider) collider = art.gameObject.AddComponent<CircleCollider2D>();
            if (lease) lease.trigger = collider;
            collider.enabled = true; collider.isTrigger = true; collider.offset = Vector2.zero;
            return collider;
        }
    }
}
