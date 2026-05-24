using NUnit.Framework;
using TacticalRoguelike.Core;

namespace TacticalRoguelike.Tests.EditMode
{
    public sealed class SaveGameRoundTripTests
    {
        [Test]
        public void Restore_PreservesRunGridEntitiesAndStatus()
        {
            GameGrid grid = CreateGrid();
            var state = CreateRunState(
                grid,
                new GridPosition(1, 1),
                new GridPosition(2, 1),
                new GridPosition(2, 1)
            );
            TurnSystem.TryMovePlayer(state, 1, 0);
            TurnSystem.TryMovePlayer(state, 1, 0);

            RunState restored = SaveGame.Capture(state).Restore();

            Assert.AreEqual(state.Seed, restored.Seed);
            Assert.AreEqual(SaveGame.CurrentSaveVersion, SaveGame.Capture(state).saveVersion);
            Assert.AreEqual(state.FloorNumber, restored.FloorNumber);
            Assert.AreEqual(state.TurnNumber, restored.TurnNumber);
            Assert.AreEqual(state.Status, restored.Status);
            Assert.AreEqual(state.StairsDown, restored.StairsDown);
            AssertGridEqual(state.Grid, restored.Grid);
            AssertEntityEqual(state.Player, restored.Player);
            Assert.AreEqual(state.Enemies.Count, restored.Enemies.Count);
            AssertEntityEqual(state.Enemies[0], restored.Enemies[0]);
            Assert.IsFalse(restored.Enemies[0].IsAlive);
            Assert.AreEqual(RunStatus.Won, restored.Status);
        }

        [Test]
        public void Restore_CurrentSaveVersion_RestoresRun()
        {
            GameGrid grid = CreateGrid();
            var state = CreateRunState(
                grid,
                new GridPosition(1, 1),
                new GridPosition(4, 1),
                new GridPosition(5, 3)
            );
            SaveGame save = SaveGame.Capture(state);

            RunState restored = save.Restore();

            Assert.AreEqual(SaveGame.CurrentSaveVersion, save.saveVersion);
            Assert.AreEqual(state.Seed, restored.Seed);
            Assert.AreEqual(state.FloorNumber, restored.FloorNumber);
        }

        [Test]
        public void Restore_UnknownSaveVersion_ThrowsClearException()
        {
            GameGrid grid = CreateGrid();
            var state = CreateRunState(
                grid,
                new GridPosition(1, 1),
                new GridPosition(4, 1),
                new GridPosition(5, 3)
            );
            SaveGame save = SaveGame.Capture(state);
            save.saveVersion = SaveGame.CurrentSaveVersion + 1;

            var exception = Assert.Throws<System.InvalidOperationException>(() => save.Restore());

            StringAssert.Contains("Unsupported save version", exception.Message);
            StringAssert.Contains(save.saveVersion.ToString(), exception.Message);
        }

        [Test]
        public void Restore_ZeroSaveVersion_ThrowsClearException()
        {
            GameGrid grid = CreateGrid();
            var state = CreateRunState(
                grid,
                new GridPosition(1, 1),
                new GridPosition(4, 1),
                new GridPosition(5, 3)
            );
            SaveGame save = SaveGame.Capture(state);
            save.saveVersion = 0;

            var exception = Assert.Throws<System.InvalidOperationException>(() => save.Restore());

            StringAssert.Contains("Unsupported save version", exception.Message);
            StringAssert.Contains("0", exception.Message);
        }

        [Test]
        public void Restore_ZeroFloorNumber_ThrowsClearException()
        {
            GameGrid grid = CreateGrid();
            var state = CreateRunState(
                grid,
                new GridPosition(1, 1),
                new GridPosition(4, 1),
                new GridPosition(5, 3)
            );
            SaveGame save = SaveGame.Capture(state);
            save.floorNumber = 0;

            var exception = Assert.Throws<System.InvalidOperationException>(() => save.Restore());

            StringAssert.Contains("floor number", exception.Message);
        }

        [Test]
        public void Restore_PreservesFloorNumber()
        {
            GameGrid grid = CreateGrid();
            var layout = new DungeonLayout(
                grid,
                777,
                new GridPosition(1, 1),
                new[] { new GridPosition(4, 1) },
                new GridPosition(5, 3)
            );
            var state = new RunState(layout, floorNumber: 3);

            RunState restored = SaveGame.Capture(state).Restore();

            Assert.AreEqual(3, restored.FloorNumber);
            Assert.AreEqual(state.FloorNumber, SaveGame.Capture(restored).floorNumber);
        }

        [Test]
        public void RunState_ZeroFloorNumber_Throws()
        {
            GameGrid grid = CreateGrid();
            var layout = new DungeonLayout(
                grid,
                777,
                new GridPosition(1, 1),
                new[] { new GridPosition(4, 1) },
                new GridPosition(5, 3)
            );

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new RunState(layout, floorNumber: 0)
            );
        }

        [Test]
        public void Restore_PreservesEnemyAlertMemoryAndSearchState()
        {
            GameGrid grid = CreateGrid();
            var state = CreateRunState(
                grid,
                new GridPosition(1, 1),
                new GridPosition(4, 1),
                new GridPosition(5, 3)
            );
            state.Enemies[0].ObservePlayer(new GridPosition(2, 2), 2);

            RunState restored = SaveGame.Capture(state).Restore();

            EntityState restoredEnemy = restored.Enemies[0];
            Assert.IsTrue(restoredEnemy.IsAlerted);
            Assert.IsTrue(restoredEnemy.LastKnownPlayerPosition.HasValue);
            Assert.AreEqual(new GridPosition(2, 2), restoredEnemy.LastKnownPlayerPosition.Value);
            Assert.AreEqual(2, restoredEnemy.SearchTurnsRemaining);
        }

        [Test]
        public void Restore_PreservesEnemyHomeReturnAndPatrolState()
        {
            GameGrid grid = CreateGrid();
            var state = CreateRunState(
                grid,
                new GridPosition(1, 1),
                new GridPosition(4, 1),
                new GridPosition(5, 3)
            );
            EntityState enemy = state.Enemies[0];
            enemy.Position = new GridPosition(3, 2);
            enemy.BeginReturnHome();
            enemy.AdvancePatrolStep(3);

            RunState restored = SaveGame.Capture(state).Restore();

            EntityState restoredEnemy = restored.Enemies[0];
            Assert.AreEqual(new GridPosition(4, 1), restoredEnemy.HomePosition);
            Assert.AreEqual(new GridPosition(3, 2), restoredEnemy.Position);
            Assert.IsTrue(restoredEnemy.IsReturningHome);
            Assert.AreEqual(enemy.PatrolStepIndex, restoredEnemy.PatrolStepIndex);
        }

        [Test]
        public void RestoredRun_CanContinueTakingTurns()
        {
            GameGrid grid = CreateGrid();
            var state = CreateRunState(
                grid,
                new GridPosition(1, 1),
                new GridPosition(4, 1),
                new GridPosition(5, 3)
            );
            TurnSystem.WaitPlayerTurn(state);

            RunState restored = SaveGame.Capture(state).Restore();
            int turnBeforeAction = restored.TurnNumber;

            bool acted = TurnSystem.TryMovePlayer(restored, 0, 1);

            Assert.IsTrue(acted);
            Assert.AreEqual(turnBeforeAction + 1, restored.TurnNumber);
            Assert.AreEqual(new GridPosition(1, 2), restored.Player.Position);
        }

        private static RunState CreateRunState(
            GameGrid grid,
            GridPosition playerSpawn,
            GridPosition enemySpawn,
            GridPosition stairsDown
        )
        {
            return new RunState(
                new DungeonLayout(grid, 777, playerSpawn, new[] { enemySpawn }, stairsDown)
            );
        }

        private static GameGrid CreateGrid()
        {
            var grid = new GameGrid(6, 5, GridTileKind.Floor);
            grid.SetTile(new GridPosition(0, 0), GridTileKind.Wall);
            grid.SetTile(new GridPosition(5, 3), GridTileKind.StairsDown);
            grid.SetTile(new GridPosition(2, 1), GridTileKind.StairsDown);
            return grid;
        }

        private static void AssertGridEqual(GameGrid expected, GameGrid actual)
        {
            Assert.AreEqual(expected.Width, actual.Width);
            Assert.AreEqual(expected.Height, actual.Height);
            foreach (GridPosition position in expected.Positions())
            {
                Assert.AreEqual(
                    expected.GetTile(position),
                    actual.GetTile(position),
                    "Tile mismatch at " + position
                );
            }
        }

        private static void AssertEntityEqual(EntityState expected, EntityState actual)
        {
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.Position, actual.Position);
            Assert.AreEqual(expected.HomePosition, actual.HomePosition);
            Assert.AreEqual(expected.MaxHitPoints, actual.MaxHitPoints);
            Assert.AreEqual(expected.HitPoints, actual.HitPoints);
            Assert.AreEqual(expected.AttackDamage, actual.AttackDamage);
            Assert.AreEqual(expected.IsAlive, actual.IsAlive);
            Assert.AreEqual(expected.IsAlerted, actual.IsAlerted);
            Assert.AreEqual(
                expected.LastKnownPlayerPosition.HasValue,
                actual.LastKnownPlayerPosition.HasValue
            );
            if (expected.LastKnownPlayerPosition.HasValue)
            {
                Assert.AreEqual(
                    expected.LastKnownPlayerPosition.Value,
                    actual.LastKnownPlayerPosition.Value
                );
            }
            Assert.AreEqual(expected.SearchTurnsRemaining, actual.SearchTurnsRemaining);
            Assert.AreEqual(expected.IsReturningHome, actual.IsReturningHome);
            Assert.AreEqual(expected.PatrolStepIndex, actual.PatrolStepIndex);
        }
    }
}
