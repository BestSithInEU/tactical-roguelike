using System;

namespace TacticalRoguelike.Core
{
    public static class TurnSystem
    {
        public static bool TryMovePlayer(RunState state, int dx, int dy)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (Math.Abs(dx) + Math.Abs(dy) != 1)
            {
                throw new ArgumentException("Player movement must be exactly one cardinal step.");
            }
            if (!state.IsOngoing || !state.Player.IsAlive)
            {
                return false;
            }

            var target = new GridPosition(state.Player.Position.X + dx, state.Player.Position.Y + dy);
            EntityState enemy = state.GetAliveEnemyAt(target);
            if (enemy != null)
            {
                CombatSystem.Attack(state.Player, enemy);
                CompletePlayerTurn(state);
                return true;
            }

            if (!state.Grid.IsWalkable(target) || state.IsOccupiedByAliveEntity(target))
            {
                return false;
            }

            state.Player.Position = target;
            CompletePlayerTurn(state);
            return true;
        }

        public static bool WaitPlayerTurn(RunState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (!state.IsOngoing || !state.Player.IsAlive)
            {
                return false;
            }

            CompletePlayerTurn(state);
            return true;
        }

        private static void CompletePlayerTurn(RunState state)
        {
            state.AdvanceTurn();
            state.RefreshOutcome();
            if (!state.IsOngoing)
            {
                return;
            }

            RunEnemyTurns(state);
            state.RefreshOutcome();
        }

        private static void RunEnemyTurns(RunState state)
        {
            var enemyAI = new EnemyAI();
            foreach (EntityState enemy in state.Enemies)
            {
                enemyAI.Act(state, enemy);
                if (!state.IsOngoing)
                {
                    return;
                }
            }
        }
    }
}
