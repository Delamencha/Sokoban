using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class MainMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button levelListButton;
        [SerializeField] private Button clearProgressButton;
        [SerializeField] private Button editorButton;
        [SerializeField] private Button exitButton;

        public static MainMenuView CreateRuntime(Transform parent)
        {
            GameObject menuObject = CreateStack("Start Panel", parent);
            MainMenuView view = menuObject.AddComponent<MainMenuView>();
            view.root = menuObject;
            view.continueButton = CreateButton("继续游戏", menuObject.transform);
            view.levelListButton = CreateButton("关卡列表", menuObject.transform);
            view.clearProgressButton = CreateButton("清空存档", menuObject.transform);
            view.editorButton = CreateButton("关卡编辑", menuObject.transform);
            view.exitButton = CreateButton("退出", menuObject.transform);
            return view;
        }

        public void Initialize(
            Action onContinue,
            Action onLevelList,
            Action onClearProgress,
            Action onEditor,
            Action onExit)
        {
            BindButton(continueButton, onContinue);
            BindButton(levelListButton, onLevelList);
            BindButton(clearProgressButton, onClearProgress);
            BindButton(editorButton, onEditor);
            BindButton(exitButton, onExit);
        }

        public void Show()
        {
            GetRoot().SetActive(true);
        }

        public void Hide()
        {
            GetRoot().SetActive(false);
        }

        private GameObject GetRoot()
        {
            return root != null ? root : gameObject;
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
            VerticalLayoutGroup layout = stack.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = stack.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return stack;
        }

        private static Button CreateButton(string label, Transform parent)
        {
            GameObject buttonObject = new GameObject(label + " Button");
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.30f, 0.92f);

            Button button = buttonObject.AddComponent<Button>();

            Text text = CreateText("Text", buttonObject.transform, label, 16, TextAnchor.MiddleCenter);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 34f;
            return button;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;

            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = Mathf.Max(32f, size + 12f);
            return text;
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }
    }
}
