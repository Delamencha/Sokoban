using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public enum LevelListFilter
    {
        MainFlow,
        NotInMainFlow,
        All
    }

    public class LevelListView : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private GameObject root;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private LevelListItemView itemPrefab;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private TMP_Dropdown filterDropdown;
        [SerializeField] private Button backButton;

        private Action<LevelListFilter> onFilterChanged;

        public void Initialize(Action onBack, Action<LevelListFilter> onFilterChanged)
        {
            this.onFilterChanged = onFilterChanged;
            BindButton(backButton, onBack);
            ConfigureFilterDropdown();
            HideEmptyText();
        }

        public void SetFilter(LevelListFilter filter)
        {
            if (filterDropdown != null)
            {
                filterDropdown.SetValueWithoutNotify((int)filter);
            }
        }

        public void SetFilterVisible(bool visible)
        {
            if (filterDropdown != null)
            {
                filterDropdown.gameObject.SetActive(visible);
            }
        }

        public void Show()
        {
            GameObject containerObject = GetContainer();
            GameObject rootObject = GetRoot();
            containerObject.SetActive(true);
            SetParentsActive(rootObject.transform);
            rootObject.SetActive(true);
            containerObject.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            GetRoot().SetActive(false);
        }

        public void Rebuild(
            IReadOnlyList<LevelFileEntry> entries,
            Func<LevelData, bool> isCompleted,
            Func<LevelData, bool> isInMainFlow,
            Action<int> onSelectLevel)
        {
            ClearLevelItems();

            bool hasLevels = entries != null && entries.Count > 0;
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(!hasLevels);
            }

            if (!hasLevels)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                int levelIndex = i;
                LevelData level = entries[i] != null ? entries[i].level : null;
                LevelListItemView itemView = CreateItem();
                if (itemView == null)
                {
                    return;
                }

                itemView.SetData(
                    levelIndex,
                    level != null ? level.displayName : string.Empty,
                    level != null && isCompleted != null && isCompleted(level),
                    level != null && isInMainFlow != null && isInMainFlow(level),
                    () => onSelectLevel?.Invoke(levelIndex));
            }
        }

        private LevelListItemView CreateItem()
        {
            if (itemPrefab != null)
            {
                LevelListItemView item = Instantiate(itemPrefab, contentRoot);
                item.gameObject.SetActive(true);
                return item;
            }

            Debug.LogError("LevelListView requires an item prefab.");
            return null;
        }

        private void ClearLevelItems()
        {
            if (contentRoot == null)
            {
                return;
            }

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = contentRoot.GetChild(i);
                if (emptyText != null && child == emptyText.transform)
                {
                    continue;
                }

                if (backButton != null && child == backButton.transform)
                {
                    continue;
                }

                if (filterDropdown != null && child == filterDropdown.transform)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }

        private void HideEmptyText()
        {
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(false);
            }
        }

        private GameObject GetRoot()
        {
            return root != null ? root : gameObject;
        }

        private GameObject GetContainer()
        {
            return container != null ? container : GetRoot();
        }

        private static void SetParentsActive(Transform child)
        {
            Transform parent = child.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    parent.gameObject.SetActive(true);
                }

                parent = parent.parent;
            }
        }

        private static void BindButton(Button button, Action action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(() => action());
            }
        }

        private void ConfigureFilterDropdown()
        {
            if (filterDropdown == null)
            {
                return;
            }

            filterDropdown.onValueChanged.RemoveAllListeners();
            filterDropdown.options.Clear();
            filterDropdown.options.Add(new TMP_Dropdown.OptionData("主流程关卡"));
            filterDropdown.options.Add(new TMP_Dropdown.OptionData("未加入主流程"));
            filterDropdown.options.Add(new TMP_Dropdown.OptionData("全部关卡"));
            filterDropdown.SetValueWithoutNotify((int)LevelListFilter.MainFlow);
            filterDropdown.onValueChanged.AddListener(value =>
            {
                LevelListFilter filter = value >= 0 && value <= (int)LevelListFilter.All
                    ? (LevelListFilter)value
                    : LevelListFilter.MainFlow;
                onFilterChanged?.Invoke(filter);
            });
            filterDropdown.RefreshShownValue();
        }

    }
}
