using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sokoban
{
    public enum MoveResult
    {
        Blocked,
        Moved,
        Pushed,
        Solved
    }

    public class BoardModel
    {
        private struct BoardSnapshot
        {
            public Vector2Int player;
            public List<Vector2Int> boxes;
        }

        private readonly Stack<BoardSnapshot> undoStack = new Stack<BoardSnapshot>();
        private LevelTile[,] tiles;
        private HashSet<Vector2Int> boxes = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> targets = new HashSet<Vector2Int>();
        private LevelData startLevel;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public Vector2Int PlayerPosition { get; private set; }
        public IEnumerable<Vector2Int> Boxes => boxes;
        public IEnumerable<Vector2Int> Targets => targets;
        public bool CanUndo => undoStack.Count > 0;

        public void Load(LevelData level)
        {
            startLevel = level.Clone();
            startLevel.EnsureTiles();
            Width = startLevel.width;
            Height = startLevel.height;
            tiles = new LevelTile[Width, Height];

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    tiles[x, y] = startLevel.GetTile(new Vector2Int(x, y));
                }
            }

            PlayerPosition = startLevel.player.ToVector2Int();
            boxes = new HashSet<Vector2Int>((startLevel.boxes ?? new List<GridPosition>()).Select(position => position.ToVector2Int()));
            targets = new HashSet<Vector2Int>((startLevel.targets ?? new List<GridPosition>()).Select(position => position.ToVector2Int()));
            undoStack.Clear();
        }

        public void Restart()
        {
            if (startLevel != null)
            {
                Load(startLevel);
            }
        }

        public bool Undo()
        {
            if (undoStack.Count == 0)
            {
                return false;
            }

            BoardSnapshot snapshot = undoStack.Pop();
            PlayerPosition = snapshot.player;
            boxes = new HashSet<Vector2Int>(snapshot.boxes);
            return true;
        }

        public MoveResult TryMove(Vector2Int direction)
        {
            if (direction == Vector2Int.zero)
            {
                return MoveResult.Blocked;
            }

            Vector2Int destination = PlayerPosition + direction;
            if (!IsWalkable(destination))
            {
                return MoveResult.Blocked;
            }

            bool pushed = false;
            if (boxes.Contains(destination))
            {
                Vector2Int boxDestination = destination + direction;
                if (!IsWalkable(boxDestination) || boxes.Contains(boxDestination))
                {
                    return MoveResult.Blocked;
                }

                SaveSnapshot();
                boxes.Remove(destination);
                boxes.Add(boxDestination);
                pushed = true;
            }
            else
            {
                SaveSnapshot();
            }

            PlayerPosition = destination;
            if (IsSolved)
            {
                return MoveResult.Solved;
            }

            return pushed ? MoveResult.Pushed : MoveResult.Moved;
        }

        public bool IsSolved
        {
            get
            {
                return boxes.Count > 0 && boxes.All(box => targets.Contains(box));
            }
        }

        public bool IsInside(Vector2Int position)
        {
            return position.x >= 0 && position.x < Width && position.y >= 0 && position.y < Height;
        }

        public LevelTile GetTile(Vector2Int position)
        {
            if (!IsInside(position))
            {
                return LevelTile.Empty;
            }

            return tiles[position.x, position.y];
        }

        public bool HasBox(Vector2Int position)
        {
            return boxes.Contains(position);
        }

        public bool HasTarget(Vector2Int position)
        {
            return targets.Contains(position);
        }

        private bool IsWalkable(Vector2Int position)
        {
            return IsInside(position) && GetTile(position) != LevelTile.Wall && GetTile(position) != LevelTile.Empty;
        }

        private void SaveSnapshot()
        {
            undoStack.Push(new BoardSnapshot
            {
                player = PlayerPosition,
                boxes = boxes.ToList()
            });
        }
    }
}
