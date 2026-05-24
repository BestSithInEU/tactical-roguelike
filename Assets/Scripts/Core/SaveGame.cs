using System;
using System.Collections.Generic;

namespace TacticalRoguelike.Core
{
    [Serializable]
    public sealed class SaveGame
    {
        public const int CurrentSaveVersion = 1;

        public int saveVersion = CurrentSaveVersion;
        public int seed;
        public int floorNumber;
        public int turnNumber;
        public int runStatus;
        public int width;
        public int height;
        public int[] tiles;
        public SavePosition stairsDown;
        public SaveEntity player;
        public SaveEntity[] enemies;

        public static SaveGame Capture(RunState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var save = new SaveGame
            {
                saveVersion = CurrentSaveVersion,
                seed = state.Seed,
                floorNumber = state.FloorNumber,
                turnNumber = state.TurnNumber,
                runStatus = (int)state.Status,
                width = state.Grid.Width,
                height = state.Grid.Height,
                tiles = new int[state.Grid.Width * state.Grid.Height],
                stairsDown = SavePosition.FromGridPosition(state.StairsDown),
                player = SaveEntity.Capture(state.Player),
                enemies = new SaveEntity[state.Enemies.Count],
            };

            for (int y = 0; y < state.Grid.Height; y++)
            {
                for (int x = 0; x < state.Grid.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    save.tiles[y * state.Grid.Width + x] = (int)state.Grid.GetTile(position);
                }
            }

            for (int i = 0; i < state.Enemies.Count; i++)
            {
                save.enemies[i] = SaveEntity.Capture(state.Enemies[i]);
            }

            return save;
        }

        public RunState Restore()
        {
            Validate();

            var grid = new GameGrid(width, height, GridTileKind.Wall);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    grid.SetTile(new GridPosition(x, y), (GridTileKind)tiles[y * width + x]);
                }
            }

            var restoredEnemies = new List<EntityState>(enemies.Length);
            for (int i = 0; i < enemies.Length; i++)
            {
                restoredEnemies.Add(enemies[i].Restore());
            }

            return new RunState(
                grid,
                seed,
                floorNumber,
                stairsDown.ToGridPosition(),
                player.Restore(),
                restoredEnemies,
                turnNumber,
                (RunStatus)runStatus
            );
        }

        private void Validate()
        {
            if (saveVersion != CurrentSaveVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported save version {saveVersion}. Expected version {CurrentSaveVersion}."
                );
            }
            if (floorNumber < 1)
            {
                throw new InvalidOperationException("Save floor number must be greater than zero.");
            }
            if (width <= 0)
            {
                throw new InvalidOperationException("Save width must be greater than zero.");
            }
            if (height <= 0)
            {
                throw new InvalidOperationException("Save height must be greater than zero.");
            }
            if (tiles == null || tiles.Length != width * height)
            {
                throw new InvalidOperationException(
                    "Save tile data does not match grid dimensions."
                );
            }
            if (player == null)
            {
                throw new InvalidOperationException("Save is missing player state.");
            }
            if (enemies == null)
            {
                throw new InvalidOperationException("Save is missing enemy state.");
            }
            if (turnNumber < 0)
            {
                throw new InvalidOperationException("Save turn number cannot be negative.");
            }
        }
    }

    [Serializable]
    public sealed class SavePosition
    {
        public int x;
        public int y;

        public static SavePosition FromGridPosition(GridPosition position)
        {
            return new SavePosition { x = position.X, y = position.Y };
        }

        public GridPosition ToGridPosition()
        {
            return new GridPosition(x, y);
        }
    }

    [Serializable]
    public sealed class SaveEntity
    {
        public string id;
        public SavePosition position;
        public SavePosition homePosition;
        public int maxHitPoints;
        public int hitPoints;
        public int attackDamage;
        public bool isAlerted;
        public bool hasLastKnownPlayerPosition;
        public SavePosition lastKnownPlayerPosition;
        public int searchTurnsRemaining;
        public bool isReturningHome;
        public int patrolStepIndex;

        public static SaveEntity Capture(EntityState entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return new SaveEntity
            {
                id = entity.Id,
                position = SavePosition.FromGridPosition(entity.Position),
                homePosition = SavePosition.FromGridPosition(entity.HomePosition),
                maxHitPoints = entity.MaxHitPoints,
                hitPoints = entity.HitPoints,
                attackDamage = entity.AttackDamage,
                isAlerted = entity.IsAlerted,
                hasLastKnownPlayerPosition = entity.LastKnownPlayerPosition.HasValue,
                lastKnownPlayerPosition = entity.LastKnownPlayerPosition.HasValue
                    ? SavePosition.FromGridPosition(entity.LastKnownPlayerPosition.Value)
                    : null,
                searchTurnsRemaining = entity.SearchTurnsRemaining,
                isReturningHome = entity.IsReturningHome,
                patrolStepIndex = entity.PatrolStepIndex,
            };
        }

        public EntityState Restore()
        {
            if (position == null)
            {
                throw new InvalidOperationException("Save entity is missing position.");
            }

            return new EntityState(
                id,
                position.ToGridPosition(),
                maxHitPoints,
                hitPoints,
                attackDamage,
                isAlerted,
                hasLastKnownPlayerPosition && lastKnownPlayerPosition != null
                    ? lastKnownPlayerPosition.ToGridPosition()
                    : (GridPosition?)null,
                searchTurnsRemaining,
                homePosition != null ? homePosition.ToGridPosition() : position.ToGridPosition(),
                isReturningHome,
                patrolStepIndex
            );
        }
    }
}
