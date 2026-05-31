using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sokoban
{
    public class LevelFileEntry
    {
        public LevelFileEntry(LevelData level, string filePath)
        {
            this.level = level;
            this.filePath = filePath;
        }

        public LevelData level;
        public string filePath;
    }

    public static class LevelSaveSystem
    {
        private const string LevelDirectoryName = "Level";

        public static List<LevelData> LoadAllLevels()
        {
            List<LevelData> levels = LoadLevelFileEntries()
                .Select(entry => entry.level)
                .ToList();

            if (levels.Count == 0)
            {
                levels.AddRange(CreateFallbackLevels());
            }

            return levels;
        }

        public static List<LevelData> LoadLevels()
        {
            return LoadLevelFileEntries().Select(entry => entry.level).ToList();
        }

        public static List<LevelFileEntry> LoadLevelFileEntries()
        {
            return LoadLevelEntriesFromDirectory();
        }

        public static List<LevelFileEntry> LoadLevelEntriesFromDirectory()
        {
            string directory = GetLevelDirectory();
            if (!Directory.Exists(directory))
            {
                return new List<LevelFileEntry>();
            }

            List<LevelFileEntry> entries = new List<LevelFileEntry>();
            string[] files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                LevelData level = LoadLevelFile(file);
                if (level != null)
                {
                    entries.Add(new LevelFileEntry(level, file));
                }
            }

            return entries.OrderBy(entry => entry.level.displayName).ToList();
        }

        public static string SaveLevel(LevelData level)
        {
            return SaveLevel(level, string.Empty);
        }

        public static string SaveLevel(LevelData level, string existingPath)
        {
            string resolvedDisplayName = ResolveLevelDisplayName(level.displayName, existingPath);
            level.displayName = resolvedDisplayName;
            if (string.IsNullOrWhiteSpace(level.id))
            {
                level.id = CreateStableLevelId();
            }

            List<string> errors = LevelValidator.Validate(level);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }

            Directory.CreateDirectory(GetLevelDirectory());
            string path = string.IsNullOrWhiteSpace(existingPath) ? GetAvailableLevelPath(level) : existingPath;
            File.WriteAllText(path, JsonUtility.ToJson(level, true));
            return path;
        }

        public static string CreateStableLevelId()
        {
            return "level_" + Guid.NewGuid().ToString("N");
        }

        public static string ResolveLevelDisplayName(string desiredName)
        {
            return ResolveLevelDisplayName(desiredName, string.Empty);
        }

        public static string ResolveLevelDisplayName(string desiredName, string allowedExistingPath)
        {
            string displayName = string.IsNullOrWhiteSpace(desiredName) ? "Custom Level" : desiredName.Trim();
            if (!HasLevelDisplayName(displayName, allowedExistingPath))
            {
                return displayName;
            }

            string candidate = CreateNextDuplicateDisplayName(displayName);
            if (HasLevelDisplayName(candidate, allowedExistingPath))
            {
                throw new InvalidOperationException("命名重复：已存在关卡“" + displayName + "”，自动候选名称“" + candidate + "”也已存在。");
            }

            return candidate;
        }

        public static LevelFileEntry CopyLevel(LevelData source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            LevelData copy = source.Clone();
            copy.displayName = ResolveLevelDisplayName(copy.displayName + " Copy");
            copy.id = CreateStableLevelId();
            string path = SaveLevel(copy);
            return new LevelFileEntry(copy, path);
        }

        public static LevelFileEntry RenameLevel(LevelFileEntry entry, string newDisplayName)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.filePath))
            {
                throw new InvalidOperationException("只能重命名已保存的关卡。");
            }

            string displayName = ResolveLevelDisplayName(newDisplayName, entry.filePath);
            LevelData level = entry.level.Clone();
            level.displayName = displayName;
            if (string.IsNullOrWhiteSpace(level.id))
            {
                level.id = CreateStableLevelId();
            }

            Directory.CreateDirectory(GetLevelDirectory());
            string newPath = GetAvailableLevelPath(level, entry.filePath);
            File.WriteAllText(newPath, JsonUtility.ToJson(level, true));

            if (!PathsEqual(entry.filePath, newPath) && File.Exists(entry.filePath))
            {
                File.Delete(entry.filePath);
            }

            return new LevelFileEntry(level, newPath);
        }

        public static void DeleteLevel(LevelFileEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.filePath))
            {
                throw new InvalidOperationException("只能删除已保存的关卡。");
            }

            if (File.Exists(entry.filePath))
            {
                File.Delete(entry.filePath);
            }
        }

        public static string GetLevelDirectory()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, LevelDirectoryName);
        }

        private static LevelData LoadLevelFile(string file)
        {
            try
            {
                LevelData level = JsonUtility.FromJson<LevelData>(File.ReadAllText(file));
                if (level == null)
                {
                    return null;
                }

                level.EnsureTiles();
                if (string.IsNullOrWhiteSpace(level.id))
                {
                    level.id = Path.GetFileNameWithoutExtension(file);
                }

                if (string.IsNullOrWhiteSpace(level.displayName))
                {
                    level.displayName = Path.GetFileNameWithoutExtension(file);
                }

                return level;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to load user level '" + file + "': " + exception.Message);
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

        private static string GetAvailableLevelPath(LevelData level)
        {
            return GetAvailableLevelPath(level, string.Empty);
        }

        private static string GetAvailableLevelPath(LevelData level, string allowedExistingPath)
        {
            string fileName = SanitizeFileName(level.displayName);
            string directory = GetLevelDirectory();
            string candidate = Path.Combine(directory, fileName + ".json");
            if (string.IsNullOrWhiteSpace(allowedExistingPath) || !PathsEqual(candidate, allowedExistingPath))
            {
                candidate = GetUniquePath(candidate, allowedExistingPath);
            }

            return candidate;
        }

        private static bool HasLevelDisplayName(string displayName, string allowedExistingPath)
        {
            return LoadLevelEntriesFromDirectory().Any(entry =>
                !PathsEqual(entry.filePath, allowedExistingPath)
                && string.Equals(entry.level.displayName, displayName, StringComparison.OrdinalIgnoreCase));
        }

        private static string CreateNextDuplicateDisplayName(string displayName)
        {
            int separatorIndex = displayName.LastIndexOf('-');
            if (separatorIndex >= 0 && separatorIndex < displayName.Length - 1)
            {
                string suffix = displayName.Substring(separatorIndex + 1);
                if (int.TryParse(suffix, out int number))
                {
                    return displayName.Substring(0, separatorIndex) + "-" + (number + 1);
                }
            }

            return displayName + "-1";
        }

        private static string GetUniquePath(string path, string allowedExistingPath)
        {
            if (!File.Exists(path) || PathsEqual(path, allowedExistingPath))
            {
                return path;
            }

            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            int suffix = 2;
            string candidate;
            do
            {
                candidate = Path.Combine(directory, fileName + "_" + suffix + extension);
                suffix++;
            }
            while (File.Exists(candidate) && !PathsEqual(candidate, allowedExistingPath));

            return candidate;
        }

        private static bool PathsEqual(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
            {
                return false;
            }

            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
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
