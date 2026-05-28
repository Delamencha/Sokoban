using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sokoban
{
    public static class LevelSaveSystem
    {
        private const string BuiltInResourcePath = "Levels";
        private const string UserLevelDirectoryName = "Level";

        public static List<LevelData> LoadAllLevels()
        {
            List<LevelData> levels = new List<LevelData>();
            levels.AddRange(LoadBuiltInLevels());
            levels.AddRange(LoadUserLevels());

            if (levels.Count == 0)
            {
                levels.AddRange(CreateFallbackLevels());
            }

            return levels;
        }

        public static List<LevelData> LoadBuiltInLevels()
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(BuiltInResourcePath);
            return assets
                .Select(ParseLevel)
                .Where(level => level != null)
                .OrderBy(level => level.displayName)
                .ToList();
        }

        public static List<LevelData> LoadUserLevels()
        {
            string directory = GetUserLevelDirectory();
            if (!Directory.Exists(directory))
            {
                return new List<LevelData>();
            }

            List<LevelData> levels = new List<LevelData>();
            string[] files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                try
                {
                    LevelData level = JsonUtility.FromJson<LevelData>(File.ReadAllText(file));
                    if (level != null)
                    {
                        level.EnsureTiles();
                        levels.Add(level);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Failed to load user level '" + file + "': " + exception.Message);
                }
            }

            return levels.OrderBy(level => level.displayName).ToList();
        }

        public static string SaveUserLevel(LevelData level)
        {
            List<string> errors = LevelValidator.Validate(level);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }

            Directory.CreateDirectory(GetUserLevelDirectory());
            string fileName = SanitizeFileName(string.IsNullOrWhiteSpace(level.id) ? level.displayName : level.id);
            string path = Path.Combine(GetUserLevelDirectory(), fileName + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(level, true));
            return path;
        }

        public static string GetUserLevelDirectory()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, UserLevelDirectoryName);
        }

        private static LevelData ParseLevel(TextAsset asset)
        {
            try
            {
                LevelData level = JsonUtility.FromJson<LevelData>(asset.text);
                if (level == null)
                {
                    return null;
                }

                level.EnsureTiles();
                if (string.IsNullOrWhiteSpace(level.id))
                {
                    level.id = asset.name;
                }

                if (string.IsNullOrWhiteSpace(level.displayName))
                {
                    level.displayName = asset.name;
                }

                return level;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to parse built-in level '" + asset.name + "': " + exception.Message);
                return null;
            }
        }

        private static string SanitizeFileName(string value)
        {
            string safeName = string.IsNullOrWhiteSpace(value) ? "level" : value.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalidChar, '_');
            }

            return safeName;
        }

        private static List<LevelData> CreateFallbackLevels()
        {
            return new List<LevelData>
            {
                new LevelData
                {
                    id = "fallback_01",
                    displayName = "Fallback 01",
                    width = 7,
                    height = 7,
                    tiles = new[]
                    {
                        "#######",
                        "#.....#",
                        "#.....#",
                        "#.....#",
                        "#.....#",
                        "#.....#",
                        "#######"
                    },
                    player = new GridPosition(2, 2),
                    boxes = new List<GridPosition> { new GridPosition(3, 3) },
                    targets = new List<GridPosition> { new GridPosition(4, 3) }
                }
            };
        }
    }
}
