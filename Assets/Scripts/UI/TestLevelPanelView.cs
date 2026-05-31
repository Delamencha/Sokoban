using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban
{
    public class TestLevelPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject container;
        [SerializeField] private GameObject root;
        [SerializeField] private Button returnTestButton;
        [SerializeField] private Button returnEditorButton;
        [SerializeField] private Button restartButton;

        public bool IsVisible => GetRoot().activeInHierarchy;

        private void Awake()
        {
            Hide();
        }

        public void Initialize(Action onReturnTest, Action onReturnEditor, Action onRestart)
        {
            BindButton(returnTestButton, onReturnTest);
            BindButton(returnEditorButton, onReturnEditor);
            BindButton(restartButton, onRestart);
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
    }
}
