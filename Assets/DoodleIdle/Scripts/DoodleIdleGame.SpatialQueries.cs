using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        const float EnemyCellSize = 2;
        struct EnemyPoint { public Actor actor; public Vector2 position; }
        readonly Dictionary<Vector2Int,List<int>> enemyCells = new Dictionary<Vector2Int,List<int>>();
        readonly List<EnemyPoint> enemyPoints = new List<EnemyPoint>(256);
        readonly Stack<List<int>> spareEnemyCells = new Stack<List<int>>();
        readonly Stack<List<Vector2>> spareSpawnCells = new Stack<List<Vector2>>();
        readonly List<int> segmentCandidates = new List<int>(256);
        static readonly System.Comparison<int> ReverseEnemyOrder = (a,b)=>b.CompareTo(a);

        // Refill runs synchronously, with no intervening physics simulation. Cache
        // exact body positions once and retain the original random attempts/distances.
        // Keep this separate from the summon snapshot (which may still be in use).
        readonly Dictionary<Vector2Int,List<Vector2>> spawnCells = new Dictionary<Vector2Int,List<Vector2>>();
        void SnapshotSpawnPositions()
        {
            foreach(var cell in spawnCells.Values)cell.Clear();
            if (spawnCells.Count > 1024) { foreach (var cell in spawnCells.Values) spareSpawnCells.Push(cell); spawnCells.Clear(); }
            foreach(var enemy in enemies)AddSpawnPosition(enemy.Position);
        }
        void AddSpawnPosition(Vector2 position)
        {
            var key=new Vector2Int(Mathf.FloorToInt(position.x/EnemyCellSize),Mathf.FloorToInt(position.y/EnemyCellSize));
            if(!spawnCells.TryGetValue(key,out var cell))spawnCells[key]=cell=spareSpawnCells.Count > 0 ? spareSpawnCells.Pop() : new List<Vector2>(4);
            cell.Add(position);
        }
        bool SpawnPositionOccupied(Vector2 position)
        {
            int x=Mathf.FloorToInt(position.x/EnemyCellSize),y=Mathf.FloorToInt(position.y/EnemyCellSize);
            // sqrt(1.6) < cell size, so only the current cell and its neighbours can overlap.
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                if(spawnCells.TryGetValue(new Vector2Int(x+dx,y+dy),out var cell))
                    foreach(var previous in cell)if((position-previous).sqrMagnitude<1.6f)return true;
            return false;
        }

        // Summon hits only apply forces: positions cannot change until the next physics
        // simulation. Keep Actor identities so deaths never redirect hits to pooled spawns.
        void SnapshotSummonTargets()
        {
            foreach(var cell in enemyCells.Values)cell.Clear();
            if (enemyCells.Count > 1024) { foreach (var cell in enemyCells.Values) spareEnemyCells.Push(cell); enemyCells.Clear(); }
            enemyPoints.Clear();
            foreach(var enemy in enemies)
            {
                if (!Alive(enemy)) continue;
                var position=enemy.Position;
                var cell=new Vector2Int(Mathf.FloorToInt(position.x/EnemyCellSize),Mathf.FloorToInt(position.y/EnemyCellSize));
                if(!enemyCells.TryGetValue(cell,out var indices))enemyCells[cell]=indices=spareEnemyCells.Count > 0 ? spareEnemyCells.Pop() : new List<int>(16);
                indices.Add(enemyPoints.Count);enemyPoints.Add(new EnemyPoint{actor=enemy,position=position});
            }
        }
        void FindSegmentCandidates(Vector2 a,Vector2 b,float radius)
        {
            segmentCandidates.Clear();
            int x0=Mathf.FloorToInt((Mathf.Min(a.x,b.x)-radius)/EnemyCellSize),x1=Mathf.FloorToInt((Mathf.Max(a.x,b.x)+radius)/EnemyCellSize);
            int y0=Mathf.FloorToInt((Mathf.Min(a.y,b.y)-radius)/EnemyCellSize),y1=Mathf.FloorToInt((Mathf.Max(a.y,b.y)+radius)/EnemyCellSize);
            // Very long swept segments can cover more empty cells than there are enemies.
            if((long)(x1-x0+1)*(y1-y0+1)>enemyPoints.Count){for(int i=enemyPoints.Count-1;i>=0;i--)segmentCandidates.Add(i);return;}
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                if(enemyCells.TryGetValue(new Vector2Int(x,y),out var indices))segmentCandidates.AddRange(indices);
            segmentCandidates.Sort(ReverseEnemyOrder);
        }
    }
}
