using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TacticalRoguelike.Core;

namespace TacticalRoguelike.Tests.EditMode
{
    public sealed class DungeonGeneratorTests
    {
        private static readonly DungeonGeneratorConfig TestConfig = new DungeonGeneratorConfig(
            width: 36,
            height: 24,
            roomCount: 6,
            minRoomSize: 4,
            maxRoomSize: 7);

        [Test]
        public void Generate_SameSeedAndConfig_ProducesSameLayoutAndSpawns()
        {
            var generator = new DungeonGenerator();

            DungeonLayout first = generator.Generate(12345, TestConfig);
            DungeonLayout second = generator.Generate(12345, TestConfig);

            AssertLayoutsEqual(first, second);
        }

        [Test]
        public void Generate_MultipleSeeds_ProduceAtLeastOneDifferentLayout()
        {
            var generator = new DungeonGenerator();
            DungeonLayout baseline = generator.Generate(1000, TestConfig);

            bool foundDifference = Enumerable.Range(1001, 12)
                .Select(seed => generator.Generate(seed, TestConfig))
                .Any(layout => !LayoutsEqual(baseline, layout));

            Assert.IsTrue(foundDifference, "Expected at least one nearby seed to produce a different layout or spawn set.");
        }

        [Test]
        public void Generate_PlacesRequiredSpawnsOnWalkableTiles()
        {
            var generator = new DungeonGenerator();

            DungeonLayout layout = generator.Generate(4444, TestConfig);

            Assert.IsTrue(layout.Grid.IsWalkable(layout.PlayerSpawn));
            Assert.IsTrue(layout.Grid.IsWalkable(layout.StairsDown));
            Assert.AreNotEqual(layout.PlayerSpawn, layout.StairsDown);
            Assert.GreaterOrEqual(layout.EnemySpawns.Count, 1);
            Assert.IsTrue(layout.EnemySpawns.All(spawn => layout.Grid.IsWalkable(spawn)));
            Assert.IsFalse(layout.EnemySpawns.Contains(layout.StairsDown));
        }

        [Test]
        public void Generate_AllWalkableTilesAreConnectedFromPlayerSpawnAcrossMultipleSeeds()
        {
            var generator = new DungeonGenerator();

            foreach (int seed in new[] { 7, 123, 4444, 98765, 123456 })
            {
                DungeonLayout layout = generator.Generate(seed, TestConfig);

                HashSet<GridPosition> reachable = FloodFillWalkable(layout.Grid, layout.PlayerSpawn);
                int walkableCount = layout.Grid.Positions().Count(position => layout.Grid.IsWalkable(position));

                Assert.AreEqual(walkableCount, reachable.Count, $"Seed {seed} produced disconnected walkable tiles.");
            }
        }

        [Test]
        public void Generate_TightValidConfig_StillProducesPlayableFallbackLayout()
        {
            var generator = new DungeonGenerator();
            var tightConfig = new DungeonGeneratorConfig(
                width: 10,
                height: 10,
                roomCount: 4,
                minRoomSize: 4,
                maxRoomSize: 4,
                roomPlacementAttempts: 4);

            DungeonLayout layout = generator.Generate(123, tightConfig);

            Assert.IsTrue(layout.Grid.IsWalkable(layout.PlayerSpawn));
            Assert.IsTrue(layout.Grid.IsWalkable(layout.StairsDown));
            Assert.AreNotEqual(layout.PlayerSpawn, layout.StairsDown);
            Assert.IsTrue(layout.EnemySpawns.All(spawn => layout.Grid.IsWalkable(spawn)));
        }

        private static HashSet<GridPosition> FloodFillWalkable(GameGrid grid, GridPosition start)
        {
            var visited = new HashSet<GridPosition>();
            var frontier = new Queue<GridPosition>();
            frontier.Enqueue(start);
            visited.Add(start);

            while (frontier.Count > 0)
            {
                GridPosition current = frontier.Dequeue();
                foreach (GridPosition neighbor in current.CardinalNeighbors())
                {
                    if (!visited.Contains(neighbor) && grid.IsWalkable(neighbor))
                    {
                        visited.Add(neighbor);
                        frontier.Enqueue(neighbor);
                    }
                }
            }

            return visited;
        }

        private static void AssertLayoutsEqual(DungeonLayout expected, DungeonLayout actual)
        {
            Assert.IsTrue(LayoutsEqual(expected, actual));
        }

        private static bool LayoutsEqual(DungeonLayout first, DungeonLayout second)
        {
            if (first.Seed != second.Seed
                || first.Grid.Width != second.Grid.Width
                || first.Grid.Height != second.Grid.Height
                || first.PlayerSpawn != second.PlayerSpawn
                || first.StairsDown != second.StairsDown
                || !first.EnemySpawns.SequenceEqual(second.EnemySpawns))
            {
                return false;
            }

            foreach (GridPosition position in first.Grid.Positions())
            {
                if (first.Grid.GetTile(position) != second.Grid.GetTile(position))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
