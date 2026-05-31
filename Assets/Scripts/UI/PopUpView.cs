using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sokoban
{
    public class PopUpView : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text confirmButtonText;
        [SerializeField] private TMP_Text alternateButtonText;
        [SerializeField] private TMP_Text cancelButtonText;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button alternateButton;
        [SerializeField] private Button cancelButton;

        private Coroutine moveCaretToEndCoroutine;
        private bool inputClickHandlerBound;

        private void Awake()
        {
            NameInputFilter.Configure(inputField);
            Hide();
        }

        public void Show(string message, Action onConfirm)
        {
            Show(message, "确认", "取消", onConfirm);
        }

        public void Show(string message, string confirmText, string cancelText, Action onConfirm, Action onCancel = null)
        {
            Show(message, confirmText, string.Empty, cancelText, onConfirm, null, onCancel);
        }

        public void Show(
            string message,
            string confirmText,
            string alternateText,
            string cancelText,
            Action onConfirm,
            Action onAlternate,
            Action onCancel = null)
        {
            SetInputVisible(false);
            if (messageText != null)
            {
                messageText.text = string.IsNullOrWhiteSpace(message) ? "你是否确认执行该操作？" : message;
            }

            SetButtonText(confirmButtonText, confirmText);
            SetButtonText(alternateButtonText, alternateText);
            SetButtonText(cancelButtonText, cancelText);
            SetAlternateButtonVisible(onAlternate != null);

            BindButton(confirmButton, () =>
            {
                Hide();
                onConfirm?.Invoke();
            });

            BindButton(alternateButton, () =>
            {
                Hide();
                onAlternate?.Invoke();
            });

            BindButton(cancelButton, () =>
            {
                Hide();
                onCancel?.Invoke();
            });
            ShowRoot();
        }

        public void ShowInput(
            string message,
            string initialValue,
            string confirmText,
            string cancelText,
            Action<string> onConfirm,
            Action onCancel = null)
        {
            if (inputField == null)
            {
                Show(message, confirmText, cancelText, () => onConfirm?.Invoke(initialValue), onCancel);
                return;
            }

            if (messageText != null)
            {
                messageText.text = string.IsNullOrWhiteSpace(message) ? "请输入内容。" : message;
            }

            SetButtonText(confirmButtonText, confirmText);
            SetButtonText(alternateButtonText, string.Empty);
            SetButtonText(cancelButtonText, cancelText);
            SetAlternateButtonVisible(false);
            ShowRoot();
            NameInputFilter.Configure(inputField);
            inputField.gameObject.SetActive(true);
            inputField.text = NameInputFilter.Sanitize(initialValue);
            inputField.ForceLabelUpdate();
            ForceRebuildLayout();
            EnsureInputCaretHandlers();

            BindButton(confirmButton, () =>
            {
                string value = inputField.text;
                Hide();
                onConfirm?.Invoke(value);
            });

            BindButton(cancelButton, () =>
            {
                Hide();
                onCancel?.Invoke();
            });

            inputField.Select();
            inputField.ActivateInputField();
            MoveInputCaretToEnd();
        }

        private void ShowRoot()
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
            SetInputVisible(false);
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

        private void SetInputVisible(bool visible)
        {
            if (inputField != null)
            {
                inputField.gameObject.SetActive(visible);
            }
        }

        private void SetAlternateButtonVisible(bool visible)
        {
            if (alternateButton != null)
            {
                alternateButton.gameObject.SetActive(visible);
            }
        }

        private void ForceRebuildLayout()
        {
            RectTransform rootTransform = GetRoot().transform as RectTransform;
            if (rootTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootTransform);
            }

            Canvas.ForceUpdateCanvases();
        }

        private void EnsureInputCaretHandlers()
        {
            if (inputField == null)
            {
                return;
            }

            inputField.onSelect.RemoveListener(MoveInputCaretToEnd);
            inputField.onSelect.AddListener(MoveInputCaretToEnd);

            if (inputClickHandlerBound)
            {
                return;
            }

            EventTrigger eventTrigger = inputField.GetComponent<EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = inputField.gameObject.AddComponent<EventTrigger>();
            }

            EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener(_ => MoveInputCaretToEndAfterClick());
            eventTrigger.triggers.Add(clickEntry);
            inputClickHandlerBound = true;
        }

        private void MoveInputCaretToEnd(string _)
        {
            MoveInputCaretToEndAfterClick();
        }

        private void MoveInputCaretToEndAfterClick()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (moveCaretToEndCoroutine != null)
            {
                StopCoroutine(moveCaretToEndCoroutine);
            }

            moveCaretToEndCoroutine = StartCoroutine(MoveInputCaretToEndAtEndOfFrame());
        }

        private IEnumerator MoveInputCaretToEndAtEndOfFrame()
        {
            yield return null;
            MoveInputCaretToEnd();
            moveCaretToEndCoroutine = null;
        }

        private void MoveInputCaretToEnd()
        {
            if (inputField == null)
            {
                return;
            }

            int endPosition = inputField.text == null ? 0 : inputField.text.Length;
            inputField.caretPosition = endPosition;
            inputField.selectionAnchorPosition = endPosition;
            inputField.selectionFocusPosition = endPosition;
            inputField.ForceLabelUpdate();
        }

    }
}
