using System;
using System.Collections.Generic;

namespace TacticalRoguelike.Core
{
    public sealed class GameGrid
    {
        private readonly GridTileKind[] tiles;

        public GameGrid(int width, int height, GridTileKind initialTile = GridTileKind.Wall)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");
            }

            Width = width;
            Height = height;
            tiles = new GridTileKind[width * height];

            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = initialTile;
            }
        }

        public int Width { get; }
        public int Height { get; }

        public bool IsInBounds(GridPosition position)
        {
            return position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
        }

        public GridTileKind GetTile(GridPosition position)
        {
            return tiles[GetIndex(position)];
        }

        public void SetTile(GridPosition position, GridTileKind tileKind)
        {
            tiles[GetIndex(position)] = tileKind;
        }

        public bool IsWalkable(GridPosition position)
        {
            if (!IsInBounds(position))
            {
                return false;
            }

            GridTileKind tileKind = GetTile(position);
            return tileKind == GridTileKind.Floor || tileKind == GridTileKind.StairsDown;
        }

        public IEnumerable<GridPosition> Positions()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    yield return new GridPosition(x, y);
                }
            }
        }

        private int GetIndex(GridPosition position)
        {
            if (!IsInBounds(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "Position is outside the grid bounds.");
            }

            return position.Y * Width + position.X;
        }
    }
}
