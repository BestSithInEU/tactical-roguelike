using System;
using System.Collections.Generic;

namespace TacticalRoguelike.Core
{
    public sealed class Pathfinding
    {
        public IReadOnlyList<GridPosition> FindPath(GameGrid grid, GridPosition start, GridPosition goal)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (!grid.IsWalkable(start) || !grid.IsWalkable(goal))
            {
                return Array.Empty<GridPosition>();
            }

            if (start == goal)
            {
                return new[] { start };
            }

            var openSet = new List<PathNode>();
            var closedSet = new HashSet<GridPosition>();
            var cameFrom = new Dictionary<GridPosition, GridPosition>();
            var bestCosts = new Dictionary<GridPosition, int>();
            int nextOrder = 0;

            bestCosts[start] = 0;
            openSet.Add(new PathNode(start, 0, ManhattanDistance(start, goal), nextOrder++));

            while (openSet.Count > 0)
            {
                int currentIndex = FindBestOpenNodeIndex(openSet);
                PathNode current = openSet[currentIndex];
                openSet.RemoveAt(currentIndex);

                if (!closedSet.Add(current.Position))
                {
                    continue;
                }

                if (current.Position == goal)
                {
                    return ReconstructPath(cameFrom, start, goal);
                }

                foreach (GridPosition neighbor in current.Position.CardinalNeighbors())
                {
                    if (closedSet.Contains(neighbor) || !grid.IsWalkable(neighbor))
                    {
                        continue;
                    }

                    int tentativeCost = current.CostFromStart + 1;
                    if (bestCosts.TryGetValue(neighbor, out int knownCost) && tentativeCost >= knownCost)
                    {
                        continue;
                    }

                    cameFrom[neighbor] = current.Position;
                    bestCosts[neighbor] = tentativeCost;
                    openSet.Add(new PathNode(neighbor, tentativeCost, ManhattanDistance(neighbor, goal), nextOrder++));
                }
            }

            return Array.Empty<GridPosition>();
        }

        private static int FindBestOpenNodeIndex(IReadOnlyList<PathNode> openSet)
        {
            int bestIndex = 0;
            PathNode best = openSet[0];

            for (int i = 1; i < openSet.Count; i++)
            {
                PathNode candidate = openSet[i];
                if (candidate.EstimatedTotalCost < best.EstimatedTotalCost
                    || (candidate.EstimatedTotalCost == best.EstimatedTotalCost && candidate.EstimatedRemainingCost < best.EstimatedRemainingCost)
                    || (candidate.EstimatedTotalCost == best.EstimatedTotalCost
                        && candidate.EstimatedRemainingCost == best.EstimatedRemainingCost
                        && candidate.Order < best.Order))
                {
                    bestIndex = i;
                    best = candidate;
                }
            }

            return bestIndex;
        }

        private static IReadOnlyList<GridPosition> ReconstructPath(
            IReadOnlyDictionary<GridPosition, GridPosition> cameFrom,
            GridPosition start,
            GridPosition goal)
        {
            var path = new List<GridPosition> { goal };
            GridPosition current = goal;

            while (current != start)
            {
                current = cameFrom[current];
                path.Add(current);
            }

            path.Reverse();
            return path;
        }

        private static int ManhattanDistance(GridPosition first, GridPosition second)
        {
            return Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
        }

        private readonly struct PathNode
        {
            public PathNode(GridPosition position, int costFromStart, int estimatedRemainingCost, int order)
            {
                Position = position;
                CostFromStart = costFromStart;
                EstimatedRemainingCost = estimatedRemainingCost;
                Order = order;
            }

            public GridPosition Position { get; }
            public int CostFromStart { get; }
            public int EstimatedRemainingCost { get; }
            public int EstimatedTotalCost => CostFromStart + EstimatedRemainingCost;
            public int Order { get; }
        }
    }
}
