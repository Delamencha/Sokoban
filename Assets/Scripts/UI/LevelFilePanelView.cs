using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class LevelFilePanelView : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private GameObject root;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private LevelFileListItemView itemPrefab;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private Button newButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button copyButton;
        [SerializeField] private Button renameButton;
        [SerializeField] private Button editButton;
        [SerializeField] private Button backButton;

        private readonly List<LevelFileEntry> entries = new List<LevelFileEntry>();
        private readonly List<LevelFileListItemView> itemViews = new List<LevelFileListItemView>();
        private Action onNew;
        private Action<LevelFileEntry> onDelete;
        private Action<LevelFileEntry> onCopy;
        private Action<LevelFileEntry> onRename;
        private Action<LevelFileEntry> onEdit;
        private Action onBack;
        private Action<LevelFileEntry> onSelectionChanged;
        private Func<LevelFileEntry, bool> isInMainFlow;
        private int selectedIndex = -1;

        public void Initialize(
            Action onNew,
            Action<LevelFileEntry> onDelete,
            Action<LevelFileEntry> onCopy,
            Action<LevelFileEntry> onRename,
            Action<LevelFileEntry> onEdit,
            Action onBack,
            Action<LevelFileEntry> onSelectionChanged = null)
        {
            this.onNew = onNew;
            this.onDelete = onDelete;
            this.onCopy = onCopy;
            this.onRename = onRename;
            this.onEdit = onEdit;
            this.onBack = onBack;
            this.onSelectionChanged = onSelectionChanged;

            BindButton(newButton, () => this.onNew?.Invoke());
            BindButton(deleteButton, () => InvokeWithSelection(this.onDelete));
            BindButton(copyButton, () => InvokeWithSelection(this.onCopy));
            BindButton(renameButton, () => InvokeWithSelection(this.onRename));
            BindButton(editButton, () => InvokeWithSelection(this.onEdit));
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

        public void Rebuild(IReadOnlyList<LevelFileEntry> levelEntries, Func<LevelFileEntry, bool> isInMainFlow = null)
        {
            ClearLevelItems();
            entries.Clear();
            selectedIndex = -1;
            this.isInMainFlow = isInMainFlow;

            if (levelEntries != null)
            {
                entries.AddRange(levelEntries);
            }

            bool hasEntries = entries.Count > 0;
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(!hasEntries);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                int entryIndex = i;
                LevelFileListItemView itemView = CreateItem();
                if (itemView == null)
                {
                    break;
                }

                itemView.SetData(entryIndex, entries[entryIndex], IsInMainFlow(entries[entryIndex]), () => SelectIndex(entryIndex));
                itemViews.Add(itemView);
            }

            UpdateOperationButtons();
        }

        public LevelFileEntry GetSelectedEntry()
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                return null;
            }

            return entries[selectedIndex];
        }

        private void SelectIndex(int index)
        {
            selectedIndex = index;
            for (int i = 0; i < itemViews.Count; i++)
            {
                itemViews[i].SetSelected(i == selectedIndex);
            }

            UpdateOperationButtons();
            onSelectionChanged?.Invoke(GetSelectedEntry());
        }

        private void InvokeWithSelection(Action<LevelFileEntry> action)
        {
            LevelFileEntry selectedEntry = GetSelectedEntry();
            if (selectedEntry != null)
            {
                action?.Invoke(selectedEntry);
            }
        }

        private void UpdateOperationButtons()
        {
            LevelFileEntry selectedEntry = GetSelectedEntry();
            bool hasSelection = selectedEntry != null;

            SetButtonInteractable(deleteButton, hasSelection);
            SetButtonInteractable(copyButton, hasSelection);
            SetButtonInteractable(renameButton, hasSelection);
            SetButtonInteractable(editButton, hasSelection);
        }

        private bool IsInMainFlow(LevelFileEntry entry)
        {
            return isInMainFlow != null && isInMainFlow(entry);
        }

        private LevelFileListItemView CreateItem()
        {
            if (itemPrefab == null || contentRoot == null)
            {
                Debug.LogError("LevelFilePanelView requires an item prefab and content root.");
                return null;
            }

            LevelFileListItemView item = Instantiate(itemPrefab, contentRoot);
            item.gameObject.SetActive(true);
            return item;
        }

        private void ClearLevelItems()
        {
            itemViews.Clear();
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

                Destroy(child.gameObject);
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
