using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TacticalRoguelike.Core;

namespace TacticalRoguelike.Tests.EditMode
{
    public sealed class PathfindingTests
    {
        [Test]
        public void FindPath_OpenGrid_ReturnsShortestPathIncludingStartAndGoal()
        {
            var grid = new GameGrid(5, 5, GridTileKind.Floor);
            var start = new GridPosition(0, 0);
            var goal = new GridPosition(4, 3);
            var pathfinding = new Pathfinding();

            IReadOnlyList<GridPosition> path = pathfinding.FindPath(grid, start, goal);

            Assert.AreEqual(ManhattanDistance(start, goal) + 1, path.Count);
            Assert.AreEqual(start, path[0]);
            Assert.AreEqual(goal, path[path.Count - 1]);
            AssertPathIsCardinalAndWalkable(grid, path);
        }

        [Test]
        public void FindPath_PathStartsAtStartAndEndsAtGoal()
        {
            var grid = new GameGrid(3, 3, GridTileKind.Floor);
            var start = new GridPosition(2, 0);
            var goal = new GridPosition(0, 2);
            var pathfinding = new Pathfinding();

            IReadOnlyList<GridPosition> path = pathfinding.FindPath(grid, start, goal);

            Assert.AreEqual(start, path.First());
            Assert.AreEqual(goal, path.Last());
        }

        [Test]
        public void FindPath_ObstacleWithGap_AvoidsWallsAndUsesGap()
        {
            var grid = new GameGrid(5, 5, GridTileKind.Floor);
            var gap = new GridPosition(2, 2);
            for (int y = 0; y < grid.Height; y++)
            {
                GridPosition wall = new GridPosition(2, y);
                if (wall != gap)
                {
                    grid.SetTile(wall, GridTileKind.Wall);
                }
            }

            var start = new GridPosition(0, 0);
            var goal = new GridPosition(4, 0);
            var pathfinding = new Pathfinding();

            IReadOnlyList<GridPosition> path = pathfinding.FindPath(grid, start, goal);

            Assert.Greater(path.Count, 0);
            Assert.Contains(gap, path.ToList());
            AssertPathIsCardinalAndWalkable(grid, path);
        }

        [Test]
        public void FindPath_Disconnected_ReturnsEmptyPath()
        {
            var grid = new GameGrid(5, 5, GridTileKind.Floor);
            for (int y = 0; y < grid.Height; y++)
            {
                grid.SetTile(new GridPosition(2, y), GridTileKind.Wall);
            }

            var pathfinding = new Pathfinding();

            IReadOnlyList<GridPosition> path = pathfinding.FindPath(
                grid,
                new GridPosition(0, 2),
                new GridPosition(4, 2)
            );

            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void FindPath_InvalidOrWallStartOrGoal_ReturnsEmptyPath()
        {
            var grid = new GameGrid(3, 3, GridTileKind.Floor);
            var pathfinding = new Pathfinding();
            var validFloor = new GridPosition(1, 1);
            var wall = new GridPosition(0, 0);
            grid.SetTile(wall, GridTileKind.Wall);

            Assert.AreEqual(
                0,
                pathfinding.FindPath(grid, new GridPosition(-1, 0), validFloor).Count
            );
            Assert.AreEqual(
                0,
                pathfinding.FindPath(grid, validFloor, new GridPosition(3, 0)).Count
            );
            Assert.AreEqual(0, pathfinding.FindPath(grid, wall, validFloor).Count);
            Assert.AreEqual(0, pathfinding.FindPath(grid, validFloor, wall).Count);
        }

        [Test]
        public void FindPath_NullGrid_ThrowsArgumentNullException()
        {
            var pathfinding = new Pathfinding();

            Assert.Throws<ArgumentNullException>(() =>
                pathfinding.FindPath(null, new GridPosition(0, 0), new GridPosition(1, 1))
            );
        }

        [Test]
        public void FindPath_RepeatedCalls_ReturnIdenticalSequence()
        {
            var grid = new GameGrid(5, 5, GridTileKind.Floor);
            var start = new GridPosition(2, 2);
            var goal = new GridPosition(4, 4);
            var pathfinding = new Pathfinding();
            IReadOnlyList<GridPosition> expected = pathfinding.FindPath(grid, start, goal);

            for (int i = 0; i < 20; i++)
            {
                IReadOnlyList<GridPosition> actual = pathfinding.FindPath(grid, start, goal);

                Assert.IsTrue(expected.SequenceEqual(actual), $"Path differed on run {i}.");
            }
        }

        private static int ManhattanDistance(GridPosition first, GridPosition second)
        {
            return Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
        }

        private static void AssertPathIsCardinalAndWalkable(
            GameGrid grid,
            IReadOnlyList<GridPosition> path
        )
        {
            Assert.IsTrue(path.All(grid.IsWalkable));

            for (int i = 1; i < path.Count; i++)
            {
                Assert.AreEqual(
                    1,
                    ManhattanDistance(path[i - 1], path[i]),
                    $"Invalid step from {path[i - 1]} to {path[i]}."
                );
            }
        }
    }
}
