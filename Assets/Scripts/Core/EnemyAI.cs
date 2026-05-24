using System;
using System.Collections.Generic;

namespace TacticalRoguelike.Core
{
    public sealed class EnemyAI
    {
        public const int DefaultSightRange = 8;
        public const int DefaultSearchTurns = 2;

        private readonly Pathfinding pathfinding;
        private readonly int sightRange;
        private readonly int searchTurns;

        public EnemyAI()
            : this(new Pathfinding(), DefaultSightRange, DefaultSearchTurns) { }

        public EnemyAI(Pathfinding pathfinding)
            : this(pathfinding, DefaultSightRange, DefaultSearchTurns) { }

        public EnemyAI(Pathfinding pathfinding, int sightRange)
            : this(pathfinding, sightRange, DefaultSearchTurns) { }

        public EnemyAI(Pathfinding pathfinding, int sightRange, int searchTurns)
        {
            this.pathfinding = pathfinding ?? throw new ArgumentNullException(nameof(pathfinding));
            if (sightRange < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sightRange),
                    sightRange,
                    "Sight range cannot be negative."
                );
            }
            if (searchTurns < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(searchTurns),
                    searchTurns,
                    "Search turns cannot be negative."
                );
            }

            this.sightRange = sightRange;
            this.searchTurns = searchTurns;
        }

        public void Act(RunState state, EntityState enemy)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (enemy == null)
            {
                throw new ArgumentNullException(nameof(enemy));
            }

            if (!state.IsOngoing || !enemy.IsAlive || !state.Player.IsAlive)
            {
                return;
            }

            if (AreAdjacent(enemy.Position, state.Player.Position))
            {
                CombatSystem.Attack(enemy, state.Player);
                state.RefreshOutcome();
                return;
            }

            if (
                FieldOfView.HasLineOfSight(
                    state.Grid,
                    enemy.Position,
                    state.Player.Position,
                    sightRange
                )
            )
            {
                enemy.ObservePlayer(state.Player.Position, searchTurns);
                MoveTowardVisiblePlayer(state, enemy);
                return;
            }

            if (!enemy.LastKnownPlayerPosition.HasValue)
            {
                ActUnalerted(state, enemy);
                return;
            }

            GridPosition lastKnownPosition = enemy.LastKnownPlayerPosition.Value;
            if (enemy.Position == lastKnownPosition)
            {
                enemy.SpendSearchTurn();
                return;
            }

            if (!MoveTowardLastKnownPosition(state, enemy, lastKnownPosition))
            {
                enemy.SpendSearchTurn();
            }
        }

        public static bool AreAdjacent(GridPosition first, GridPosition second)
        {
            return Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y) == 1;
        }

        private bool MoveTowardVisiblePlayer(RunState state, EntityState enemy)
        {
            IReadOnlyList<GridPosition> path = FindBestMovingPath(
                state,
                enemy,
                state.Player.Position,
                true,
                false
            );
            if (path.Count == 0)
            {
                path = FindBestMovingPath(state, enemy, state.Player.Position, false, true);
            }

            return MoveOneStepAlongPath(state, enemy, path);
        }

        private bool MoveTowardLastKnownPosition(
            RunState state,
            EntityState enemy,
            GridPosition destination
        )
        {
            IReadOnlyList<GridPosition> path = FindBestMovingPath(
                state,
                enemy,
                destination,
                false,
                true
            );
            return MoveOneStepAlongPath(state, enemy, path);
        }

        private void ActUnalerted(RunState state, EntityState enemy)
        {
            if (enemy.IsReturningHome)
            {
                if (enemy.Position == enemy.HomePosition || !MoveTowardHome(state, enemy))
                {
                    enemy.FinishReturnHome();
                }

                return;
            }

            Patrol(state, enemy);
        }

        private bool MoveTowardHome(RunState state, EntityState enemy)
        {
            IReadOnlyList<GridPosition> path = FindBestMovingPath(
                state,
                enemy,
                enemy.HomePosition,
                false,
                true
            );
            return MoveOneStepAlongPath(state, enemy, path);
        }

        private void Patrol(RunState state, EntityState enemy)
        {
            List<GridPosition> patrolPoints = GetPatrolPoints(state, enemy);
            if (patrolPoints.Count < 2)
            {
                return;
            }

            for (int attempt = 0; attempt < patrolPoints.Count; attempt++)
            {
                int targetIndex = enemy.PatrolStepIndex % patrolPoints.Count;
                GridPosition target = patrolPoints[targetIndex];
                if (target == enemy.Position)
                {
                    enemy.AdvancePatrolStep(patrolPoints.Count);
                    continue;
                }

                IReadOnlyList<GridPosition> path = FindExactMovingPath(state, enemy, target);
                if (MoveOneStepAlongPath(state, enemy, path))
                {
                    if (enemy.Position == target)
                    {
                        enemy.AdvancePatrolStep(patrolPoints.Count);
                    }

                    return;
                }

                enemy.AdvancePatrolStep(patrolPoints.Count);
            }
        }

        private IReadOnlyList<GridPosition> FindExactMovingPath(
            RunState state,
            EntityState enemy,
            GridPosition destination
        )
        {
            if (destination == enemy.Position)
            {
                return Array.Empty<GridPosition>();
            }

            GameGrid pathGrid = CreatePathGrid(state, enemy);
            if (!pathGrid.IsWalkable(destination))
            {
                return Array.Empty<GridPosition>();
            }

            IReadOnlyList<GridPosition> path = pathfinding.FindPath(
                pathGrid,
                enemy.Position,
                destination
            );
            return path.Count >= 2 ? path : Array.Empty<GridPosition>();
        }

        private IReadOnlyList<GridPosition> FindBestMovingPath(
            RunState state,
            EntityState enemy,
            GridPosition destination,
            bool requireAdjacentToDestination,
            bool requireDistanceImprovement
        )
        {
            GameGrid pathGrid = CreatePathGrid(state, enemy);
            IReadOnlyList<GridPosition> bestPath = Array.Empty<GridPosition>();
            int bestDistanceToDestination = int.MaxValue;
            int bestPathLength = int.MaxValue;
            int currentDistanceToDestination = ManhattanDistance(enemy.Position, destination);

            foreach (GridPosition candidate in pathGrid.Positions())
            {
                if (candidate == enemy.Position || !pathGrid.IsWalkable(candidate))
                {
                    continue;
                }

                int distanceToDestination = ManhattanDistance(candidate, destination);
                if (requireAdjacentToDestination && distanceToDestination != 1)
                {
                    continue;
                }

                if (
                    requireDistanceImprovement
                    && distanceToDestination >= currentDistanceToDestination
                )
                {
                    continue;
                }

                if (
                    !requireAdjacentToDestination
                    && distanceToDestination > bestDistanceToDestination
                )
                {
                    continue;
                }

                IReadOnlyList<GridPosition> path = pathfinding.FindPath(
                    pathGrid,
                    enemy.Position,
                    candidate
                );
                if (path.Count < 2)
                {
                    continue;
                }

                bool isBetter = requireAdjacentToDestination
                    ? path.Count < bestPathLength
                        || (
                            path.Count == bestPathLength
                            && distanceToDestination < bestDistanceToDestination
                        )
                    : distanceToDestination < bestDistanceToDestination
                        || (
                            distanceToDestination == bestDistanceToDestination
                            && path.Count < bestPathLength
                        );

                if (isBetter)
                {
                    bestPath = path;
                    bestDistanceToDestination = distanceToDestination;
                    bestPathLength = path.Count;
                }
            }

            return bestPath;
        }

        private static bool MoveOneStepAlongPath(
            RunState state,
            EntityState enemy,
            IReadOnlyList<GridPosition> path
        )
        {
            if (path.Count < 2)
            {
                return false;
            }

            GridPosition nextStep = path[1];
            if (state.Grid.IsWalkable(nextStep) && !state.IsOccupiedByAliveEntity(nextStep))
            {
                enemy.Position = nextStep;
                return true;
            }

            return false;
        }

        private static int ManhattanDistance(GridPosition first, GridPosition second)
        {
            return Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
        }

        private static List<GridPosition> GetPatrolPoints(RunState state, EntityState enemy)
        {
            var points = new List<GridPosition>();
            AddPatrolPointIfAvailable(state, enemy, points, enemy.HomePosition);
            foreach (GridPosition neighbor in enemy.HomePosition.CardinalNeighbors())
            {
                AddPatrolPointIfAvailable(state, enemy, points, neighbor);
            }

            return points;
        }

        private static void AddPatrolPointIfAvailable(
            RunState state,
            EntityState enemy,
            List<GridPosition> points,
            GridPosition position
        )
        {
            if (!state.Grid.IsWalkable(position))
            {
                return;
            }

            EntityState occupyingEnemy = state.GetAliveEnemyAt(position);
            if (
                (state.Player.IsAlive && state.Player.Position == position)
                || (occupyingEnemy != null && occupyingEnemy != enemy)
            )
            {
                return;
            }

            points.Add(position);
        }

        private static GameGrid CreatePathGrid(RunState state, EntityState movingEnemy)
        {
            var pathGrid = new GameGrid(state.Grid.Width, state.Grid.Height, GridTileKind.Wall);
            foreach (GridPosition position in state.Grid.Positions())
            {
                pathGrid.SetTile(position, state.Grid.GetTile(position));
            }

            foreach (EntityState enemy in state.Enemies)
            {
                if (enemy.IsAlive && enemy != movingEnemy)
                {
                    pathGrid.SetTile(enemy.Position, GridTileKind.Wall);
                }
            }

            if (state.Player.IsAlive)
            {
                pathGrid.SetTile(state.Player.Position, GridTileKind.Wall);
            }

            return pathGrid;
        }
    }
}
