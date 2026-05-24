using System;
using System.Linq;
using NUnit.Framework;
using TacticalRoguelike.Core;

namespace TacticalRoguelike.Tests.EditMode
{
    public sealed class GameGridTests
    {
        [Test]
        public void Constructor_InitializesEveryTileWithExpectedKind()
        {
            var grid = new GameGrid(4, 3, GridTileKind.Floor);

            Assert.AreEqual(4, grid.Width);
            Assert.AreEqual(3, grid.Height);
            Assert.AreEqual(12, grid.Positions().Count());
            Assert.IsTrue(grid.Positions().All(position => grid.GetTile(position) == GridTileKind.Floor));
        }

        [Test]
        public void GetSetTile_EnforcesBounds()
        {
            var grid = new GameGrid(2, 2);
            var inBounds = new GridPosition(1, 1);

            grid.SetTile(inBounds, GridTileKind.StairsDown);

            Assert.AreEqual(GridTileKind.StairsDown, grid.GetTile(inBounds));
            Assert.IsFalse(grid.IsInBounds(new GridPosition(-1, 0)));
            Assert.IsFalse(grid.IsWalkable(new GridPosition(2, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetTile(new GridPosition(2, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetTile(new GridPosition(0, 2), GridTileKind.Floor));
        }
    }
}
