using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sokoban
{
    public enum EditorTool
    {
        None = -1,
        Floor,
        Wall,
        Empty,
        Player,
        Box,
        Target,
        EraseEntity
    }

    public enum EditorBrushMode
    {
        Normal,
        FilledRectangle,
        HollowRectangle
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
        private Vector2Int rectangleStartPosition;
        private Vector2Int rectangleEndPosition;
        private bool rectangleBrushOperationActive;

        public RuntimeLevelEditor(BoardRenderer renderer, Action<string> setStatus)
        {
            this.renderer = renderer;
            this.setStatus = setStatus;
            CurrentLevel = LevelData.CreateBlank(8, 8);
        }

        public LevelData CurrentLevel { get; private set; }
        public EditorTool ActiveTool { get; private set; } = EditorTool.None;
        public EditorBrushMode ActiveBrushMode { get; private set; } = EditorBrushMode.Normal;
        public bool ShouldUseRectangleBrush => ActiveBrushMode != EditorBrushMode.Normal && IsTileTool(ActiveTool);

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
            ActiveTool = EditorTool.None;
            ActiveBrushMode = EditorBrushMode.Normal;
            Render();
            setStatus("已创建空白关卡。");
        }

        public void SetTool(EditorTool tool)
        {
            ActiveTool = tool;
            setStatus("当前工具：" + GetToolName(tool));
        }

        public void SetBrushMode(EditorBrushMode brushMode)
        {
            ActiveBrushMode = brushMode;
            setStatus("当前画笔：" + GetBrushModeName(brushMode));
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

        public bool BeginBrushOperation(Vector2 screenPosition)
        {
            if (!ShouldUseRectangleBrush || !TryGetGridFromScreenPosition(screenPosition, out Vector2Int grid))
            {
                return false;
            }

            rectangleStartPosition = grid;
            rectangleEndPosition = grid;
            rectangleBrushOperationActive = true;
            BeginEditOperation();
            renderer.ShowRectanglePreview(rectangleStartPosition, rectangleEndPosition);
            return true;
        }

        public void UpdateBrushOperation(Vector2 screenPosition)
        {
            if (!rectangleBrushOperationActive)
            {
                return;
            }

            if (TryGetGridFromScreenPosition(screenPosition, out Vector2Int grid))
            {
                rectangleEndPosition = grid;
                renderer.ShowRectanglePreview(rectangleStartPosition, rectangleEndPosition);
            }
        }

        public bool EndBrushOperation(Vector2 screenPosition)
        {
            if (!rectangleBrushOperationActive)
            {
                return false;
            }

            UpdateBrushOperation(screenPosition);
            string beforeJson = JsonUtility.ToJson(CurrentLevel);
            ApplyRectangle(rectangleStartPosition, rectangleEndPosition, ActiveBrushMode == EditorBrushMode.HollowRectangle);
            bool changed = beforeJson != JsonUtility.ToJson(CurrentLevel);
            EndEditOperation();
            rectangleBrushOperationActive = false;
            renderer.ClearPreview();
            if (changed)
            {
                Render();
            }

            return changed;
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
            rectangleBrushOperationActive = false;
            renderer.ClearPreview();
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
            rectangleBrushOperationActive = false;
            renderer.ClearPreview();
        }

        private bool ApplyPaint(Vector2Int position)
        {
            switch (ActiveTool)
            {
                case EditorTool.Floor:
                    CurrentLevel.SetTile(position, LevelTile.Floor);
                    break;
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
                    return false;
            }

            return true;
        }

        private void ApplyRectangle(Vector2Int start, Vector2Int end, bool hollow)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (hollow && x != minX && x != maxX && y != minY && y != maxY)
                    {
                        continue;
                    }

                    ApplyPaint(new Vector2Int(x, y));
                }
            }
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

        private bool TryGetGridFromScreenPosition(Vector2 screenPosition, out Vector2Int grid)
        {
            grid = new Vector2Int();
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -camera.transform.position.z));
            grid = renderer.WorldToGrid(world);
            return CurrentLevel.IsInside(grid);
        }

        private static bool IsTileTool(EditorTool tool)
        {
            return tool == EditorTool.Floor || tool == EditorTool.Wall || tool == EditorTool.Empty;
        }

        private static string GetToolName(EditorTool tool)
        {
            switch (tool)
            {
                case EditorTool.None:
                    return "未选择";
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

        private static string GetBrushModeName(EditorBrushMode brushMode)
        {
            switch (brushMode)
            {
                case EditorBrushMode.FilledRectangle:
                    return "实心矩形";
                case EditorBrushMode.HollowRectangle:
                    return "空心矩形";
                default:
                    return "常规";
            }
        }
    }
}
