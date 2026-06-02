using System;
using System.Collections.Generic;
using System.Globalization;
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

    [Serializable]
    public class LevelIoConfig
    {
        public string importDirectoryName = "Import";
        public string exportDirectoryName = "Export";
    }

    public class LevelImportSummary
    {
        public int importedCount;
        public int skippedCount;
        public int failedCount;
        public readonly List<string> messages = new List<string>();
    }

    public static class LevelSaveSystem
    {
        private const string LevelDirectoryName = "Level";
        private const string ConfigDirectoryName = "Config";
        private const string IoConfigFileName = "level_io_config.json";
        private const string MainFlowConfigFileName = "main_flow.json";
        private const string SeedInitializationMarkerFileName = "level_seed_initialized.json";
        private const string ImportedDirectoryName = "Imported";
        private const string FailedDirectoryName = "Failed";
        private static bool seedInitializationChecked;

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
            InitializeSeedLevelsIfNeeded();

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

            return entries;
        }

        public static List<LevelFileEntry> SortEntriesByCreatedAtThenDisplayName(IEnumerable<LevelFileEntry> entries)
        {
            return (entries ?? Enumerable.Empty<LevelFileEntry>())
                .Where(entry => entry != null && entry.level != null)
                .OrderBy(entry => GetCreatedAtSortValue(entry.level))
                .ThenBy(entry => entry.level.displayName)
                .ThenBy(entry => entry.level.id)
                .ThenBy(entry => entry.filePath)
                .ToList();
        }

        private static DateTimeOffset GetCreatedAtSortValue(LevelData level)
        {
            if (level != null
                && DateTimeOffset.TryParse(
                    level.createdAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset createdAt))
            {
                return createdAt;
            }

            return DateTimeOffset.MaxValue;
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

            level.EnsureTiles();

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
            copy.createdAt = LevelData.CreateCurrentTimestamp();
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

        public static LevelImportSummary ImportLevelsFromConfiguredDirectory()
        {
            string importDirectory = GetImportDirectory();
            if (PathsEqual(importDirectory, GetLevelDirectory()))
            {
                throw new InvalidOperationException("导入目录不能设置为关卡库目录 Level。");
            }

            Directory.CreateDirectory(importDirectory);

            LevelImportSummary summary = new LevelImportSummary();
            string[] files = Directory.GetFiles(importDirectory, "*.json", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                summary.messages.Add("导入目录没有可导入的 .json 文件：" + importDirectory);
                return summary;
            }

            foreach (string file in files)
            {
                ImportLevelFile(file, summary);
            }

            return summary;
        }

        public static string ExportLevel(LevelFileEntry entry)
        {
            if (entry == null || entry.level == null)
            {
                throw new InvalidOperationException("请选择要导出的关卡。");
            }

            List<string> errors = LevelValidator.Validate(entry.level);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }

            string exportDirectory = GetExportDirectory();
            if (PathsEqual(exportDirectory, GetLevelDirectory()))
            {
                throw new InvalidOperationException("导出目录不能设置为关卡库目录 Level。");
            }

            Directory.CreateDirectory(exportDirectory);
            string fileName = SanitizeFileName(string.IsNullOrWhiteSpace(entry.level.displayName) ? entry.level.id : entry.level.displayName);
            string path = GetUniquePath(Path.Combine(exportDirectory, fileName + ".json"), string.Empty);
            File.WriteAllText(path, JsonUtility.ToJson(entry.level, true));
            return path;
        }

        public static string GetLevelDirectory()
        {
            return Path.Combine(GetProjectRoot(), LevelDirectoryName);
        }

        public static string GetImportDirectory()
        {
            return Path.Combine(GetProjectRoot(), NormalizeDirectoryName(LoadLevelIoConfig().importDirectoryName, "Import"));
        }

        public static string GetExportDirectory()
        {
            return Path.Combine(GetProjectRoot(), NormalizeDirectoryName(LoadLevelIoConfig().exportDirectoryName, "Export"));
        }

        public static string GetLevelIoConfigPath()
        {
            return Path.Combine(GetProjectRoot(), ConfigDirectoryName, IoConfigFileName);
        }

        private static void InitializeSeedLevelsIfNeeded()
        {
            if (seedInitializationChecked)
            {
                return;
            }

            seedInitializationChecked = true;
            string markerPath = GetSeedInitializationMarkerPath();
            if (File.Exists(markerPath))
            {
                return;
            }

            string levelDirectory = GetLevelDirectory();
            if (Directory.Exists(levelDirectory)
                && Directory.GetFiles(levelDirectory, "*.json", SearchOption.TopDirectoryOnly).Length > 0)
            {
                WriteSeedInitializationMarker(0, "Skipped because external Level directory already contains levels.");
                return;
            }

            string seedDirectory = GetSeedLevelDirectory();
            if (!Directory.Exists(seedDirectory))
            {
                Debug.Log("No built-in seed level directory found: " + seedDirectory);
                return;
            }

            string[] seedFiles = Directory.GetFiles(seedDirectory, "*.json", SearchOption.TopDirectoryOnly);
            if (seedFiles.Length == 0)
            {
                Debug.Log("Built-in seed level directory has no .json files: " + seedDirectory);
                return;
            }

            Directory.CreateDirectory(levelDirectory);
            int copiedCount = 0;
            foreach (string seedFile in seedFiles)
            {
                string targetPath = Path.Combine(levelDirectory, Path.GetFileName(seedFile));
                if (File.Exists(targetPath))
                {
                    continue;
                }

                File.Copy(seedFile, targetPath);
                copiedCount++;
            }

            WriteSeedInitializationMarker(copiedCount, "Copied built-in seed levels from StreamingAssets.");
            if (copiedCount > 0)
            {
                TryCopySeedMainFlowConfig();
            }

            Debug.Log("Initialized built-in seed levels: " + copiedCount + " file(s) copied.");
        }

        private static string GetSeedLevelDirectory()
        {
            return Path.Combine(Application.streamingAssetsPath, LevelDirectoryName);
        }

        private static string GetSeedConfigDirectory()
        {
            return Path.Combine(Application.streamingAssetsPath, ConfigDirectoryName);
        }

        private static string GetSeedMainFlowConfigPath()
        {
            return Path.Combine(GetSeedConfigDirectory(), MainFlowConfigFileName);
        }

        private static string GetMainFlowConfigPath()
        {
            return Path.Combine(GetProjectRoot(), ConfigDirectoryName, MainFlowConfigFileName);
        }

        private static string GetSeedInitializationMarkerPath()
        {
            return Path.Combine(GetProjectRoot(), ConfigDirectoryName, SeedInitializationMarkerFileName);
        }

        private static void TryCopySeedMainFlowConfig()
        {
            try
            {
                string seedConfigPath = GetSeedMainFlowConfigPath();
                if (!File.Exists(seedConfigPath))
                {
                    Debug.Log("No built-in main flow config found: " + seedConfigPath);
                    return;
                }

                string targetConfigPath = GetMainFlowConfigPath();
                if (File.Exists(targetConfigPath))
                {
                    Debug.Log("External main flow config already exists: " + targetConfigPath);
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetConfigPath));
                File.Copy(seedConfigPath, targetConfigPath);
                Debug.Log("Initialized built-in main flow config: " + targetConfigPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to initialize built-in main flow config: " + exception.Message);
            }
        }

        private static void WriteSeedInitializationMarker(int copiedCount, string message)
        {
            string markerPath = GetSeedInitializationMarkerPath();
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            string json = "{\n"
                + "  \"initializedAt\": \"" + DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture) + "\",\n"
                + "  \"copiedCount\": " + copiedCount + ",\n"
                + "  \"message\": \"" + EscapeJsonString(message) + "\"\n"
                + "}";
            File.WriteAllText(markerPath, json);
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

        private static void ImportLevelFile(string file, LevelImportSummary summary)
        {
            try
            {
                LevelData level = LoadImportLevelFile(file);
                if (level == null)
                {
                    throw new InvalidOperationException("关卡数据为空。");
                }

                bool alreadyExists = TryResolveImportedLevelIdentity(level, out bool reassignedId);
                if (alreadyExists)
                {
                    summary.skippedCount++;
                    summary.messages.Add("已跳过已存在关卡：" + Path.GetFileName(file));
                    MoveImportedFile(file, ImportedDirectoryName);
                    return;
                }

                level.createdAt = LevelData.CreateCurrentTimestamp();
                string originalDisplayName = level.displayName;
                string path = SaveLevel(level);
                summary.importedCount++;
                string message = "已导入：" + level.displayName;
                if (reassignedId)
                {
                    message += "（检测到 ID 冲突，已生成新 ID）";
                }

                if (!string.Equals(originalDisplayName, level.displayName, StringComparison.Ordinal))
                {
                    message += "（名称已调整）";
                }

                summary.messages.Add(message + " -> " + path);
                MoveImportedFile(file, ImportedDirectoryName);
            }
            catch (Exception exception)
            {
                summary.failedCount++;
                summary.messages.Add("导入失败：" + Path.GetFileName(file) + " - " + exception.Message);
                MoveImportedFile(file, FailedDirectoryName);
            }
        }

        private static LevelData LoadImportLevelFile(string file)
        {
            LevelData level = JsonUtility.FromJson<LevelData>(File.ReadAllText(file));
            if (level == null)
            {
                return null;
            }

            level.EnsureTiles();
            if (string.IsNullOrWhiteSpace(level.id))
            {
                level.id = CreateStableLevelId();
            }

            if (string.IsNullOrWhiteSpace(level.displayName))
            {
                level.displayName = Path.GetFileNameWithoutExtension(file);
            }

            List<string> errors = LevelValidator.Validate(level);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }

            return level;
        }

        private static bool TryResolveImportedLevelIdentity(LevelData importedLevel, out bool reassignedId)
        {
            reassignedId = false;
            if (importedLevel == null || string.IsNullOrWhiteSpace(importedLevel.id))
            {
                return false;
            }

            LevelFileEntry existingEntry = LoadLevelEntriesFromDirectory()
                .FirstOrDefault(entry => entry.level != null && string.Equals(entry.level.id, importedLevel.id, StringComparison.Ordinal));
            if (existingEntry == null)
            {
                return false;
            }

            if (AreSameLevelContent(existingEntry.level, importedLevel))
            {
                return true;
            }

            importedLevel.id = CreateStableLevelId();
            reassignedId = true;
            return false;
        }

        private static bool AreSameLevelContent(LevelData first, LevelData second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return string.Equals(JsonUtility.ToJson(first), JsonUtility.ToJson(second), StringComparison.Ordinal);
        }

        private static void MoveImportedFile(string file, string targetDirectoryName)
        {
            try
            {
                string directory = Path.Combine(Path.GetDirectoryName(file), targetDirectoryName);
                Directory.CreateDirectory(directory);
                string target = GetUniquePath(Path.Combine(directory, Path.GetFileName(file)), string.Empty);
                File.Move(file, target);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to move imported file '" + file + "': " + exception.Message);
            }
        }

        private static LevelIoConfig LoadLevelIoConfig()
        {
            string path = GetLevelIoConfigPath();
            if (!File.Exists(path))
            {
                LevelIoConfig defaultConfig = new LevelIoConfig();
                SaveLevelIoConfig(defaultConfig);
                return defaultConfig;
            }

            try
            {
                LevelIoConfig config = JsonUtility.FromJson<LevelIoConfig>(File.ReadAllText(path));
                if (config == null)
                {
                    return new LevelIoConfig();
                }

                return config;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to load level IO config: " + exception.Message);
                return new LevelIoConfig();
            }
        }

        private static void SaveLevelIoConfig(LevelIoConfig config)
        {
            string path = GetLevelIoConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(config ?? new LevelIoConfig(), true));
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

        private static string NormalizeDirectoryName(string value, string fallback)
        {
            string directoryName = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            directoryName = directoryName.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                directoryName = directoryName.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(directoryName) ? fallback : directoryName;
        }

        private static string EscapeJsonString(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
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
