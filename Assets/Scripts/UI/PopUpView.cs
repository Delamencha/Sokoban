using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class PopUpView : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text confirmButtonText;
        [SerializeField] private TMP_Text cancelButtonText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private void Awake()
        {
            Hide();
        }

        public static PopUpView CreateRuntime(Transform parent)
        {
            GameObject overlay = new GameObject("Pop Up View");
            overlay.transform.SetParent(parent, false);
            Image overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.55f);
            ConfigureFillRect(overlay.GetComponent<RectTransform>());

            GameObject dialog = new GameObject("Dialog");
            dialog.transform.SetParent(overlay.transform, false);
            Image dialogImage = dialog.AddComponent<Image>();
            dialogImage.color = new Color(0.92f, 0.92f, 0.92f, 1f);

            RectTransform dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(420f, 220f);
            dialogRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup dialogLayout = dialog.AddComponent<VerticalLayoutGroup>();
            dialogLayout.padding = new RectOffset(24, 24, 24, 24);
            dialogLayout.spacing = 18f;
            dialogLayout.childControlHeight = false;
            dialogLayout.childForceExpandHeight = false;

            TMP_Text message = CreateText("Message", dialog.transform, "你是否确认执行该操作？", 20, TextAlignmentOptions.Center, Color.black);
            LayoutElement messageLayout = message.GetComponent<LayoutElement>();
            messageLayout.preferredHeight = 88f;

            GameObject buttonRow = new GameObject("Button Row");
            buttonRow.transform.SetParent(dialog.transform, false);
            HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 16f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childForceExpandWidth = true;
            LayoutElement buttonRowLayout = buttonRow.AddComponent<LayoutElement>();
            buttonRowLayout.preferredHeight = 42f;

            Button confirm = CreateButton("确认", buttonRow.transform);
            Button cancel = CreateButton("取消", buttonRow.transform);
            TMP_Text confirmText = confirm.GetComponentInChildren<TMP_Text>();
            TMP_Text cancelText = cancel.GetComponentInChildren<TMP_Text>();

            PopUpView view = overlay.AddComponent<PopUpView>();
            view.container = overlay;
            view.root = overlay;
            view.messageText = message;
            view.confirmButtonText = confirmText;
            view.cancelButtonText = cancelText;
            view.confirmButton = confirm;
            view.cancelButton = cancel;
            view.Hide();
            return view;
        }

        public void Show(string message, Action onConfirm)
        {
            Show(message, "确认", "取消", onConfirm);
        }

        public void Show(string message, string confirmText, string cancelText, Action onConfirm, Action onCancel = null)
        {
            if (messageText != null)
            {
                messageText.text = string.IsNullOrWhiteSpace(message) ? "你是否确认执行该操作？" : message;
            }

            SetButtonText(confirmButtonText, confirmText);
            SetButtonText(cancelButtonText, cancelText);

            BindButton(confirmButton, () =>
            {
                Hide();
                onConfirm?.Invoke();
            });

            BindButton(cancelButton, () =>
            {
                Hide();
                onCancel?.Invoke();
            });
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

        private static void SetButtonText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
            }
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
            layout.preferredHeight = 40f;
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

            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = Mathf.Max(32f, size + 12f);
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
