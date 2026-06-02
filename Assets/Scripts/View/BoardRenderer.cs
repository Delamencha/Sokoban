using System.Collections.Generic;
using UnityEngine;

namespace Sokoban
{
    public class BoardRenderer : MonoBehaviour
    {
        private static readonly Color EditorEmptyColor = new Color(0.72f, 0.67f, 0.56f, 0.10f);
        private static readonly Color FloorColor = new Color(0.72f, 0.67f, 0.56f);
        private static readonly Color WallColor = new Color(0.25f, 0.28f, 0.35f);
        private static readonly Color TargetColor = new Color(0.28f, 0.70f, 0.42f);
        private static readonly Color PlayerOnTargetOverlayColor = new Color(0.28f, 0.70f, 0.42f, 0.42f);
        private static readonly Color BoxColor = new Color(0.74f, 0.43f, 0.18f);
        private static readonly Color BoxOnTargetColor = new Color(0.95f, 0.72f, 0.22f);
        private static readonly Color PlayerColor = new Color(0.20f, 0.45f, 0.94f);
        private static readonly Color RectanglePreviewColor = new Color(0.65f, 0.85f, 1f, 0.55f);

        private readonly List<GameObject> spawnedObjects = new List<GameObject>();
        private readonly List<GameObject> previewObjects = new List<GameObject>();
        private Sprite squareSprite;
        private Transform boardRoot;

        public void Render(BoardModel model)
        {
            Clear();
            EnsureSetup();

            for (int y = 0; y < model.Height; y++)
            {
                for (int x = 0; x < model.Width; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    LevelTile tile = model.GetTile(position);
                    DrawTile(position, tile, model.HasTarget(position), false);

                    if (model.HasBox(position))
                    {
                        DrawCell(position, model.HasTarget(position) ? BoxOnTargetColor : BoxColor, 0.72f, 4, "Box");
                    }

                    if (model.PlayerPosition == position)
                    {
                        DrawCell(position, PlayerColor, 0.62f, 5, "Player");
                        if (model.HasTarget(position))
                        {
                            DrawCell(position, PlayerOnTargetOverlayColor, 0.42f, 6, "Player On Target Overlay");
                        }
                    }
                }
            }

            FitCamera(model.Width, model.Height);
        }

        public void RenderLevel(LevelData level)
        {
            Clear();
            EnsureSetup();
            level.EnsureTiles();

            HashSet<Vector2Int> targetSet = new HashSet<Vector2Int>();
            foreach (GridPosition target in level.targets)
            {
                targetSet.Add(target.ToVector2Int());
            }

            HashSet<Vector2Int> boxSet = new HashSet<Vector2Int>();
            foreach (GridPosition box in level.boxes)
            {
                boxSet.Add(box.ToVector2Int());
            }

            for (int y = 0; y < level.height; y++)
            {
                for (int x = 0; x < level.width; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    DrawTile(position, level.GetTile(position), targetSet.Contains(position), true);

                    if (boxSet.Contains(position))
                    {
                        DrawCell(position, targetSet.Contains(position) ? BoxOnTargetColor : BoxColor, 0.72f, 4, "Editor Box");
                    }

                    if (level.player.ToVector2Int() == position)
                    {
                        DrawCell(position, PlayerColor, 0.62f, 5, "Editor Player");
                        if (targetSet.Contains(position))
                        {
                            DrawCell(position, PlayerOnTargetOverlayColor, 0.42f, 6, "Editor Player On Target Overlay");
                        }
                    }
                }
            }

            FitCamera(level.width, level.height);
        }

        public void ClearBoard()
        {
            Clear();
            ClearPreview();
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            return new Vector2Int(Mathf.RoundToInt(worldPosition.x), Mathf.RoundToInt(worldPosition.y));
        }

        public Vector3 GridToWorld(Vector2Int position)
        {
            return new Vector3(position.x, position.y, 0f);
        }

        public void ShowRectanglePreview(Vector2Int start, Vector2Int end)
        {
            ClearPreview();
            EnsureSetup();

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x != minX && x != maxX && y != minY && y != maxY)
                    {
                        continue;
                    }

                    DrawPreviewCell(new Vector2Int(x, y));
                }
            }
        }

        public void ClearPreview()
        {
            for (int i = previewObjects.Count - 1; i >= 0; i--)
            {
                if (previewObjects[i] != null)
                {
                    Destroy(previewObjects[i]);
                }
            }

            previewObjects.Clear();
        }

        private void DrawTile(Vector2Int position, LevelTile tile, bool hasTarget, bool showEditorEmptyTile)
        {
            if (tile == LevelTile.Empty)
            {
                if (showEditorEmptyTile)
                {
                    DrawCell(position, EditorEmptyColor, 0.96f, 0, "Editor Empty");
                }

                return;
            }

            DrawCell(position, tile == LevelTile.Wall ? WallColor : FloorColor, 0.96f, 1, tile.ToString());
            if (hasTarget)
            {
                DrawCell(position, TargetColor, 0.36f, 3, "Target");
            }
        }

        private void DrawCell(Vector2Int position, Color color, float scale, int sortingOrder, string label)
        {
            GameObject cell = new GameObject(label + " " + position.x + "," + position.y);
            cell.transform.SetParent(boardRoot, false);
            cell.transform.position = GridToWorld(position);
            cell.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = cell.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            spawnedObjects.Add(cell);
        }

        private void DrawPreviewCell(Vector2Int position)
        {
            GameObject cell = new GameObject("Rectangle Preview " + position.x + "," + position.y);
            cell.transform.SetParent(boardRoot, false);
            cell.transform.position = GridToWorld(position);
            cell.transform.localScale = Vector3.one * 0.86f;

            SpriteRenderer renderer = cell.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = RectanglePreviewColor;
            renderer.sortingOrder = 10;
            previewObjects.Add(cell);
        }

        private void EnsureSetup()
        {
            if (boardRoot == null)
            {
                boardRoot = transform;
            }

            if (squareSprite != null)
            {
                return;
            }

            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private void Clear()
        {
            ClearPreview();
            for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            {
                if (spawnedObjects[i] != null)
                {
                    Destroy(spawnedObjects[i]);
                }
            }

            spawnedObjects.Clear();
        }

        private static void FitCamera(int width, int height)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.orthographic = true;
            camera.transform.position = new Vector3((width - 1) * 0.5f, (height - 1) * 0.5f, -10f);
            int longestSide = Mathf.Max(width, height);
            camera.orthographicSize = Mathf.Max(longestSide * 0.65f, 4f);
        }
    }
}
