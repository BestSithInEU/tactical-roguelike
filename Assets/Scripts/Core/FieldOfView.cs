using System;

namespace TacticalRoguelike.Core
{
    public static class FieldOfView
    {
        public static bool HasLineOfSight(
            GameGrid grid,
            GridPosition start,
            GridPosition target,
            int maxRange
        )
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (maxRange < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxRange),
                    maxRange,
                    "Sight range cannot be negative."
                );
            }

            if (!grid.IsInBounds(start) || !grid.IsInBounds(target))
            {
                return false;
            }
            if (ManhattanDistance(start, target) > maxRange)
            {
                return false;
            }

            int x0 = start.X;
            int y0 = start.Y;
            int x1 = target.X;
            int y1 = target.Y;
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int error = dx - dy;

            while (true)
            {
                var current = new GridPosition(x0, y0);
                if (current != start && current != target && BlocksSight(grid, current))
                {
                    return false;
                }

                if (current == target)
                {
                    return true;
                }

                int doubleError = error * 2;
                if (doubleError > -dy)
                {
                    error -= dy;
                    x0 += sx;
                }
                if (doubleError < dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static bool BlocksSight(GameGrid grid, GridPosition position)
        {
            return grid.GetTile(position) == GridTileKind.Wall;
        }

        private static int ManhattanDistance(GridPosition first, GridPosition second)
        {
            return Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
        }
    }
}
