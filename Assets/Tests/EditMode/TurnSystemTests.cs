using NUnit.Framework;
using TacticalRoguelike.Core;

namespace TacticalRoguelike.Tests.EditMode
{
    public sealed class TurnSystemTests
    {
        [Test]
        public void TryMovePlayer_FloorTile_MovesPlayerAdvancesTurnAndEnemyActs()
        {
            RunState state = CreateRunState(new GridPosition(1, 1), new GridPosition(4, 4));

            bool moved = TurnSystem.TryMovePlayer(state, 1, 0);

            Assert.IsTrue(moved);
            Assert.AreEqual(new GridPosition(2, 1), state.Player.Position);
            Assert.AreEqual(1, state.TurnNumber);
            Assert.AreEqual(new GridPosition(4, 3), state.Enemies[0].Position);
        }

        [Test]
        public void TryMovePlayer_WallTile_DoesNotMoveOrAdvanceTurn()
        {
            GameGrid grid = CreateFloorGrid();
            grid.SetTile(new GridPosition(2, 1), GridTileKind.Wall);
            RunState state = CreateRunState(grid, new GridPosition(1, 1), new GridPosition(4, 4));

            bool moved = TurnSystem.TryMovePlayer(state, 1, 0);

            Assert.IsFalse(moved);
            Assert.AreEqual(new GridPosition(1, 1), state.Player.Position);
            Assert.AreEqual(new GridPosition(4, 4), state.Enemies[0].Position);
            Assert.AreEqual(0, state.TurnNumber);
        }

        [Test]
        public void TryMovePlayer_IntoAdjacentEnemy_AttacksInsteadOfMoving()
        {
            RunState state = CreateRunState(new GridPosition(1, 1), new GridPosition(2, 1));

            bool acted = TurnSystem.TryMovePlayer(state, 1, 0);

            Assert.IsTrue(acted);
            Assert.AreEqual(new GridPosition(1, 1), state.Player.Position);
            Assert.IsFalse(state.Enemies[0].IsAlive);
            Assert.AreEqual(1, state.TurnNumber);
        }

        [Test]
        public void WaitPlayerTurn_EnemyNotAdjacent_ChasesOneStepTowardPlayer()
        {
            RunState state = CreateRunState(new GridPosition(1, 1), new GridPosition(4, 4));

            bool acted = TurnSystem.WaitPlayerTurn(state);

            Assert.IsTrue(acted);
            Assert.AreEqual(new GridPosition(4, 3), state.Enemies[0].Position);
            Assert.AreEqual(1, state.TurnNumber);
        }

        [Test]
        public void WaitPlayerTurn_EnemyBehindWall_DoesNotChasePlayer()
        {
            GameGrid grid = CreateFloorGrid();
            grid.SetTile(new GridPosition(2, 1), GridTileKind.Wall);
            grid.SetTile(new GridPosition(4, 2), GridTileKind.Wall);
            grid.SetTile(new GridPosition(5, 1), GridTileKind.Wall);
            grid.SetTile(new GridPosition(4, 0), GridTileKind.Wall);
            grid.SetTile(new GridPosition(3, 1), GridTileKind.Wall);
            RunState state = CreateRunState(grid, new GridPosition(1, 1), new GridPosition(4, 1));

            bool acted = TurnSystem.WaitPlayerTurn(state);

            Assert.IsTrue(acted);
            Assert.AreEqual(new GridPosition(4, 1), state.Enemies[0].Position);
            Assert.AreEqual(1, state.TurnNumber);
        }

        [Test]
        public void WaitPlayerTurn_AdjacentEnemy_AttacksPlayer()
        {
            RunState state = CreateRunState(new GridPosition(1, 1), new GridPosition(2, 1));

            bool acted = TurnSystem.WaitPlayerTurn(state);

            Assert.IsTrue(acted);
            Assert.AreEqual(8, state.Player.HitPoints);
            Assert.AreEqual(new GridPosition(2, 1), state.Enemies[0].Position);
            Assert.AreEqual(1, state.TurnNumber);
        }

        [Test]
        public void WaitPlayerTurn_AdjacentEnemy_AttacksEvenWhenBlockedByNearbyWalls()
        {
            GameGrid grid = CreateFloorGrid();
            grid.SetTile(new GridPosition(0, 1), GridTileKind.Wall);
            grid.SetTile(new GridPosition(1, 0), GridTileKind.Wall);
            grid.SetTile(new GridPosition(2, 0), GridTileKind.Wall);
            grid.SetTile(new GridPosition(3, 1), GridTileKind.Wall);
            RunState state = CreateRunState(grid, new GridPosition(1, 1), new GridPosition(2, 1));

            bool acted = TurnSystem.WaitPlayerTurn(state);

            Assert.IsTrue(acted);
            Assert.AreEqual(8, state.Player.HitPoints);
            Assert.AreEqual(new GridPosition(2, 1), state.Enemies[0].Position);
        }

        [Test]
        public void TryMovePlayer_DeadEnemy_NoLongerBlocksMovement()
        {
            RunState state = CreateRunState(new GridPosition(1, 1), new GridPosition(2, 1));
            TurnSystem.TryMovePlayer(state, 1, 0);

            bool movedIntoDeadEnemyTile = TurnSystem.TryMovePlayer(state, 1, 0);

            Assert.IsTrue(movedIntoDeadEnemyTile);
            Assert.AreEqual(new GridPosition(2, 1), state.Player.Position);
            Assert.AreEqual(2, state.TurnNumber);
        }

        [Test]
        public void EnemyAttack_WhenPlayerHitPointsReachZero_SetsLostStatus()
        {
            RunState state = CreateRunState(new GridPosition(1, 1), new GridPosition(2, 1));

            for (int i = 0; i < 5; i++)
            {
                TurnSystem.WaitPlayerTurn(state);
            }

            Assert.IsFalse(state.Player.IsAlive);
            Assert.AreEqual(RunStatus.Lost, state.Status);
        }

        [Test]
        public void TryMovePlayer_OntoStairsAfterDefeatingEnemies_SetsWonStatus()
        {
            RunState state = CreateRunState(new GridPosition(1, 1), new GridPosition(2, 1), new GridPosition(2, 1));

            bool acted = TurnSystem.TryMovePlayer(state, 1, 0);

            Assert.IsTrue(acted);
            Assert.IsFalse(state.Enemies[0].IsAlive);
            Assert.AreEqual(new GridPosition(1, 1), state.Player.Position);
            Assert.AreEqual(RunStatus.Ongoing, state.Status);

            bool movedToStairs = TurnSystem.TryMovePlayer(state, 1, 0);

            Assert.IsTrue(movedToStairs);
            Assert.AreEqual(new GridPosition(2, 1), state.Player.Position);
            Assert.AreEqual(RunStatus.Won, state.Status);
        }

        private static RunState CreateRunState(GridPosition playerSpawn, GridPosition enemySpawn)
        {
            return CreateRunState(playerSpawn, enemySpawn, new GridPosition(5, 5));
        }

        private static RunState CreateRunState(GridPosition playerSpawn, GridPosition enemySpawn, GridPosition stairsDown)
        {
            return CreateRunState(CreateFloorGrid(), playerSpawn, enemySpawn, stairsDown);
        }

        private static RunState CreateRunState(GameGrid grid, GridPosition playerSpawn, GridPosition enemySpawn)
        {
            return CreateRunState(grid, playerSpawn, enemySpawn, new GridPosition(5, 5));
        }

        private static RunState CreateRunState(GameGrid grid, GridPosition playerSpawn, GridPosition enemySpawn, GridPosition stairsDown)
        {
            var layout = new DungeonLayout(grid, 1234, playerSpawn, new[] { enemySpawn }, stairsDown);
            return new RunState(layout);
        }

        private static GameGrid CreateFloorGrid()
        {
            return new GameGrid(6, 6, GridTileKind.Floor);
        }
    }
}
