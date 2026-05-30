using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class CommonEditorMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private GameObject root;
        [SerializeField] private Button returnEditorButton;
        [SerializeField] private Button returnTitleButton;
        [SerializeField] private Button testLevelButton;
        [SerializeField] private Button saveLevelButton;
        [SerializeField] private Button deleteLevelButton;

        public bool IsVisible => GetRoot().activeInHierarchy;

        private void Awake()
        {
            Hide();
        }

        public static CommonEditorMenuView CreateRuntime(Transform parent)
        {
            GameObject menuObject = CreateStack("Common Editor Menu Panel", parent);
            CommonEditorMenuView view = menuObject.AddComponent<CommonEditorMenuView>();
            view.container = menuObject;
            view.root = menuObject;
            view.returnEditorButton = CreateButton("返回编辑", menuObject.transform);
            view.returnTitleButton = CreateButton("返回标题页面", menuObject.transform);
            view.testLevelButton = CreateButton("试玩", menuObject.transform);
            view.saveLevelButton = CreateButton("保存关卡", menuObject.transform);
            view.deleteLevelButton = CreateButton("删除关卡", menuObject.transform);
            view.Hide();
            return view;
        }

        public void Initialize(
            Action onReturnEditor,
            Action onReturnTitle,
            Action onTestLevel,
            Action onSaveLevel,
            Action onDeleteLevel)
        {
            BindButton(returnEditorButton, onReturnEditor);
            BindButton(returnTitleButton, onReturnTitle);
            BindButton(testLevelButton, onTestLevel);
            BindButton(saveLevelButton, onSaveLevel);
            BindButton(deleteLevelButton, onDeleteLevel);
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

        private static GameObject CreateStack(string name, Transform parent)
        {
            GameObject stack = new GameObject(name);
            stack.transform.SetParent(parent, false);
            Image image = stack.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.72f);

            RectTransform rect = stack.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(280f, 260f);
            rect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup layout = stack.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 18, 18);
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            return stack;
        }

        private static Button CreateButton(string label, Transform parent)
        {
            GameObject buttonObject = new GameObject(label + " Button");
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.30f, 0.92f);

            Button button = buttonObject.AddComponent<Button>();

            TMP_Text text = CreateText("Text", buttonObject.transform, label, 17, TextAlignmentOptions.Center, Color.white);
            ConfigureFillRect(text.GetComponent<RectTransform>());

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 38f;
            return button;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, int size, TextAlignmentOptions alignment, Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            return text;
        }

        private static void ConfigureFillRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
