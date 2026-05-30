using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class LevelListView : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private GameObject root;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private LevelListItemView itemPrefab;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private Button backButton;

        public static LevelListView CreateRuntime(Transform parent)
        {
            GameObject scrollObject = CreateScrollView("Level List Panel", parent, out Transform content);
            LevelListView view = scrollObject.AddComponent<LevelListView>();
            view.container = scrollObject;
            view.root = scrollObject;
            view.contentRoot = content;
            view.emptyText = CreateText("Empty Text", content, "当前没有可用关卡", 16, TextAlignmentOptions.Left);
            view.backButton = CreateButton("返回开始页面", content);
            view.Hide();
            return view;
        }

        public void Initialize(Action onBack)
        {
            BindButton(backButton, onBack);
            HideEmptyText();
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
            IReadOnlyList<LevelData> levels,
            Func<LevelData, bool> isCompleted,
            Action<int> onSelectLevel)
        {
            ClearLevelItems();

            bool hasLevels = levels != null && levels.Count > 0;
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(!hasLevels);
            }

            if (!hasLevels)
            {
                return;
            }

            for (int i = 0; i < levels.Count; i++)
            {
                int levelIndex = i;
                LevelListItemView itemView = CreateItem();
                itemView.SetData(
                    levelIndex,
                    levels[i].displayName,
                    isCompleted != null && isCompleted(levels[i]),
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

            return LevelListItemView.CreateRuntime(contentRoot);
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

        private static GameObject CreateScrollView(string name, Transform parent, out Transform content)
        {
            GameObject scrollObject = new GameObject(name);
            scrollObject.transform.SetParent(parent, false);
            Image background = scrollObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.12f);

            LayoutElement layoutElement = scrollObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 410f;
            layoutElement.flexibleHeight = 1f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObject.transform, false);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport.transform, false);
            content = contentObject.transform;
            VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 6f;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter contentFitter = contentObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();

            ConfigureFillRect(viewportRect, 0f, 0f);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            scrollRectTransform.sizeDelta = Vector2.zero;
            return scrollObject;
        }

        private static Button CreateButton(string label, Transform parent)
        {
            GameObject buttonObject = new GameObject(label + " Button");
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.30f, 0.92f);

            Button button = buttonObject.AddComponent<Button>();
            TMP_Text text = CreateText("Text", buttonObject.transform, label, 16, TextAlignmentOptions.Center);
            ConfigureFillRect(text.GetComponent<RectTransform>(), 0f, 0f);

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 34f;
            return button;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, int size, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;

            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = Mathf.Max(32f, size + 12f);
            return text;
        }

        private static void ConfigureFillRect(RectTransform rect, float horizontalPadding, float verticalPadding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        }
    }
}
