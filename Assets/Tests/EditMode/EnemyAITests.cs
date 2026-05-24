using NUnit.Framework;
using TacticalRoguelike.Core;

namespace TacticalRoguelike.Tests.EditMode
{
    public sealed class EnemyAITests
    {
        [Test]
        public void Act_HiddenPlayerWithoutMemoryAndNoPatrolRoute_EnemyDoesNotMove()
        {
            GameGrid grid = CreateFloorGrid();
            grid.SetTile(new GridPosition(2, 2), GridTileKind.Wall);
            grid.SetTile(new GridPosition(5, 3), GridTileKind.Wall);
            grid.SetTile(new GridPosition(6, 2), GridTileKind.Wall);
            grid.SetTile(new GridPosition(5, 1), GridTileKind.Wall);
            grid.SetTile(new GridPosition(4, 2), GridTileKind.Wall);
            RunState state = CreateRunState(grid, new GridPosition(1, 2), new GridPosition(5, 2));
            EntityState enemy = state.Enemies[0];

            new EnemyAI().Act(state, enemy);

            Assert.AreEqual(new GridPosition(5, 2), enemy.Position);
            Assert.IsFalse(enemy.IsAlerted);
            Assert.IsFalse(enemy.LastKnownPlayerPosition.HasValue);
        }

        [Test]
        public void Act_HiddenPlayerWithoutMemory_PatrolsDeterministicallyAroundHome()
        {
            GameGrid grid = CreateFloorGrid();
            grid.SetTile(new GridPosition(2, 2), GridTileKind.Wall);
            RunState state = CreateRunState(grid, new GridPosition(1, 2), new GridPosition(5, 2));
            EntityState enemy = state.Enemies[0];

            new EnemyAI().Act(state, enemy);

            Assert.AreEqual(new GridPosition(5, 3), enemy.Position);
            Assert.IsFalse(enemy.IsAlerted);
            Assert.IsFalse(enemy.IsReturningHome);
        }

        [Test]
        public void Act_ReturningHome_MovesTowardHomeAfterSearchExpires()
        {
            GameGrid grid = CreateFloorGrid();
            grid.SetTile(new GridPosition(1, 1), GridTileKind.Wall);
            RunState state = CreateRunState(grid, new GridPosition(0, 0), new GridPosition(5, 2));
            EntityState enemy = state.Enemies[0];
            enemy.Position = new GridPosition(2, 3);
            enemy.BeginReturnHome();

            new EnemyAI().Act(state, enemy);

            Assert.AreEqual(new GridPosition(3, 3), enemy.Position);
            Assert.IsTrue(enemy.IsReturningHome);
            Assert.IsFalse(enemy.IsAlerted);
        }

        [Test]
        public void Act_AtHomeAfterReturning_ResumesPatrol()
        {
            GameGrid grid = CreateFloorGrid();
            grid.SetTile(new GridPosition(1, 1), GridTileKind.Wall);
            RunState state = CreateRunState(grid, new GridPosition(0, 0), new GridPosition(5, 2));
            EntityState enemy = state.Enemies[0];
            enemy.BeginReturnHome();
            var enemyAI = new EnemyAI();

            enemyAI.Act(state, enemy);
            Assert.AreEqual(new GridPosition(5, 2), enemy.Position);
            Assert.IsFalse(enemy.IsReturningHome);

            enemyAI.Act(state, enemy);
            Assert.AreEqual(new GridPosition(5, 3), enemy.Position);
        }

        [Test]
        public void Act_ReturningHomeButPlayerVisible_RealertsAndChases()
        {
            GameGrid grid = CreateFloorGrid();
            RunState state = CreateRunState(grid, new GridPosition(1, 2), new GridPosition(5, 2));
            EntityState enemy = state.Enemies[0];
            enemy.BeginReturnHome();

            new EnemyAI().Act(state, enemy);

            Assert.IsTrue(enemy.IsAlerted);
            Assert.IsFalse(enemy.IsReturningHome);
            Assert.AreEqual(new GridPosition(4, 2), enemy.Position);
            Assert.AreEqual(new GridPosition(1, 2), enemy.LastKnownPlayerPosition.Value);
        }

        [Test]
        public void Act_PlayerSeenThenHidden_EnemyMovesTowardLastKnownPosition()
        {
            GameGrid grid = CreateFloorGrid();
            RunState state = CreateRunState(grid, new GridPosition(1, 2), new GridPosition(5, 2));
            EntityState enemy = state.Enemies[0];
            var enemyAI = new EnemyAI();
            enemyAI.Act(state, enemy);
            Assert.AreEqual(new GridPosition(4, 2), enemy.Position);
            Assert.AreEqual(new GridPosition(1, 2), enemy.LastKnownPlayerPosition.Value);

            grid.SetTile(new GridPosition(3, 2), GridTileKind.Wall);
            state.Player.Position = new GridPosition(1, 1);
            enemyAI.Act(state, enemy);

            Assert.AreEqual(new GridPosition(4, 3), enemy.Position);
            Assert.IsTrue(enemy.IsAlerted);
            Assert.AreEqual(new GridPosition(1, 2), enemy.LastKnownPlayerPosition.Value);
        }

        [Test]
        public void Act_VisiblePlayer_ChasesToAdjacentTileBeforeAttacking()
        {
            GameGrid grid = CreateFloorGrid();
            RunState state = CreateRunState(grid, new GridPosition(1, 2), new GridPosition(5, 2));
            EntityState enemy = state.Enemies[0];
            var enemyAI = new EnemyAI();

            enemyAI.Act(state, enemy);
            enemyAI.Act(state, enemy);
            enemyAI.Act(state, enemy);

            Assert.AreEqual(new GridPosition(2, 2), enemy.Position);
            Assert.AreEqual(10, state.Player.HitPoints);

            enemyAI.Act(state, enemy);

            Assert.AreEqual(new GridPosition(2, 2), enemy.Position);
            Assert.AreEqual(8, state.Player.HitPoints);
        }

        [Test]
        public void Act_VisiblePlayerWithBlockedAdjacentGoals_MovesTowardClosestReachableTile()
        {
            GameGrid grid = CreateFloorGrid();
            grid.SetTile(new GridPosition(0, 1), GridTileKind.Wall);
            grid.SetTile(new GridPosition(1, 0), GridTileKind.Wall);
            grid.SetTile(new GridPosition(1, 2), GridTileKind.Wall);
            grid.SetTile(new GridPosition(2, 1), GridTileKind.Wall);
            RunState state = CreateRunState(grid, new GridPosition(1, 1), new GridPosition(5, 3));
            EntityState enemy = state.Enemies[0];

            new EnemyAI().Act(state, enemy);

            Assert.AreEqual(new GridPosition(4, 3), enemy.Position);
            Assert.IsTrue(enemy.IsAlerted);
            Assert.AreEqual(new GridPosition(1, 1), enemy.LastKnownPlayerPosition.Value);
        }

        [Test]
        public void Act_LastKnownBehindCorner_EnemyKeepsMovingAlongReachableRoute()
        {
            GameGrid grid = CreateFloorGrid();
            RunState state = CreateRunState(grid, new GridPosition(1, 2), new GridPosition(5, 2));
            EntityState enemy = state.Enemies[0];
            var enemyAI = new EnemyAI();
            enemyAI.Act(state, enemy);

            grid.SetTile(new GridPosition(3, 2), GridTileKind.Wall);
            state.Player.Position = new GridPosition(1, 1);

            enemyAI.Act(state, enemy);
            Assert.AreEqual(new GridPosition(4, 3), enemy.Position);

            enemyAI.Act(state, enemy);
            Assert.AreEqual(new GridPosition(3, 3), enemy.Position);

            enemyAI.Act(state, enemy);
            Assert.AreEqual(new GridPosition(2, 3), enemy.Position);
            Assert.IsTrue(enemy.IsAlerted);
            Assert.AreEqual(new GridPosition(1, 2), enemy.LastKnownPlayerPosition.Value);
        }

        [Test]
        public void Act_UnreachableLastKnownPosition_SearchesThenForgets()
        {
            GameGrid grid = CreateFloorGrid();
            for (int y = 0; y < grid.Height; y++)
            {
                grid.SetTile(new GridPosition(3, y), GridTileKind.Wall);
            }

            RunState state = CreateRunState(grid, new GridPosition(1, 1), new GridPosition(5, 2));
            EntityState enemy = state.Enemies[0];
            enemy.ObservePlayer(new GridPosition(1, 1), EnemyAI.DefaultSearchTurns);
            var enemyAI = new EnemyAI();

            enemyAI.Act(state, enemy);
            Assert.IsTrue(enemy.IsAlerted);
            Assert.AreEqual(1, enemy.SearchTurnsRemaining);
            Assert.AreEqual(new GridPosition(5, 2), enemy.Position);

            enemyAI.Act(state, enemy);
            Assert.IsFalse(enemy.IsAlerted);
            Assert.IsFalse(enemy.LastKnownPlayerPosition.HasValue);
            Assert.IsTrue(enemy.IsReturningHome);
            Assert.AreEqual(new GridPosition(5, 2), enemy.Position);
        }

        [Test]
        public void Act_AtLastKnownPositionWithoutSight_SearchesThenForgets()
        {
            GameGrid grid = CreateFloorGrid();
            grid.SetTile(new GridPosition(3, 2), GridTileKind.Wall);
            RunState state = CreateRunState(grid, new GridPosition(1, 2), new GridPosition(5, 2));
            EntityState enemy = state.Enemies[0];
            enemy.ObservePlayer(enemy.Position, EnemyAI.DefaultSearchTurns);
            var enemyAI = new EnemyAI();

            enemyAI.Act(state, enemy);

            Assert.IsTrue(enemy.IsAlerted);
            Assert.AreEqual(1, enemy.SearchTurnsRemaining);
            Assert.AreEqual(new GridPosition(5, 2), enemy.Position);

            enemyAI.Act(state, enemy);

            Assert.IsFalse(enemy.IsAlerted);
            Assert.IsFalse(enemy.LastKnownPlayerPosition.HasValue);
            Assert.AreEqual(0, enemy.SearchTurnsRemaining);
            Assert.IsTrue(enemy.IsReturningHome);
        }

        [Test]
        public void Act_PlayerBecomesVisibleAgain_UpdatesLastKnownPosition()
        {
            GameGrid grid = CreateFloorGrid();
            RunState state = CreateRunState(grid, new GridPosition(2, 2), new GridPosition(5, 2));
            EntityState enemy = state.Enemies[0];
            enemy.ObservePlayer(new GridPosition(1, 1), EnemyAI.DefaultSearchTurns);

            new EnemyAI().Act(state, enemy);

            Assert.AreEqual(new GridPosition(2, 2), enemy.LastKnownPlayerPosition.Value);
            Assert.AreEqual(new GridPosition(4, 2), enemy.Position);
            Assert.IsTrue(enemy.IsAlerted);
            Assert.AreEqual(EnemyAI.DefaultSearchTurns, enemy.SearchTurnsRemaining);
        }

        [Test]
        public void Act_AdjacentEnemy_AttacksEvenWithoutMemory()
        {
            GameGrid grid = CreateFloorGrid();
            RunState state = CreateRunState(grid, new GridPosition(1, 2), new GridPosition(2, 2));
            EntityState enemy = state.Enemies[0];

            new EnemyAI().Act(state, enemy);

            Assert.AreEqual(8, state.Player.HitPoints);
            Assert.AreEqual(new GridPosition(2, 2), enemy.Position);
        }

        private static RunState CreateRunState(
            GameGrid grid,
            GridPosition playerSpawn,
            GridPosition enemySpawn
        )
        {
            var layout = new DungeonLayout(
                grid,
                1234,
                playerSpawn,
                new[] { enemySpawn },
                new GridPosition(6, 4)
            );
            return new RunState(layout);
        }

        private static GameGrid CreateFloorGrid()
        {
            return new GameGrid(8, 5, GridTileKind.Floor);
        }
    }
}
