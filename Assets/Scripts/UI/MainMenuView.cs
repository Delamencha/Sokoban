using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class MainMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueButtonText;
        [SerializeField] private Button levelListButton;
        [SerializeField] private Button clearProgressButton;
        [SerializeField] private Button editorButton;
        [SerializeField] private Button mainFlowEditorButton;
        [SerializeField] private Button exitButton;

        public void Initialize(
            Action onContinue,
            Action onLevelList,
            Action onClearProgress,
            Action onEditor,
            Action onMainFlowEditor,
            Action onExit)
        {
            BindButton(continueButton, onContinue);
            BindButton(levelListButton, onLevelList);
            BindButton(clearProgressButton, onClearProgress);
            BindButton(editorButton, onEditor);
            BindButton(mainFlowEditorButton, onMainFlowEditor);
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

        public void SetContinueButtonText(string text)
        {
            if (continueButtonText != null)
            {
                continueButtonText.text = string.IsNullOrWhiteSpace(text) ? "开始游戏" : text;
            }
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

    }
}
