using System;
using System.Collections.Generic;

namespace TacticalRoguelike.Core
{
    public sealed class RunState
    {
        private const int DefaultPlayerHitPoints = 10;
        private const int DefaultPlayerAttackDamage = 3;
        private const int DefaultEnemyHitPoints = 3;
        private const int DefaultEnemyAttackDamage = 2;

        public RunState(DungeonLayout layout, int floorNumber = 1)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }
            if (floorNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(floorNumber),
                    floorNumber,
                    "Floor number must be greater than zero."
                );
            }

            Grid = layout.Grid;
            Seed = layout.Seed;
            FloorNumber = floorNumber;
            StairsDown = layout.StairsDown;
            Player = new EntityState(
                "player",
                layout.PlayerSpawn,
                DefaultPlayerHitPoints,
                DefaultPlayerAttackDamage
            );

            var enemies = new List<EntityState>();
            for (int i = 0; i < layout.EnemySpawns.Count; i++)
            {
                GridPosition spawn = layout.EnemySpawns[i];
                if (spawn == Player.Position)
                {
                    throw new ArgumentException(
                        "Enemy spawn cannot overlap the player spawn.",
                        nameof(layout)
                    );
                }

                if (FindAliveEntityAt(enemies, spawn) != null)
                {
                    throw new ArgumentException(
                        "Enemy spawns cannot overlap each other.",
                        nameof(layout)
                    );
                }

                enemies.Add(
                    new EntityState(
                        $"enemy-{i}",
                        spawn,
                        DefaultEnemyHitPoints,
                        DefaultEnemyAttackDamage
                    )
                );
            }

            Enemies = enemies;
            Status = RunStatus.Ongoing;
        }

        internal RunState(
            GameGrid grid,
            int seed,
            int floorNumber,
            GridPosition stairsDown,
            EntityState player,
            IEnumerable<EntityState> enemies,
            int turnNumber,
            RunStatus status
        )
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Player = player ?? throw new ArgumentNullException(nameof(player));
            if (enemies == null)
            {
                throw new ArgumentNullException(nameof(enemies));
            }
            if (turnNumber < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(turnNumber),
                    turnNumber,
                    "Turn number cannot be negative."
                );
            }
            if (floorNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(floorNumber),
                    floorNumber,
                    "Floor number must be greater than zero."
                );
            }
            if (!Grid.IsWalkable(Player.Position))
            {
                throw new ArgumentException(
                    "Player position must be on a walkable tile.",
                    nameof(player)
                );
            }
            if (!Grid.IsWalkable(stairsDown))
            {
                throw new ArgumentException(
                    "Stairs down must be on a walkable tile.",
                    nameof(stairsDown)
                );
            }

            Seed = seed;
            FloorNumber = floorNumber;
            StairsDown = stairsDown;
            TurnNumber = turnNumber;
            Status = status;

            var copiedEnemies = new List<EntityState>();
            foreach (EntityState enemy in enemies)
            {
                if (enemy == null)
                {
                    throw new ArgumentException(
                        "Enemy list cannot contain null entries.",
                        nameof(enemies)
                    );
                }
                if (!Grid.IsWalkable(enemy.Position))
                {
                    throw new ArgumentException(
                        "Enemy positions must be on walkable tiles.",
                        nameof(enemies)
                    );
                }
                if (enemy.IsAlive && Player.IsAlive && enemy.Position == Player.Position)
                {
                    throw new ArgumentException(
                        "Alive enemy cannot overlap alive player.",
                        nameof(enemies)
                    );
                }
                if (enemy.IsAlive && FindAliveEntityAt(copiedEnemies, enemy.Position) != null)
                {
                    throw new ArgumentException(
                        "Alive enemies cannot overlap each other.",
                        nameof(enemies)
                    );
                }

                copiedEnemies.Add(enemy);
            }

            Enemies = copiedEnemies;
        }

        public GameGrid Grid { get; }
        public int Seed { get; }
        public int FloorNumber { get; }
        public GridPosition StairsDown { get; }
        public int TurnNumber { get; private set; }
        public EntityState Player { get; }
        public IReadOnlyList<EntityState> Enemies { get; }
        public RunStatus Status { get; private set; }

        public bool IsOngoing => Status == RunStatus.Ongoing;

        public EntityState GetAliveEnemyAt(GridPosition position)
        {
            return FindAliveEntityAt(Enemies, position);
        }

        public bool IsOccupiedByAliveEntity(GridPosition position)
        {
            return Player.IsAlive && Player.Position == position
                || GetAliveEnemyAt(position) != null;
        }

        public bool IsWalkableAndEmpty(GridPosition position)
        {
            return Grid.IsWalkable(position) && !IsOccupiedByAliveEntity(position);
        }

        internal void AdvanceTurn()
        {
            TurnNumber++;
        }

        internal void RefreshOutcome()
        {
            if (!Player.IsAlive)
            {
                Status = RunStatus.Lost;
                return;
            }

            if (Player.Position == StairsDown && AreAllEnemiesDefeated())
            {
                Status = RunStatus.Won;
            }
        }

        private bool AreAllEnemiesDefeated()
        {
            foreach (EntityState enemy in Enemies)
            {
                if (enemy.IsAlive)
                {
                    return false;
                }
            }

            return true;
        }

        private static EntityState FindAliveEntityAt(
            IEnumerable<EntityState> entities,
            GridPosition position
        )
        {
            foreach (EntityState entity in entities)
            {
                if (entity.IsAlive && entity.Position == position)
                {
                    return entity;
                }
            }

            return null;
        }
    }
}
