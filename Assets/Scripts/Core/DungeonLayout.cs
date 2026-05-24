using System;
using System.Collections.Generic;

namespace TacticalRoguelike.Core
{
    public sealed class DungeonLayout
    {
        public DungeonLayout(
            GameGrid grid,
            int seed,
            GridPosition playerSpawn,
            IEnumerable<GridPosition> enemySpawns,
            GridPosition stairsDown
        )
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Seed = seed;
            PlayerSpawn = playerSpawn;
            StairsDown = stairsDown;

            if (!Grid.IsWalkable(playerSpawn))
            {
                throw new ArgumentException(
                    "Player spawn must be on a walkable tile.",
                    nameof(playerSpawn)
                );
            }

            if (!Grid.IsWalkable(stairsDown))
            {
                throw new ArgumentException(
                    "Stairs down must be on a walkable tile.",
                    nameof(stairsDown)
                );
            }

            if (enemySpawns == null)
            {
                throw new ArgumentNullException(nameof(enemySpawns));
            }

            var copiedEnemySpawns = new List<GridPosition>();
            foreach (GridPosition enemySpawn in enemySpawns)
            {
                if (!Grid.IsWalkable(enemySpawn))
                {
                    throw new ArgumentException(
                        "Enemy spawns must be on walkable tiles.",
                        nameof(enemySpawns)
                    );
                }

                copiedEnemySpawns.Add(enemySpawn);
            }

            if (copiedEnemySpawns.Count == 0)
            {
                throw new ArgumentException(
                    "At least one enemy spawn is required.",
                    nameof(enemySpawns)
                );
            }

            EnemySpawns = copiedEnemySpawns.AsReadOnly();
        }

        public GameGrid Grid { get; }
        public int Seed { get; }
        public GridPosition PlayerSpawn { get; }
        public IReadOnlyList<GridPosition> EnemySpawns { get; }
        public GridPosition StairsDown { get; }
    }
}
