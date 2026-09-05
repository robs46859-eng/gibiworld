// GW-ARCH-003 AR-04 & W04 — LocalGridNavigation.
// Bounded 2D grid (0.05 m cells, max 6x6 m / 14,400 cells).
// Deterministic A* with integer costs and stable tie-breaking.
// Diagonal corner-cut prevention (requires both orthogonal cells clear).
// Swept capsule corridor validation.
using System;
using System.Collections.Generic;
using Gibi.Core;
using UnityEngine;

namespace Gibi.Spatial
{
    public sealed class LocalGridNavigation : MonoBehaviour, INavigationQuery
    {
        public const float CellSizeM = 0.05f;
        public const int MaxGridDimension = 120; // 6.0m / 0.05m = 120 cells
        public const int StraightCost = 10;
        public const int DiagonalCost = 14;

        [SerializeField] private Vector2 gridOrigin = new Vector2(-3f, -3f);
        [SerializeField] private int widthCells = 120;
        [SerializeField] private int heightCells = 120;

        private readonly bool[] _blocked = new bool[MaxGridDimension * MaxGridDimension];
        private int _geometryRevision = 1;

        public int GeometryRevision => _geometryRevision;
        public int Width => widthCells;
        public int Height => heightCells;

        private void Awake()
        {
            InitializeGrid();
        }

        public void InitializeGrid(int width = 120, int height = 120, Vector2? origin = null)
        {
            widthCells = Mathf.Clamp(width, 10, MaxGridDimension);
            heightCells = Mathf.Clamp(height, 10, MaxGridDimension);
            if (origin.HasValue) gridOrigin = origin.Value;
            Array.Clear(_blocked, 0, _blocked.Length);
            _geometryRevision++;
        }

        public void SetCellBlocked(int x, int z, bool blocked)
        {
            if (x < 0 || x >= widthCells || z < 0 || z >= heightCells) return;
            _blocked[z * widthCells + x] = blocked;
        }

        public bool IsCellBlocked(int x, int z)
        {
            if (x < 0 || x >= widthCells || z < 0 || z >= heightCells) return true;
            return _blocked[z * widthCells + x];
        }

        public void RasterizeObstacleBox(Vector3 center, Vector3 size, float marginM = 0.05f)
        {
            Vector3 min = center - size * 0.5f - new Vector3(marginM, 0f, marginM);
            Vector3 max = center + size * 0.5f + new Vector3(marginM, 0f, marginM);

            Vector2Int minCell = WorldToCell(min);
            Vector2Int maxCell = WorldToCell(max);

            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    SetCellBlocked(x, z, true);
                }
            }
            _geometryRevision++;
        }

        public Vector2Int WorldToCell(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / CellSizeM);
            int z = Mathf.FloorToInt((worldPos.z - gridOrigin.y) / CellSizeM);
            return new Vector2Int(x, z);
        }

        public Vector3 CellToWorld(int x, int z, float y = 0f)
        {
            float wx = gridOrigin.x + (x + 0.5f) * CellSizeM;
            float wz = gridOrigin.y + (z + 0.5f) * CellSizeM;
            return new Vector3(wx, y, wz);
        }

        public PathResult TryPlan(Vector3 start, Vector3 goal, AgentEnvelope agent, int geometryRevision)
        {
            Vector2Int startCell = WorldToCell(start);
            Vector2Int goalCell = WorldToCell(goal);

            if (IsCellBlocked(startCell.x, startCell.y))
                return PathResult.Fail(PathStatus.Blocked, _geometryRevision);
            if (IsCellBlocked(goalCell.x, goalCell.y))
                return PathResult.Fail(PathStatus.Blocked, _geometryRevision);

            if (startCell == goalCell)
            {
                return PathResult.Success(_geometryRevision, new[] { start, goal }, Vector3.Distance(start, goal));
            }

            // A* with deterministic integer tie breaking
            int totalCells = widthCells * heightCells;
            var gCost = new int[totalCells];
            var fCost = new int[totalCells];
            var parent = new int[totalCells];
            var closed = new bool[totalCells];
            Array.Fill(gCost, int.MaxValue);
            Array.Fill(fCost, int.MaxValue);
            Array.Fill(parent, -1);

            int startIndex = startCell.y * widthCells + startCell.x;
            int goalIndex = goalCell.y * widthCells + goalCell.x;

            var openList = new List<int> { startIndex };
            gCost[startIndex] = 0;
            fCost[startIndex] = Heuristic(startCell, goalCell);

            // 8 neighbors: cardinal first, then diagonal
            int[] dx = { 0, 1, 0, -1, 1, 1, -1, -1 };
            int[] dz = { 1, 0, -1, 0, 1, -1, 1, -1 };
            int[] moveCosts = { StraightCost, StraightCost, StraightCost, StraightCost,
                                DiagonalCost, DiagonalCost, DiagonalCost, DiagonalCost };

            bool found = false;

            while (openList.Count > 0)
            {
                // Find minimum fCost with stable index tie-breaking
                int bestIdx = 0;
                int bestF = fCost[openList[0]];
                for (int i = 1; i < openList.Count; i++)
                {
                    int candidateF = fCost[openList[i]];
                    if (candidateF < bestF || (candidateF == bestF && openList[i] < openList[bestIdx]))
                    {
                        bestF = candidateF;
                        bestIdx = i;
                    }
                }

                int current = openList[bestIdx];
                openList.RemoveAt(bestIdx);

                if (current == goalIndex)
                {
                    found = true;
                    break;
                }

                closed[current] = true;
                int cx = current % widthCells;
                int cz = current / widthCells;

                for (int n = 0; n < 8; n++)
                {
                    int nx = cx + dx[n];
                    int nz = cz + dz[n];

                    if (nx < 0 || nx >= widthCells || nz < 0 || nz >= heightCells)
                        continue;

                    int neighborIndex = nz * widthCells + nx;
                    if (closed[neighborIndex] || _blocked[neighborIndex])
                        continue;

                    // Diagonal corner cut prevention: both orthogonal cells must be clear
                    if (n >= 4)
                    {
                        if (IsCellBlocked(cx + dx[n], cz) || IsCellBlocked(cx, cz + dz[n]))
                            continue;
                    }

                    int tentativeG = gCost[current] + moveCosts[n];
                    if (tentativeG < gCost[neighborIndex])
                    {
                        parent[neighborIndex] = current;
                        gCost[neighborIndex] = tentativeG;
                        fCost[neighborIndex] = tentativeG + Heuristic(new Vector2Int(nx, nz), goalCell);

                        if (!openList.Contains(neighborIndex))
                            openList.Add(neighborIndex);
                    }
                }
            }

            if (!found)
                return PathResult.Fail(PathStatus.Failed, _geometryRevision);

            // Reconstruct path
            var pathIndices = new List<int>();
            int trace = goalIndex;
            while (trace != -1)
            {
                pathIndices.Add(trace);
                trace = parent[trace];
            }
            pathIndices.Reverse();

            // Convert to world waypoints
            var rawWaypoints = new List<Vector3> { start };
            for (int i = 1; i < pathIndices.Count - 1; i++)
            {
                int idx = pathIndices[i];
                rawWaypoints.Add(CellToWorld(idx % widthCells, idx / widthCells, start.y));
            }
            rawWaypoints.Add(goal);

            // Simplify path using straight-line line-of-sight checks
            var waypoints = SimplifyPath(rawWaypoints, agent);
            float lengthM = CalculatePathLength(waypoints);

            return PathResult.Success(_geometryRevision, waypoints.ToArray(), lengthM);
        }

        private static int Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Math.Abs(a.x - b.x);
            int dz = Math.Abs(a.y - b.y);
            return 10 * (dx + dz) + (14 - 2 * 10) * Math.Min(dx, dz);
        }

        private List<Vector3> SimplifyPath(List<Vector3> raw, AgentEnvelope agent)
        {
            if (raw.Count <= 2) return raw;

            var simplified = new List<Vector3> { raw[0] };
            int current = 0;

            while (current < raw.Count - 1)
            {
                int furthest = current + 1;
                for (int next = raw.Count - 1; next > current + 1; next--)
                {
                    if (IsSweptCorridorClear(raw[current], raw[next], agent.RadiusM))
                    {
                        furthest = next;
                        break;
                    }
                }
                simplified.Add(raw[furthest]);
                current = furthest;
            }

            return simplified;
        }

        private bool IsSweptCorridorClear(Vector3 p0, Vector3 p1, float radiusM)
        {
            float dist = Vector3.Distance(p0, p1);
            int samples = Mathf.CeilToInt(dist / (CellSizeM * 0.5f));
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector3 samplePos = Vector3.Lerp(p0, p1, t);
                Vector2Int cell = WorldToCell(samplePos);
                if (IsCellBlocked(cell.x, cell.y))
                    return false;
            }
            return true;
        }

        private static float CalculatePathLength(List<Vector3> points)
        {
            float length = 0f;
            for (int i = 0; i < points.Count - 1; i++)
                length += Vector3.Distance(points[i], points[i + 1]);
            return length;
        }
    }
}
