using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sokoban
{
    [Serializable]
    public class MainFlowData
    {
        public List<string> levelIdsInOrder = new List<string>();
    }

    public static class MainFlowSaveSystem
    {
        private const string ConfigDirectoryName = "Config";
        private const string ConfigFileName = "main_flow.json";

        public static List<LevelFileEntry> LoadMainFlowEntries(IReadOnlyList<LevelFileEntry> allEntries)
        {
            List<LevelFileEntry> entries = NormalizeEntries(allEntries);
            if (entries.Count == 0)
            {
                return entries;
            }

            MainFlowData data = LoadOrCreateData(entries);
            List<string> validIds = FilterValidLevelIds(data.levelIdsInOrder, entries);
            if (!AreSameIds(data.levelIdsInOrder, validIds))
            {
                data.levelIdsInOrder = validIds;
                SaveData(data);
            }

            return GetEntriesByIds(validIds, entries);
        }

        public static void SaveMainFlow(IReadOnlyList<string> levelIdsInOrder)
        {
            MainFlowData data = new MainFlowData
            {
                levelIdsInOrder = NormalizeIds(levelIdsInOrder)
            };

            SaveData(data);
        }

        public static void CleanupInvalidLevelIds(IReadOnlyList<LevelFileEntry> allEntries)
        {
            if (!File.Exists(GetConfigFilePath()))
            {
                return;
            }

            MainFlowData data = LoadData();
            List<string> validIds = FilterValidLevelIds(data.levelIdsInOrder, NormalizeEntries(allEntries));
            if (AreSameIds(data.levelIdsInOrder, validIds))
            {
                return;
            }

            data.levelIdsInOrder = validIds;
            SaveData(data);
        }

        public static bool IsMainFlowLevel(LevelData level, IReadOnlyList<LevelFileEntry> mainFlowEntries)
        {
            if (level == null || string.IsNullOrWhiteSpace(level.id) || mainFlowEntries == null)
            {
                return false;
            }

            return mainFlowEntries.Any(entry =>
                entry != null
                && entry.level != null
                && string.Equals(entry.level.id, level.id, StringComparison.Ordinal));
        }

        public static string GetConfigDirectory()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, ConfigDirectoryName);
        }

        public static string GetConfigFilePath()
        {
            return Path.Combine(GetConfigDirectory(), ConfigFileName);
        }

        private static MainFlowData LoadOrCreateData(IReadOnlyList<LevelFileEntry> allEntries)
        {
            if (File.Exists(GetConfigFilePath()))
            {
                return LoadData();
            }

            MainFlowData data = new MainFlowData
            {
                levelIdsInOrder = LevelSaveSystem.SortEntriesByCreatedAtThenDisplayName(allEntries)
                    .Where(entry => entry != null && entry.level != null && !string.IsNullOrWhiteSpace(entry.level.id))
                    .Select(entry => entry.level.id)
                    .ToList()
            };

            SaveData(data);
            return data;
        }

        private static MainFlowData LoadData()
        {
            try
            {
                MainFlowData data = JsonUtility.FromJson<MainFlowData>(File.ReadAllText(GetConfigFilePath()));
                if (data == null)
                {
                    return new MainFlowData();
                }

                data.levelIdsInOrder = data.levelIdsInOrder ?? new List<string>();
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to load main flow config: " + exception.Message);
                return new MainFlowData();
            }
        }

        private static void SaveData(MainFlowData data)
        {
            Directory.CreateDirectory(GetConfigDirectory());
            File.WriteAllText(GetConfigFilePath(), JsonUtility.ToJson(data ?? new MainFlowData(), true));
        }

        private static List<LevelFileEntry> NormalizeEntries(IReadOnlyList<LevelFileEntry> entries)
        {
            if (entries == null)
            {
                return new List<LevelFileEntry>();
            }

            return entries
                .Where(entry => entry != null && entry.level != null && !string.IsNullOrWhiteSpace(entry.level.id))
                .ToList();
        }

        private static List<string> NormalizeIds(IReadOnlyList<string> ids)
        {
            List<string> result = new List<string>();
            if (ids == null)
            {
                return result;
            }

            foreach (string id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || result.Contains(id))
                {
                    continue;
                }

                result.Add(id);
            }

            return result;
        }

        private static List<string> FilterValidLevelIds(IReadOnlyList<string> ids, IReadOnlyList<LevelFileEntry> allEntries)
        {
            HashSet<string> validIds = new HashSet<string>(
                allEntries
                    .Where(entry => entry != null && entry.level != null && !string.IsNullOrWhiteSpace(entry.level.id))
                    .Select(entry => entry.level.id));

            return NormalizeIds(ids).Where(validIds.Contains).ToList();
        }

        private static List<LevelFileEntry> GetEntriesByIds(IReadOnlyList<string> ids, IReadOnlyList<LevelFileEntry> allEntries)
        {
            Dictionary<string, LevelFileEntry> entriesById = allEntries
                .Where(entry => entry != null && entry.level != null && !string.IsNullOrWhiteSpace(entry.level.id))
                .GroupBy(entry => entry.level.id)
                .ToDictionary(group => group.Key, group => group.First());

            List<LevelFileEntry> result = new List<LevelFileEntry>();
            foreach (string id in NormalizeIds(ids))
            {
                if (entriesById.TryGetValue(id, out LevelFileEntry entry))
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        private static bool AreSameIds(IReadOnlyList<string> first, IReadOnlyList<string> second)
        {
            List<string> firstIds = NormalizeIds(first);
            List<string> secondIds = NormalizeIds(second);
            if (firstIds.Count != secondIds.Count)
            {
                return false;
            }

            for (int i = 0; i < firstIds.Count; i++)
            {
                if (!string.Equals(firstIds[i], secondIds[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
