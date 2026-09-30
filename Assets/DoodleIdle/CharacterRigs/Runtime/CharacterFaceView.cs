using UnityEngine;

namespace DoodleIdle.CharacterRigs
{
    // Shared by world faces. Portraits/manual cameras leave this unset and always
    // update. Publish after camera follow, before CharacterFace.LateUpdate.
    public sealed class CharacterFaceView
    {
        readonly Plane[] planes = new Plane[6];
        int frame = -1;

        public void Update(Camera camera)
        {
            if (!camera) { frame = -1; return; }
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            frame = Time.frameCount;
        }

        public bool MaySee(SpriteMask mask)
        {
            // Before the camera has published this frame, never use stale culling.
            if (frame != Time.frameCount || !mask || !mask.sprite) return true;
            var local = mask.sprite.bounds;
            var matrix = mask.transform.localToWorldMatrix;
            var half = local.extents;
            // Current transforms, including negative scale, animated bones and lids.
            var extent = new Vector3(
                Mathf.Abs(matrix.m00) * half.x + Mathf.Abs(matrix.m01) * half.y + Mathf.Abs(matrix.m02) * half.z,
                Mathf.Abs(matrix.m10) * half.x + Mathf.Abs(matrix.m11) * half.y + Mathf.Abs(matrix.m12) * half.z,
                Mathf.Abs(matrix.m20) * half.x + Mathf.Abs(matrix.m21) * half.y + Mathf.Abs(matrix.m22) * half.z);
            var bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), extent * 2 + Vector3.one * .02f);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }
    }
}
