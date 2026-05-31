using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sokoban
{
    public enum EditorTool
    {
        Floor,
        Wall,
        Empty,
        Player,
        Box,
        Target,
        EraseEntity
    }

    public class RuntimeLevelEditor
    {
        private const int MaxUndoHistory = 20;

        private readonly BoardRenderer renderer;
        private readonly Action<string> setStatus;
        private readonly List<LevelData> undoHistory = new List<LevelData>();
        private LevelData editOperationSnapshot;
        private string editOperationSnapshotJson;
        private bool editOperationActive;

        public RuntimeLevelEditor(BoardRenderer renderer, Action<string> setStatus)
        {
            this.renderer = renderer;
            this.setStatus = setStatus;
            CurrentLevel = LevelData.CreateBlank(8, 8);
        }

        public LevelData CurrentLevel { get; private set; }
        public EditorTool ActiveTool { get; private set; } = EditorTool.Floor;

        public void SetLevel(LevelData level)
        {
            CurrentLevel = level.Clone();
            CurrentLevel.EnsureTiles();
            ClearUndoHistory();
            Render();
        }

        public void CreateBlank(int width, int height)
        {
            PushUndoHistory(CurrentLevel.Clone());
            CurrentLevel = LevelData.CreateBlank(width, height);
            ActiveTool = EditorTool.Floor;
            Render();
            setStatus("已创建空白关卡。");
        }

        public void SetTool(EditorTool tool)
        {
            ActiveTool = tool;
            setStatus("当前工具：" + GetToolName(tool));
        }

        public bool TryPaintFromScreenPosition(Vector2 screenPosition)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -camera.transform.position.z));
            Vector2Int grid = renderer.WorldToGrid(world);
            return TryPaint(grid);
        }

        public bool TryPaint(Vector2Int position)
        {
            if (!CurrentLevel.IsInside(position))
            {
                return false;
            }

            LevelData beforeChange = CurrentLevel.Clone();
            string beforeJson = JsonUtility.ToJson(beforeChange);
            bool applied = ApplyPaint(position);
            if (!applied)
            {
                return false;
            }

            if (beforeJson == JsonUtility.ToJson(CurrentLevel))
            {
                return false;
            }

            if (!editOperationActive)
            {
                PushUndoHistory(beforeChange);
            }

            Render();
            return true;
        }

        public void BeginEditOperation()
        {
            if (editOperationActive)
            {
                return;
            }

            editOperationSnapshot = CurrentLevel.Clone();
            editOperationSnapshotJson = JsonUtility.ToJson(editOperationSnapshot);
            editOperationActive = true;
        }

        public void EndEditOperation()
        {
            if (!editOperationActive)
            {
                return;
            }

            if (editOperationSnapshotJson != JsonUtility.ToJson(CurrentLevel))
            {
                PushUndoHistory(editOperationSnapshot);
            }

            editOperationSnapshot = null;
            editOperationSnapshotJson = string.Empty;
            editOperationActive = false;
        }

        public bool Undo()
        {
            if (undoHistory.Count == 0)
            {
                setStatus("没有可撤销的编辑操作。");
                return false;
            }

            int lastIndex = undoHistory.Count - 1;
            CurrentLevel = undoHistory[lastIndex].Clone();
            CurrentLevel.EnsureTiles();
            undoHistory.RemoveAt(lastIndex);
            Render();
            setStatus("已撤销一步编辑操作。");
            return true;
        }

        public void ClearUndoHistory()
        {
            undoHistory.Clear();
            editOperationSnapshot = null;
            editOperationSnapshotJson = string.Empty;
            editOperationActive = false;
        }

        private bool ApplyPaint(Vector2Int position)
        {
            switch (ActiveTool)
            {
                case EditorTool.Wall:
                    CurrentLevel.SetTile(position, LevelTile.Wall);
                    RemoveEntitiesAt(position);
                    break;
                case EditorTool.Empty:
                    CurrentLevel.SetTile(position, LevelTile.Empty);
                    RemoveEntitiesAt(position);
                    break;
                case EditorTool.Player:
                    EnsureFloor(position);
                    CurrentLevel.player = new GridPosition(position);
                    RemoveBoxAt(position);
                    break;
                case EditorTool.Box:
                    EnsureFloor(position);
                    if (CurrentLevel.player.ToVector2Int() == position)
                    {
                        setStatus("箱子不能与玩家重叠。");
                        return false;
                    }
                    AddUnique(CurrentLevel.boxes, position);
                    break;
                case EditorTool.Target:
                    EnsureFloor(position);
                    AddUnique(CurrentLevel.targets, position);
                    break;
                case EditorTool.EraseEntity:
                    RemoveEntitiesAt(position);
                    break;
                default:
                    CurrentLevel.SetTile(position, LevelTile.Floor);
                    break;
            }

            return true;
        }

        public List<string> Validate()
        {
            return LevelValidator.Validate(CurrentLevel);
        }

        public string Save()
        {
            return Save(string.Empty);
        }

        public string Save(string existingPath)
        {
            string path = LevelSaveSystem.SaveLevel(CurrentLevel, existingPath);
            setStatus("关卡已保存：" + path);
            return path;
        }

        public void Render()
        {
            renderer.RenderLevel(CurrentLevel);
        }

        private void PushUndoHistory(LevelData snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            undoHistory.Add(snapshot);
            if (undoHistory.Count > MaxUndoHistory)
            {
                undoHistory.RemoveAt(0);
            }
        }

        private void EnsureFloor(Vector2Int position)
        {
            if (CurrentLevel.GetTile(position) == LevelTile.Empty || CurrentLevel.GetTile(position) == LevelTile.Wall)
            {
                CurrentLevel.SetTile(position, LevelTile.Floor);
            }
        }

        private void RemoveEntitiesAt(Vector2Int position)
        {
            RemoveBoxAt(position);
            RemovePosition(CurrentLevel.targets, position);

            if (CurrentLevel.player.ToVector2Int() == position && CurrentLevel.GetTile(position) != LevelTile.Floor)
            {
                CurrentLevel.player = new GridPosition(0, 0);
            }
        }

        private void RemoveBoxAt(Vector2Int position)
        {
            RemovePosition(CurrentLevel.boxes, position);
        }

        private static void AddUnique(List<GridPosition> positions, Vector2Int position)
        {
            if (positions == null)
            {
                return;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].ToVector2Int() == position)
                {
                    return;
                }
            }

            positions.Add(new GridPosition(position));
        }

        private static void RemovePosition(List<GridPosition> positions, Vector2Int position)
        {
            if (positions == null)
            {
                return;
            }

            for (int i = positions.Count - 1; i >= 0; i--)
            {
                if (positions[i].ToVector2Int() == position)
                {
                    positions.RemoveAt(i);
                }
            }
        }

        private static string GetToolName(EditorTool tool)
        {
            switch (tool)
            {
                case EditorTool.Wall:
                    return "墙";
                case EditorTool.Empty:
                    return "空白";
                case EditorTool.Player:
                    return "玩家";
                case EditorTool.Box:
                    return "箱子";
                case EditorTool.Target:
                    return "目标点";
                case EditorTool.EraseEntity:
                    return "擦除实体";
                default:
                    return "地板";
            }
        }
    }
}
