using System;
using System.Collections.Generic;

namespace TacticalRoguelike.Core
{
    public readonly struct DungeonGeneratorConfig
    {
        public DungeonGeneratorConfig(int width, int height, int roomCount, int minRoomSize, int maxRoomSize, int roomPlacementAttempts = 200)
        {
            if (width < 10)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be at least 10.");
            }

            if (height < 10)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be at least 10.");
            }

            if (roomCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(roomCount), roomCount, "At least two rooms are required.");
            }

            if (minRoomSize < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(minRoomSize), minRoomSize, "Minimum room size must be at least 3.");
            }

            if (maxRoomSize < minRoomSize)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRoomSize), maxRoomSize, "Maximum room size must be greater than or equal to minimum room size.");
            }

            if (maxRoomSize > width - 2 || maxRoomSize > height - 2)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRoomSize), maxRoomSize, "Maximum room size must fit inside the dungeon border.");
            }

            if (roomPlacementAttempts < roomCount)
            {
                throw new ArgumentOutOfRangeException(nameof(roomPlacementAttempts), roomPlacementAttempts, "Room placement attempts must be at least the room count.");
            }

            Width = width;
            Height = height;
            RoomCount = roomCount;
            MinRoomSize = minRoomSize;
            MaxRoomSize = maxRoomSize;
            RoomPlacementAttempts = roomPlacementAttempts;
        }

        public int Width { get; }
        public int Height { get; }
        public int RoomCount { get; }
        public int MinRoomSize { get; }
        public int MaxRoomSize { get; }
        public int RoomPlacementAttempts { get; }

        public static DungeonGeneratorConfig Default => new DungeonGeneratorConfig(40, 24, 7, 4, 8);
    }

    public sealed class DungeonGenerator
    {
        public DungeonLayout Generate(int seed)
        {
            return Generate(seed, DungeonGeneratorConfig.Default);
        }

        public DungeonLayout Generate(int seed, DungeonGeneratorConfig config)
        {
            var random = new Random(seed);
            var grid = new GameGrid(config.Width, config.Height, GridTileKind.Wall);
            List<Room> rooms = PlaceRooms(random, config);

            foreach (Room room in rooms)
            {
                CarveRoom(grid, room);
            }

            for (int i = 1; i < rooms.Count; i++)
            {
                CarveCorridor(grid, rooms[i - 1].Center, rooms[i].Center, random.Next(0, 2) == 0);
            }

            Room finalRoom = rooms[rooms.Count - 1];
            GridPosition playerSpawn = rooms[0].Center;
            GridPosition stairsDown = finalRoom.Center;
            GridPosition enemySpawn = finalRoom.RandomInteriorPositionExcept(random, stairsDown);

            grid.SetTile(playerSpawn, GridTileKind.Floor);
            grid.SetTile(enemySpawn, GridTileKind.Floor);
            grid.SetTile(stairsDown, GridTileKind.StairsDown);

            return new DungeonLayout(grid, seed, playerSpawn, new[] { enemySpawn }, stairsDown);
        }

        private static List<Room> PlaceRooms(Random random, DungeonGeneratorConfig config)
        {
            var rooms = new List<Room>(config.RoomCount);

            for (int attempt = 0; attempt < config.RoomPlacementAttempts && rooms.Count < config.RoomCount; attempt++)
            {
                int width = random.Next(config.MinRoomSize, config.MaxRoomSize + 1);
                int height = random.Next(config.MinRoomSize, config.MaxRoomSize + 1);
                int x = random.Next(1, config.Width - width);
                int y = random.Next(1, config.Height - height);
                var candidate = new Room(x, y, width, height);

                bool overlapsExisting = false;
                foreach (Room room in rooms)
                {
                    if (candidate.OverlapsWithMargin(room, 1))
                    {
                        overlapsExisting = true;
                        break;
                    }
                }

                if (!overlapsExisting)
                {
                    rooms.Add(candidate);
                }
            }

            EnsureMinimumRooms(rooms, config);

            return rooms;
        }

        private static void EnsureMinimumRooms(List<Room> rooms, DungeonGeneratorConfig config)
        {
            if (rooms.Count >= 2)
            {
                return;
            }

            Room firstFallback = new Room(1, 1, config.MinRoomSize, config.MinRoomSize);
            Room secondFallback = new Room(
                config.Width - config.MinRoomSize - 1,
                config.Height - config.MinRoomSize - 1,
                config.MinRoomSize,
                config.MinRoomSize);

            if (rooms.Count == 0)
            {
                rooms.Add(firstFallback);
                rooms.Add(secondFallback);
                return;
            }

            Room existingRoom = rooms[0];
            rooms.Add(existingRoom.Center == secondFallback.Center ? firstFallback : secondFallback);
        }

        private static void CarveRoom(GameGrid grid, Room room)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    grid.SetTile(new GridPosition(x, y), GridTileKind.Floor);
                }
            }
        }

        private static void CarveCorridor(GameGrid grid, GridPosition start, GridPosition end, bool horizontalFirst)
        {
            if (horizontalFirst)
            {
                CarveHorizontal(grid, start.X, end.X, start.Y);
                CarveVertical(grid, start.Y, end.Y, end.X);
            }
            else
            {
                CarveVertical(grid, start.Y, end.Y, start.X);
                CarveHorizontal(grid, start.X, end.X, end.Y);
            }
        }

        private static void CarveHorizontal(GameGrid grid, int startX, int endX, int y)
        {
            int minX = Math.Min(startX, endX);
            int maxX = Math.Max(startX, endX);
            for (int x = minX; x <= maxX; x++)
            {
                grid.SetTile(new GridPosition(x, y), GridTileKind.Floor);
            }
        }

        private static void CarveVertical(GameGrid grid, int startY, int endY, int x)
        {
            int minY = Math.Min(startY, endY);
            int maxY = Math.Max(startY, endY);
            for (int y = minY; y <= maxY; y++)
            {
                grid.SetTile(new GridPosition(x, y), GridTileKind.Floor);
            }
        }

        private readonly struct Room
        {
            public Room(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int Height { get; }

            public GridPosition Center => new GridPosition(X + Width / 2, Y + Height / 2);

            public bool OverlapsWithMargin(Room other, int margin)
            {
                return X - margin < other.X + other.Width
                    && X + Width + margin > other.X
                    && Y - margin < other.Y + other.Height
                    && Y + Height + margin > other.Y;
            }

            public GridPosition RandomInteriorPosition(Random random)
            {
                int x = random.Next(X, X + Width);
                int y = random.Next(Y, Y + Height);
                return new GridPosition(x, y);
            }

            public GridPosition RandomInteriorPositionExcept(Random random, GridPosition excludedPosition)
            {
                for (int attempt = 0; attempt < Width * Height; attempt++)
                {
                    GridPosition candidate = RandomInteriorPosition(random);
                    if (candidate != excludedPosition)
                    {
                        return candidate;
                    }
                }

                for (int y = Y; y < Y + Height; y++)
                {
                    for (int x = X; x < X + Width; x++)
                    {
                        var candidate = new GridPosition(x, y);
                        if (candidate != excludedPosition)
                        {
                            return candidate;
                        }
                    }
                }

                throw new InvalidOperationException("Room has no valid interior position outside the excluded tile.");
            }
        }
    }
}
