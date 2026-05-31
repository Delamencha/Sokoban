using System;
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
        [SerializeField] private Button levelListButton;
        [SerializeField] private Button previousLevelButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button editLevelButton;

        public bool IsVisible => GetRoot().activeInHierarchy;

        private void Awake()
        {
            Hide();
        }

        public void Initialize(
            Action onReturnGame,
            Action onReturnTitle,
            Action onLevelList,
            Action onPreviousLevel,
            Action onNextLevel,
            Action onRestart,
            Action onEditLevel)
        {
            BindButton(returnGameButton, onReturnGame);
            BindButton(returnTitleButton, onReturnTitle);
            BindButton(levelListButton, onLevelList);
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

    }
}
