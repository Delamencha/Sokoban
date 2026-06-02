using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sokoban
{
    public enum LevelTile
    {
        Empty,
        Floor,
        Wall
    }

    public static class LevelValidationStatus
    {
        public const string Unverified = "未验证";
        public const string NoValidSolution = "无有效解";
        public const string SimpleValidated = "通过简单验证";
        public const string Solved = "有解";

        public static string Normalize(string status)
        {
            switch (status)
            {
                case NoValidSolution:
                case SimpleValidated:
                case Solved:
                    return status;
                default:
                    return Unverified;
            }
        }
    }

    public class LevelValidationResult
    {
        public LevelValidationResult(List<string> errors)
        {
            this.errors = errors ?? new List<string>();
        }

        public readonly List<string> errors;
        public bool Passed => errors.Count == 0;
        public string Status => Passed ? LevelValidationStatus.SimpleValidated : LevelValidationStatus.NoValidSolution;

        public string CreateMessage(string passedMessage)
        {
            if (Passed)
            {
                return passedMessage;
            }

            return string.Join("\n", errors);
        }
    }

    [Serializable]
    public struct GridPosition
    {
        public int x;
        public int y;

        public GridPosition(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public GridPosition(Vector2Int position)
        {
            x = position.x;
            y = position.y;
        }

        public Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, y);
        }
    }

    [Serializable]
    public class LevelData
    {
        public string id = "level";
        public string displayName = "Level";
        public string createdAt = string.Empty;
        public int width = 8;
        public int height = 8;
        public string validationStatus = LevelValidationStatus.Unverified;
        public string solutionActions = string.Empty;
        public string[] tiles = new string[0];
        public GridPosition player = new GridPosition(1, 1);
        public List<GridPosition> boxes = new List<GridPosition>();
        public List<GridPosition> targets = new List<GridPosition>();

        public LevelData Clone()
        {
            return JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(this));
        }

        public LevelTile GetTile(Vector2Int position)
        {
            if (!IsInside(position) || tiles == null || tiles.Length != height)
            {
                return LevelTile.Empty;
            }

            int rowIndex = height - 1 - position.y;
            if (rowIndex < 0 || rowIndex >= tiles.Length || tiles[rowIndex] == null || position.x >= tiles[rowIndex].Length)
            {
                return LevelTile.Empty;
            }

            char tile = tiles[rowIndex][position.x];
            if (tile == '#')
            {
                return LevelTile.Wall;
            }

            if (tile == '.')
            {
                return LevelTile.Floor;
            }

            return LevelTile.Empty;
        }

        public void SetTile(Vector2Int position, LevelTile tile)
        {
            EnsureTiles();

            if (!IsInside(position))
            {
                return;
            }

            int rowIndex = height - 1 - position.y;
            char[] row = tiles[rowIndex].ToCharArray();
            row[position.x] = TileToChar(tile);
            tiles[rowIndex] = new string(row);
        }

        public bool IsInside(Vector2Int position)
        {
            return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
        }

        public void EnsureTiles()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            if (string.IsNullOrWhiteSpace(createdAt))
            {
                createdAt = CreateCurrentTimestamp();
            }

            boxes = boxes ?? new List<GridPosition>();
            targets = targets ?? new List<GridPosition>();
            validationStatus = LevelValidationStatus.Normalize(validationStatus);
            solutionActions = solutionActions ?? string.Empty;
            if (validationStatus != LevelValidationStatus.Solved)
            {
                solutionActions = string.Empty;
            }

            if (tiles == null || tiles.Length != height)
            {
                string[] newTiles = new string[height];
                for (int y = 0; y < height; y++)
                {
                    newTiles[y] = new string('.', width);
                }

                tiles = newTiles;
                return;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                string row = tiles[i] ?? string.Empty;
                if (row.Length < width)
                {
                    row = row.PadRight(width, '.');
                }
                else if (row.Length > width)
                {
                    row = row.Substring(0, width);
                }

                tiles[i] = row;
            }
        }

        public static LevelData CreateBlank(int width, int height)
        {
            LevelData data = new LevelData
            {
                id = "custom_level",
                displayName = "Custom Level",
                width = Mathf.Max(3, width),
                height = Mathf.Max(3, height),
                boxes = new List<GridPosition>(),
                targets = new List<GridPosition>()
            };

            data.EnsureTiles();
            data.player = new GridPosition(1, 1);
            return data;
        }

        public static string CreateCurrentTimestamp()
        {
            return DateTime.Now.ToString("O");
        }

        private static char TileToChar(LevelTile tile)
        {
            switch (tile)
            {
                case LevelTile.Wall:
                    return '#';
                case LevelTile.Empty:
                    return ' ';
                default:
                    return '.';
            }
        }
    }

    public static class LevelValidator
    {
        public static LevelValidationResult ValidateSimple(LevelData level)
        {
            return new LevelValidationResult(Validate(level));
        }

        public static LevelValidationResult ValidateSimpleAndApplyStatus(LevelData level)
        {
            LevelValidationResult result = ValidateSimple(level);
            if (level != null)
            {
                if (result.Passed && GetValidationStatusText(level) == LevelValidationStatus.Solved)
                {
                    return result;
                }

                level.validationStatus = result.Status;
                if (level.validationStatus != LevelValidationStatus.Solved)
                {
                    level.solutionActions = string.Empty;
                }
            }

            return result;
        }

        public static string GetValidationStatusText(LevelData level)
        {
            return level != null ? LevelValidationStatus.Normalize(level.validationStatus) : LevelValidationStatus.Unverified;
        }

        public static List<string> Validate(LevelData level)
        {
            List<string> errors = ValidateBasic(level);
            if (level != null)
            {
                ValidateTargetReachability(level, errors);
            }

            return errors;
        }

        public static List<string> ValidateBasic(LevelData level)
        {
            List<string> errors = new List<string>();

            if (level == null)
            {
                errors.Add("关卡数据为空。");
                return errors;
            }

            level.EnsureTiles();
            if (level.width < 3 || level.height < 3)
            {
                errors.Add("地图尺寸至少需要 3 x 3。");
            }

            ValidateWalkablePosition(level, level.player.ToVector2Int(), "玩家", errors);

            if (level.boxes == null || level.boxes.Count == 0)
            {
                errors.Add("至少需要放置 1 个箱子。");
            }

            if (level.targets == null || level.targets.Count == 0)
            {
                errors.Add("至少需要放置 1 个目标点。");
            }

            int boxCount = level.boxes != null ? level.boxes.Count : 0;
            int targetCount = level.targets != null ? level.targets.Count : 0;
            if (boxCount > 0 && targetCount > 0 && boxCount != targetCount)
            {
                errors.Add("箱子数量必须等于目标点数量。");
            }

            ValidatePositionList(level, level.boxes, "箱子", errors);
            ValidatePositionList(level, level.targets, "目标点", errors);
            ValidateBoxPlayerOverlap(level, errors);

            return errors;
        }

        private static void ValidatePositionList(LevelData level, List<GridPosition> positions, string label, List<string> errors)
        {
            if (positions == null)
            {
                return;
            }

            HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2Int position = positions[i].ToVector2Int();
                ValidateWalkablePosition(level, position, label, errors);

                if (!seen.Add(position))
                {
                    errors.Add(label + "位置重复：" + position);
                }
            }
        }

        private static void ValidateWalkablePosition(LevelData level, Vector2Int position, string label, List<string> errors)
        {
            if (!level.IsInside(position))
            {
                errors.Add(label + "位置超出地图：" + position);
                return;
            }

            if (level.GetTile(position) != LevelTile.Floor)
            {
                errors.Add(label + "必须放在地板上：" + position);
            }
        }

        private static void ValidateBoxPlayerOverlap(LevelData level, List<string> errors)
        {
            if (level.boxes == null)
            {
                return;
            }

            Vector2Int playerPosition = level.player.ToVector2Int();
            for (int i = 0; i < level.boxes.Count; i++)
            {
                if (level.boxes[i].ToVector2Int() == playerPosition)
                {
                    errors.Add("玩家不能与箱子重叠：" + playerPosition);
                }
            }
        }

        private static void ValidateTargetReachability(LevelData level, List<string> errors)
        {
            if (level.boxes == null || level.targets == null || level.boxes.Count == 0 || level.targets.Count == 0)
            {
                return;
            }

            for (int i = 0; i < level.targets.Count; i++)
            {
                Vector2Int target = level.targets[i].ToVector2Int();
                bool reachable = false;
                for (int j = 0; j < level.boxes.Count; j++)
                {
                    if (CanBoxReachTarget(level, level.boxes[j].ToVector2Int(), target))
                    {
                        reachable = true;
                        break;
                    }
                }

                if (!reachable)
                {
                    errors.Add("没有箱子可简单到达目标点：" + target);
                }
            }
        }

        private static bool CanBoxReachTarget(LevelData level, Vector2Int start, Vector2Int target)
        {
            if (!IsWalkable(level, start) || !IsWalkable(level, target))
            {
                return false;
            }

            Queue<Vector2Int> open = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            open.Enqueue(start);
            visited.Add(start);

            while (open.Count > 0)
            {
                Vector2Int current = open.Dequeue();
                if (current == target)
                {
                    return true;
                }

                TryVisitBoxPosition(level, current, Vector2Int.up, open, visited);
                TryVisitBoxPosition(level, current, Vector2Int.down, open, visited);
                TryVisitBoxPosition(level, current, Vector2Int.left, open, visited);
                TryVisitBoxPosition(level, current, Vector2Int.right, open, visited);
            }

            return false;
        }

        private static void TryVisitBoxPosition(
            LevelData level,
            Vector2Int current,
            Vector2Int direction,
            Queue<Vector2Int> open,
            HashSet<Vector2Int> visited)
        {
            Vector2Int next = current + direction;
            Vector2Int pusherPosition = current - direction;
            if (!IsWalkable(level, next) || !IsWalkable(level, pusherPosition) || visited.Contains(next))
            {
                return;
            }

            visited.Add(next);
            open.Enqueue(next);
        }

        private static bool IsWalkable(LevelData level, Vector2Int position)
        {
            return level.IsInside(position) && level.GetTile(position) == LevelTile.Floor;
        }
    }
}
