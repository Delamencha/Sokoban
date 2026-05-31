using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sokoban
{
    public static class GameProgressSaveSystem
    {
        private const string SaveDirectoryName = "Save";
        private const string SaveFileName = "progress.json";

        [Serializable]
        private class ProgressData
        {
            public int highestCompletedLevelIndex = -1;
            public List<string> completedLevelKeys = new List<string>();
        }

        public static bool HasProgress()
        {
            return LoadProgressData().completedLevelKeys.Count > 0;
        }

        public static bool AreAllLevelsCompleted(IReadOnlyList<LevelData> levels)
        {
            if (levels == null || levels.Count == 0)
            {
                return false;
            }

            ProgressData data = LoadProgressData();
            return levels.All(level => IsLevelCompleted(level, data));
        }

        public static bool IsLevelCompleted(LevelData level)
        {
            return IsLevelCompleted(level, LoadProgressData());
        }

        public static int GetContinueLevelIndex(IReadOnlyList<LevelData> levels)
        {
            if (levels == null || levels.Count <= 0)
            {
                return 0;
            }

            ProgressData data = LoadProgressData();
            for (int i = levels.Count - 1; i >= 0; i--)
            {
                if (IsLevelCompleted(levels[i], data))
                {
                    return Mathf.Clamp(i + 1, 0, levels.Count - 1);
                }
            }

            return 0;
        }

        public static void MarkLevelCompleted(LevelData level)
        {
            if (level == null)
            {
                return;
            }

            ProgressData data = LoadProgressData();
            string key = GetLevelKey(level);
            if (!data.completedLevelKeys.Contains(key))
            {
                data.completedLevelKeys.Add(key);
            }

            SaveProgressData(data);
        }

        public static void ClearProgress()
        {
            string path = GetSaveFilePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public static string GetSaveDirectory()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, SaveDirectoryName);
        }

        private static string GetSaveFilePath()
        {
            return Path.Combine(GetSaveDirectory(), SaveFileName);
        }

        private static bool IsLevelCompleted(LevelData level, ProgressData data)
        {
            return level != null && data.completedLevelKeys.Contains(GetLevelKey(level));
        }

        private static string GetLevelKey(LevelData level)
        {
            if (!string.IsNullOrWhiteSpace(level.id))
            {
                return "id:" + level.id;
            }

            if (!string.IsNullOrWhiteSpace(level.displayName))
            {
                return "name:" + level.displayName;
            }

            return JsonUtility.ToJson(level);
        }

        private static ProgressData LoadProgressData()
        {
            string path = GetSaveFilePath();
            if (!File.Exists(path))
            {
                return new ProgressData();
            }

            try
            {
                ProgressData data = JsonUtility.FromJson<ProgressData>(File.ReadAllText(path));
                if (data == null)
                {
                    return new ProgressData();
                }

                data.completedLevelKeys = data.completedLevelKeys ?? new List<string>();
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to load progress save: " + exception.Message);
                return new ProgressData();
            }
        }

        private static void SaveProgressData(ProgressData data)
        {
            Directory.CreateDirectory(GetSaveDirectory());
            File.WriteAllText(GetSaveFilePath(), JsonUtility.ToJson(data, true));
        }
    }
}
