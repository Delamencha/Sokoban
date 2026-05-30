using System.Collections.Generic;
using UnityEngine;

namespace Sokoban
{
    public class BoardRenderer : MonoBehaviour
    {
        private static readonly Color EmptyColor = new Color(0.08f, 0.09f, 0.12f);
        private static readonly Color FloorColor = new Color(0.72f, 0.67f, 0.56f);
        private static readonly Color WallColor = new Color(0.25f, 0.28f, 0.35f);
        private static readonly Color TargetColor = new Color(0.28f, 0.70f, 0.42f);
        private static readonly Color BoxColor = new Color(0.74f, 0.43f, 0.18f);
        private static readonly Color BoxOnTargetColor = new Color(0.95f, 0.72f, 0.22f);
        private static readonly Color PlayerColor = new Color(0.20f, 0.45f, 0.94f);

        private readonly List<GameObject> spawnedObjects = new List<GameObject>();
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
                    DrawTile(position, tile, model.HasTarget(position));

                    if (model.HasBox(position))
                    {
                        DrawCell(position, model.HasTarget(position) ? BoxOnTargetColor : BoxColor, 0.72f, 4, "Box");
                    }

                    if (model.PlayerPosition == position)
                    {
                        DrawCell(position, PlayerColor, 0.62f, 5, "Player");
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
                    DrawTile(position, level.GetTile(position), targetSet.Contains(position));

                    if (boxSet.Contains(position))
                    {
                        DrawCell(position, targetSet.Contains(position) ? BoxOnTargetColor : BoxColor, 0.72f, 4, "Editor Box");
                    }

                    if (level.player.ToVector2Int() == position)
                    {
                        DrawCell(position, PlayerColor, 0.62f, 5, "Editor Player");
                    }
                }
            }

            FitCamera(level.width, level.height);
        }

        public void ClearBoard()
        {
            Clear();
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            return new Vector2Int(Mathf.RoundToInt(worldPosition.x), Mathf.RoundToInt(worldPosition.y));
        }

        public Vector3 GridToWorld(Vector2Int position)
        {
            return new Vector3(position.x, position.y, 0f);
        }

        private void DrawTile(Vector2Int position, LevelTile tile, bool hasTarget)
        {
            if (tile == LevelTile.Empty)
            {
                DrawCell(position, EmptyColor, 0.96f, 0, "Empty");
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
            camera.orthographicSize = Mathf.Max(height * 0.65f, width * 0.35f, 4f);
        }
    }
}
