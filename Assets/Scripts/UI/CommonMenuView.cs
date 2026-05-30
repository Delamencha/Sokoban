using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class CommonMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private GameObject root;
        [SerializeField] private Button returnGameButton;
        [SerializeField] private Button returnTitleButton;
        [SerializeField] private Button previousLevelButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button editLevelButton;

        public bool IsVisible => GetRoot().activeInHierarchy;

        private void Awake()
        {
            Hide();
        }

        public static CommonMenuView CreateRuntime(Transform parent)
        {
            GameObject menuObject = CreateStack("Common Menu Panel", parent);
            CommonMenuView view = menuObject.AddComponent<CommonMenuView>();
            view.container = menuObject;
            view.root = menuObject;
            view.returnGameButton = CreateButton("返回游戏", menuObject.transform);
            view.returnTitleButton = CreateButton("返回标题页面", menuObject.transform);
            view.previousLevelButton = CreateButton("上一关", menuObject.transform);
            view.nextLevelButton = CreateButton("下一关", menuObject.transform);
            view.restartButton = CreateButton("重开", menuObject.transform);
            view.editLevelButton = CreateButton("编辑关卡", menuObject.transform);
            view.Hide();
            return view;
        }

        public void Initialize(
            Action onReturnGame,
            Action onReturnTitle,
            Action onPreviousLevel,
            Action onNextLevel,
            Action onRestart,
            Action onEditLevel)
        {
            BindButton(returnGameButton, onReturnGame);
            BindButton(returnTitleButton, onReturnTitle);
            BindButton(previousLevelButton, onPreviousLevel);
            BindButton(nextLevelButton, onNextLevel);
            BindButton(restartButton, onRestart);
            BindButton(editLevelButton, onEditLevel);
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
            rect.sizeDelta = new Vector2(280f, 300f);
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
