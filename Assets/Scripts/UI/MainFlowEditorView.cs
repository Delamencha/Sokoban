using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class MainFlowEditorView : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private GameObject root;
        [SerializeField] private Transform mainFlowContentRoot;
        [SerializeField] private Transform availableContentRoot;
        [SerializeField] private MainFlowLevelItemView itemPrefab;
        [SerializeField] private TMP_Text mainFlowEmptyText;
        [SerializeField] private TMP_Text availableEmptyText;
        [SerializeField] private Button addButton;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button moveUpButton;
        [SerializeField] private Button moveDownButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button validateButton;
        [SerializeField] private Button backButton;

        private readonly List<LevelFileEntry> mainFlowEntries = new List<LevelFileEntry>();
        private readonly List<LevelFileEntry> availableEntries = new List<LevelFileEntry>();
        private readonly List<MainFlowLevelItemView> mainFlowItemViews = new List<MainFlowLevelItemView>();
        private readonly List<MainFlowLevelItemView> availableItemViews = new List<MainFlowLevelItemView>();
        private Action<LevelFileEntry> onAdd;
        private Action<LevelFileEntry> onRemove;
        private Action<LevelFileEntry> onMoveUp;
        private Action<LevelFileEntry> onMoveDown;
        private Action<LevelFileEntry> onValidate;
        private Action onSave;
        private Action onBack;
        private int selectedMainFlowIndex = -1;
        private int selectedAvailableIndex = -1;

        public void Initialize(
            Action<LevelFileEntry> onAdd,
            Action<LevelFileEntry> onRemove,
            Action<LevelFileEntry> onMoveUp,
            Action<LevelFileEntry> onMoveDown,
            Action<LevelFileEntry> onValidate,
            Action onSave,
            Action onBack)
        {
            this.onAdd = onAdd;
            this.onRemove = onRemove;
            this.onMoveUp = onMoveUp;
            this.onMoveDown = onMoveDown;
            this.onValidate = onValidate;
            this.onSave = onSave;
            this.onBack = onBack;

            BindButton(addButton, () => InvokeWithSelection(availableEntries, selectedAvailableIndex, this.onAdd));
            BindButton(removeButton, () => InvokeWithSelection(mainFlowEntries, selectedMainFlowIndex, this.onRemove));
            BindButton(moveUpButton, () => InvokeWithSelection(mainFlowEntries, selectedMainFlowIndex, this.onMoveUp));
            BindButton(moveDownButton, () => InvokeWithSelection(mainFlowEntries, selectedMainFlowIndex, this.onMoveDown));
            BindButton(validateButton, () => this.onValidate?.Invoke(GetSelectedEntry()));
            BindButton(saveButton, () => this.onSave?.Invoke());
            BindButton(backButton, () => this.onBack?.Invoke());
            UpdateOperationButtons();
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

        public void Rebuild(IReadOnlyList<LevelFileEntry> mainFlowEntries, IReadOnlyList<LevelFileEntry> availableEntries)
        {
            Rebuild(mainFlowEntries, availableEntries, null, false);
        }

        public void Rebuild(
            IReadOnlyList<LevelFileEntry> mainFlowEntries,
            IReadOnlyList<LevelFileEntry> availableEntries,
            string selectedLevelId,
            bool selectInMainFlow)
        {
            ClearItems(mainFlowContentRoot, mainFlowItemViews);
            ClearItems(availableContentRoot, availableItemViews);
            this.mainFlowEntries.Clear();
            this.availableEntries.Clear();
            selectedMainFlowIndex = -1;
            selectedAvailableIndex = -1;

            if (mainFlowEntries != null)
            {
                this.mainFlowEntries.AddRange(mainFlowEntries);
            }

            if (availableEntries != null)
            {
                this.availableEntries.AddRange(availableEntries);
            }

            SetEmptyText(mainFlowEmptyText, this.mainFlowEntries.Count == 0);
            SetEmptyText(availableEmptyText, this.availableEntries.Count == 0);
            RebuildList(this.mainFlowEntries, mainFlowContentRoot, mainFlowItemViews, SelectMainFlowIndex);
            RebuildList(this.availableEntries, availableContentRoot, availableItemViews, SelectAvailableIndex);
            RestoreSelection(selectedLevelId, selectInMainFlow);
            UpdateOperationButtons();
        }

        private void RestoreSelection(string selectedLevelId, bool selectInMainFlow)
        {
            if (string.IsNullOrWhiteSpace(selectedLevelId))
            {
                return;
            }

            int index = FindEntryIndex(selectInMainFlow ? mainFlowEntries : availableEntries, selectedLevelId);
            if (index < 0)
            {
                return;
            }

            if (selectInMainFlow)
            {
                SelectMainFlowIndex(index);
            }
            else
            {
                SelectAvailableIndex(index);
            }
        }

        private static int FindEntryIndex(IReadOnlyList<LevelFileEntry> entries, string levelId)
        {
            if (entries == null)
            {
                return -1;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null
                    && entries[i].level != null
                    && string.Equals(entries[i].level.id, levelId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RebuildList(
            IReadOnlyList<LevelFileEntry> entries,
            Transform contentRoot,
            List<MainFlowLevelItemView> itemViews,
            Action<int> onSelectIndex)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                int entryIndex = i;
                MainFlowLevelItemView itemView = CreateItem(contentRoot);
                if (itemView == null)
                {
                    return;
                }

                itemView.SetData(entryIndex, entries[entryIndex], () => onSelectIndex(entryIndex));
                itemViews.Add(itemView);
            }
        }

        private void SelectMainFlowIndex(int index)
        {
            selectedMainFlowIndex = index;
            selectedAvailableIndex = -1;
            SetSelected(mainFlowItemViews, selectedMainFlowIndex);
            SetSelected(availableItemViews, selectedAvailableIndex);
            UpdateOperationButtons();
        }

        private void SelectAvailableIndex(int index)
        {
            selectedAvailableIndex = index;
            selectedMainFlowIndex = -1;
            SetSelected(mainFlowItemViews, selectedMainFlowIndex);
            SetSelected(availableItemViews, selectedAvailableIndex);
            UpdateOperationButtons();
        }

        private void UpdateOperationButtons()
        {
            bool hasAvailableSelection = selectedAvailableIndex >= 0 && selectedAvailableIndex < availableEntries.Count;
            bool hasMainFlowSelection = selectedMainFlowIndex >= 0 && selectedMainFlowIndex < mainFlowEntries.Count;

            SetButtonInteractable(addButton, hasAvailableSelection);
            SetButtonInteractable(removeButton, hasMainFlowSelection);
            SetButtonInteractable(moveUpButton, hasMainFlowSelection && selectedMainFlowIndex > 0);
            SetButtonInteractable(moveDownButton, hasMainFlowSelection && selectedMainFlowIndex < mainFlowEntries.Count - 1);
            SetButtonInteractable(validateButton, hasAvailableSelection || hasMainFlowSelection);
            SetButtonInteractable(saveButton, true);
            SetButtonInteractable(backButton, true);
        }

        private LevelFileEntry GetSelectedEntry()
        {
            if (selectedAvailableIndex >= 0 && selectedAvailableIndex < availableEntries.Count)
            {
                return availableEntries[selectedAvailableIndex];
            }

            if (selectedMainFlowIndex >= 0 && selectedMainFlowIndex < mainFlowEntries.Count)
            {
                return mainFlowEntries[selectedMainFlowIndex];
            }

            return null;
        }

        private void InvokeWithSelection(
            IReadOnlyList<LevelFileEntry> entries,
            int selectedIndex,
            Action<LevelFileEntry> action)
        {
            if (entries == null || selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                return;
            }

            action?.Invoke(entries[selectedIndex]);
        }

        private MainFlowLevelItemView CreateItem(Transform contentRoot)
        {
            if (itemPrefab == null || contentRoot == null)
            {
                Debug.LogError("MainFlowEditorView requires an item prefab and content roots.");
                return null;
            }

            MainFlowLevelItemView item = Instantiate(itemPrefab, contentRoot);
            item.gameObject.SetActive(true);
            return item;
        }

        private void ClearItems(Transform contentRoot, List<MainFlowLevelItemView> itemViews)
        {
            itemViews.Clear();
            if (contentRoot == null)
            {
                return;
            }

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = contentRoot.GetChild(i);
                if (IsEmptyText(child))
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }

        private bool IsEmptyText(Transform child)
        {
            return (mainFlowEmptyText != null && child == mainFlowEmptyText.transform)
                || (availableEmptyText != null && child == availableEmptyText.transform);
        }

        private static void SetSelected(IReadOnlyList<MainFlowLevelItemView> itemViews, int selectedIndex)
        {
            if (itemViews == null)
            {
                return;
            }

            for (int i = 0; i < itemViews.Count; i++)
            {
                itemViews[i].SetSelected(i == selectedIndex);
            }
        }

        private static void SetEmptyText(TMP_Text emptyText, bool visible)
        {
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(visible);
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

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }
}
