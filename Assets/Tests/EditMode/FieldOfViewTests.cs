using NUnit.Framework;
using TacticalRoguelike.Core;

namespace TacticalRoguelike.Tests.EditMode
{
    public sealed class FieldOfViewTests
    {
        [Test]
        public void HasLineOfSight_ClearFloorWithinRange_ReturnsTrue()
        {
            var grid = new GameGrid(6, 6, GridTileKind.Floor);

            bool canSee = FieldOfView.HasLineOfSight(
                grid,
                new GridPosition(1, 1),
                new GridPosition(4, 4),
                8
            );

            Assert.IsTrue(canSee);
        }

        [Test]
        public void HasLineOfSight_WallBetweenPositions_ReturnsFalse()
        {
            var grid = new GameGrid(6, 6, GridTileKind.Floor);
            grid.SetTile(new GridPosition(2, 1), GridTileKind.Wall);

            bool canSee = FieldOfView.HasLineOfSight(
                grid,
                new GridPosition(1, 1),
                new GridPosition(4, 1),
                8
            );

            Assert.IsFalse(canSee);
        }

        [Test]
        public void HasLineOfSight_TargetBeyondRange_ReturnsFalse()
        {
            var grid = new GameGrid(12, 12, GridTileKind.Floor);

            bool canSee = FieldOfView.HasLineOfSight(
                grid,
                new GridPosition(1, 1),
                new GridPosition(10, 1),
                8
            );

            Assert.IsFalse(canSee);
        }

        [Test]
        public void HasLineOfSight_StairsAreTransparent_ReturnsTrue()
        {
            var grid = new GameGrid(6, 6, GridTileKind.Floor);
            grid.SetTile(new GridPosition(2, 1), GridTileKind.StairsDown);

            bool canSee = FieldOfView.HasLineOfSight(
                grid,
                new GridPosition(1, 1),
                new GridPosition(4, 1),
                8
            );

            Assert.IsTrue(canSee);
        }
    }
}
