using UnityEngine;
namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        void UpdateEndlessGround()
        {
            if (!endlessWorld || !gameCamera || groundTiles.Count == 0) return;
            // The shader samples world coordinates, so moving one quad does not slide the pattern.
            float halfHeight = Mathf.Max(1, gameCamera.orthographicSize) + 8;
            float halfWidth = Mathf.Max(1, gameCamera.orthographicSize * gameCamera.aspect) + 8;
            var floor = groundTiles[0].transform;
            var position = gameCamera.transform.position; position.z = 0;
            floor.position = position; floor.localScale = new Vector3(halfWidth * 2, halfHeight * 2, 1);
        }
    }
}
